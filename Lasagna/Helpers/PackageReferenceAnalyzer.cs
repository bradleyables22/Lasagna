using System.Text.Json;
using Lasagna.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lasagna.Helpers;

internal static class PackageReferenceAnalyzer
{
    private static readonly HashSet<string> ExcludedDirectories = new(
        [
            ".git",
            ".vs",
            ".idea",
            "bin",
            "obj",
            "TestResults",
            "packages",
            "artifacts",
            "build"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> SourceExtensions = new(
        [".cs", ".razor", ".cshtml", ".json", ".xml", ".config", ".yaml", ".yml"],
        StringComparer.OrdinalIgnoreCase);

    public static PackageReferencePlan BuildPlan(string scope = ".")
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);
        var dependencyReport = DependencyAnalyzer.BuildReport(rootPath);
        var packageReports = new List<PackageReferenceReport>();
        var issues = new List<string>();

        foreach (var project in dependencyReport.Projects.OrderBy(
                     project => project.Path,
                     StringComparer.OrdinalIgnoreCase))
        {
            var directReferences = dependencyReport.Edges
                .Where(edge => edge.Kind == DependencyEdgeKind.DirectPackageReference)
                .Where(edge => string.Equals(edge.From, project.NodeKey, StringComparison.OrdinalIgnoreCase))
                .Select(edge => dependencyReport.Nodes.FirstOrDefault(node =>
                    string.Equals(node.Key, edge.To, StringComparison.OrdinalIgnoreCase)))
                .Where(node => node is not null)
                .Cast<DependencyNode>()
                .GroupBy(node => node.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (directReferences.Length == 0)
                continue;

            try
            {
                packageReports.AddRange(AnalyzeProject(project, directReferences));
            }
            catch (Exception exception) when (exception is
                IOException or
                UnauthorizedAccessException or
                JsonException or
                InvalidDataException)
            {
                issues.Add($"Package usage analysis for '{project.Name}' could not be completed: {exception.Message}");

                packageReports.AddRange(directReferences.Select(package => new PackageReferenceReport(
                    project.Name,
                    project.Path,
                    package.Name,
                    package.Version,
                    PackageReferenceStatus.Review,
                    "The package usage scan could not complete; review this reference manually before removing it.",
                    [],
                    HasCompileAssets: false)));
            }
        }

        return new PackageReferencePlan(
            rootPath,
            dependencyReport.Projects.Count,
            packageReports
                .OrderBy(reference => reference.ProjectPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(reference => reference.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            issues);
    }

    private static IReadOnlyList<PackageReferenceReport> AnalyzeProject(
        DependencyProject project,
        IReadOnlyList<DependencyNode> directReferences)
    {
        var projectDirectory = Path.GetDirectoryName(project.Path)
            ?? throw new InvalidDataException($"The project '{project.Path}' has no directory.");
        var sourceFiles = EnumerateFiles(projectDirectory, ".cs").ToArray();
        var textFiles = EnumerateSourceFiles(projectDirectory).ToArray();
        var assets = ReadProjectAssets(projectDirectory, project.TargetFrameworks);
        var packageAssets = directReferences.ToDictionary(
            package => package.Name,
            package => assets?.Find(package.Name, package.Version) ?? [],
            StringComparer.OrdinalIgnoreCase);

        var allCompileAssets = assets?.AllCompileAssets ?? [];
        var packageReferences = new Dictionary<MetadataReference, string>();
        var metadataReferences = new List<MetadataReference>(ShakeAnalyzer.GetMetadataReferences());

        foreach (var asset in allCompileAssets)
        {
            if (!File.Exists(asset.Path))
                continue;

            var metadataReference = MetadataReference.CreateFromFile(asset.Path);
            metadataReferences.Add(metadataReference);

            if (packageAssets.Values.Any(package => package.Any(candidate =>
                    string.Equals(candidate.Path, asset.Path, StringComparison.OrdinalIgnoreCase))))
            {
                var package = packageAssets.First(pair => pair.Value.Any(candidate =>
                    string.Equals(candidate.Path, asset.Path, StringComparison.OrdinalIgnoreCase)));
                packageReferences[metadataReference] = package.Key;
            }
        }

        if (sourceFiles.Length == 0)
        {
            return directReferences.Select(package => new PackageReferenceReport(
                project.Name,
                project.Path,
                package.Name,
                package.Version,
                PackageReferenceStatus.Review,
                "No C# source files were found for this project, so package use could not be proven absent.",
                [],
                packageAssets[package.Name].Count > 0)).ToArray();
        }

        var trees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(
                File.ReadAllText(path),
                CSharpParseOptions.Default
                    .WithLanguageVersion(LanguageVersion.Latest)
                    .WithDocumentationMode(DocumentationMode.Parse),
                path))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            $"Lasagna.PackageUsage.{Guid.NewGuid():N}",
            trees,
            metadataReferences,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var packageAssemblies = BuildPackageAssemblies(compilation, packageReferences);
        var semanticEvidence = CollectSemanticEvidence(compilation, packageAssemblies);
        var diagnosticsPreventingProof = compilation.GetDiagnostics()
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var textualHints = CollectTextualHints(
            textFiles,
            directReferences,
            packageAssets,
            packageAssemblies);

        return directReferences.Select(package =>
        {
            var semantic = semanticEvidence.GetValueOrDefault(package.Name, []);
            var textual = textualHints.GetValueOrDefault(package.Name, []);
            var compileAssets = packageAssets[package.Name];
            var evidence = semantic.Concat(textual).ToArray();

            if (semantic.Count > 0)
            {
                return new PackageReferenceReport(
                    project.Name,
                    project.Path,
                    package.Name,
                    package.Version,
                    PackageReferenceStatus.Used,
                    "A source symbol resolves to an assembly supplied by this package.",
                    evidence,
                    compileAssets.Count > 0);
            }

            if (diagnosticsPreventingProof || compileAssets.Count == 0 || textual.Count > 0)
            {
                var reason = compileAssets.Count == 0
                    ? "This package has no restored compile-time assembly; analyzers, build assets, runtime behavior, and generated code cannot be proven unused by source scanning."
                    : diagnosticsPreventingProof
                        ? "The project has compilation errors, so the absence of a package reference cannot be proven safely."
                        : "No resolved source symbol was found, but text/configuration mentions this package or one of its assemblies.";

                return new PackageReferenceReport(
                    project.Name,
                    project.Path,
                    package.Name,
                    package.Version,
                    PackageReferenceStatus.Review,
                    reason,
                    evidence,
                    compileAssets.Count > 0);
            }

            return new PackageReferenceReport(
                project.Name,
                project.Path,
                package.Name,
                package.Version,
                PackageReferenceStatus.PotentiallyUnnecessary,
                "No semantic or textual source evidence was found for this direct package reference. Verify runtime, reflection, analyzer, and build use before removing it.",
                [],
                HasCompileAssets: true);
        }).ToArray();
    }

    private static Dictionary<string, IReadOnlyList<PackageReferenceEvidence>> CollectSemanticEvidence(
        CSharpCompilation compilation,
        IReadOnlyDictionary<string, IReadOnlySet<string>> packageAssemblies)
    {
        var evidence = new Dictionary<string, List<PackageReferenceEvidence>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(node).Symbol
                    ?? model.GetTypeInfo(node).Type;
                var assemblyName = symbol?.ContainingAssembly?.Identity.Name;

                if (string.IsNullOrWhiteSpace(assemblyName) ||
                    !packageAssemblies.TryGetValue(assemblyName, out var packages))
                {
                    continue;
                }

                foreach (var package in packages)
                {
                    AddEvidence(
                        evidence,
                        package,
                        new PackageReferenceEvidence(
                            tree.FilePath,
                            GetLine(node),
                            $"Semantic reference: {symbol!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}"));
                }
            }
        }

        return evidence.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PackageReferenceEvidence>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, IReadOnlyList<PackageReferenceEvidence>> CollectTextualHints(
        IReadOnlyList<string> files,
        IReadOnlyList<DependencyNode> packages,
        IReadOnlyDictionary<string, IReadOnlyList<PackageAsset>> packageAssets,
        IReadOnlyDictionary<string, IReadOnlySet<string>> packageAssemblies)
    {
        var hints = new Dictionary<string, List<PackageReferenceEvidence>>(StringComparer.OrdinalIgnoreCase);
        var tokens = packages.ToDictionary(
            package => package.Name,
            package => BuildTextTokens(package, packageAssets[package.Name], packageAssemblies),
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var package in packages)
                {
                    var token = tokens[package.Name].FirstOrDefault(candidate =>
                        lines[index].Contains(candidate, StringComparison.Ordinal));

                    if (token is null)
                        continue;

                    AddEvidence(
                        hints,
                        package.Name,
                        new PackageReferenceEvidence(
                            file,
                            index + 1,
                            $"Possible indirect or configuration reference containing '{token}'"));
                }
            }
        }

        return hints.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<PackageReferenceEvidence>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlySet<string> BuildTextTokens(
        DependencyNode package,
        IReadOnlyList<PackageAsset> assets,
        IReadOnlyDictionary<string, IReadOnlySet<string>> packageAssemblies)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal)
        {
            package.Name
        };

        foreach (var asset in assets)
            tokens.Add(Path.GetFileNameWithoutExtension(asset.Path));

        foreach (var assembly in assets
                     .Select(asset => Path.GetFileNameWithoutExtension(asset.Path))
                     .Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            tokens.Add(assembly);
        }

        foreach (var assembly in packageAssemblies)
        {
            if (assembly.Value.Contains(package.Name, StringComparer.OrdinalIgnoreCase))
                tokens.Add(assembly.Key);
        }

        return tokens
            .Where(token => token.Length >= 3)
            .OrderByDescending(token => token.Length)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlySet<string>> BuildPackageAssemblies(
        CSharpCompilation compilation,
        IReadOnlyDictionary<MetadataReference, string> packageReferences)
    {
        var assemblies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var packageReference in packageReferences)
        {
            if (compilation.GetAssemblyOrModuleSymbol(packageReference.Key) is not IAssemblySymbol assembly)
                continue;

            if (!assemblies.TryGetValue(assembly.Identity.Name, out var packages))
            {
                packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                assemblies.Add(assembly.Identity.Name, packages);
            }

            packages.Add(packageReference.Value);
        }

        return assemblies.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AddEvidence(
        IDictionary<string, List<PackageReferenceEvidence>> evidence,
        string package,
        PackageReferenceEvidence value)
    {
        if (!evidence.TryGetValue(package, out var values))
        {
            values = [];
            evidence.Add(package, values);
        }

        if (!values.Any(existing =>
                string.Equals(existing.FilePath, value.FilePath, StringComparison.OrdinalIgnoreCase) &&
                existing.Line == value.Line &&
                string.Equals(existing.Reason, value.Reason, StringComparison.Ordinal)))
        {
            values.Add(value);
        }
    }

    private static ProjectAssets? ReadProjectAssets(
        string projectDirectory,
        IReadOnlyList<string> targetFrameworks)
    {
        var path = Path.Combine(projectDirectory, "obj", "project.assets.json");
        if (!File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var packageFolders = document.RootElement.TryGetProperty("packageFolders", out var folders)
            ? folders.EnumerateObject().Select(folder => folder.Name).ToArray()
            : [];
        var libraries = document.RootElement.TryGetProperty("libraries", out var libraryElement)
            ? libraryElement
            : default;
        var assetsByPackage = new Dictionary<string, List<PackageAsset>>(StringComparer.OrdinalIgnoreCase);
        var allCompileAssets = new List<PackageAsset>();

        if (!document.RootElement.TryGetProperty("targets", out var targets))
            return new ProjectAssets(
                assetsByPackage.ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<PackageAsset>)pair.Value,
                    StringComparer.OrdinalIgnoreCase),
                allCompileAssets);

        var targetEntries = targets.EnumerateObject().ToArray();
        var selectedTargets = targetEntries
            .Where(target => targetFrameworks.Any(framework =>
                string.Equals(target.Name, framework, StringComparison.OrdinalIgnoreCase) ||
                target.Name.StartsWith(framework + "/", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (selectedTargets.Length == 0)
            selectedTargets = targetEntries.Take(1).ToArray();

        foreach (var target in selectedTargets)
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                var separator = library.Name.LastIndexOf('@');
                if (separator <= 0 || separator == library.Name.Length - 1 ||
                    !libraries.TryGetProperty(library.Name, out var libraryMetadata) ||
                    !libraryMetadata.TryGetProperty("path", out var libraryPathElement))
                {
                    continue;
                }

                var packageId = library.Name[..separator];
                var packageVersion = library.Name[(separator + 1)..];
                var packagePath = libraryPathElement.GetString();
                if (string.IsNullOrWhiteSpace(packagePath))
                    continue;

                if (!library.Value.TryGetProperty("compile", out var compileAssets))
                    continue;

                foreach (var compileAsset in compileAssets.EnumerateObject())
                {
                    if (compileAsset.Name.EndsWith("/_._", StringComparison.Ordinal) ||
                        compileAsset.Name.EndsWith("\\_._", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var fullPath = FindPackageAsset(packageFolders, packagePath, compileAsset.Name);
                    if (fullPath is null)
                        continue;

                    var asset = new PackageAsset(packageId, packageVersion, fullPath);
                    allCompileAssets.Add(asset);

                    if (!assetsByPackage.TryGetValue(packageId, out var packageValues))
                    {
                        packageValues = [];
                        assetsByPackage.Add(packageId, packageValues);
                    }

                    if (!packageValues.Any(existing => string.Equals(existing.Path, fullPath, StringComparison.OrdinalIgnoreCase)))
                        packageValues.Add(asset);
                }
            }
        }

        return new ProjectAssets(
            assetsByPackage.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<PackageAsset>)pair.Value,
                StringComparer.OrdinalIgnoreCase),
            allCompileAssets
                .GroupBy(asset => asset.Path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray());
    }

    private static string? FindPackageAsset(
        IReadOnlyList<string> packageFolders,
        string packagePath,
        string relativeAssetPath)
    {
        foreach (var folder in packageFolders)
        {
            var candidate = Path.GetFullPath(Path.Combine(
                folder,
                packagePath.Replace('/', Path.DirectorySeparatorChar),
                relativeAssetPath.Replace('/', Path.DirectorySeparatorChar)));

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string rootPath)
    {
        foreach (var extension in SourceExtensions)
        {
            foreach (var file in EnumerateFiles(rootPath, extension))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateFiles(string rootPath, string extension)
    {
        var directory = new DirectoryInfo(rootPath);
        if (!directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            yield break;

        foreach (var file in directory.EnumerateFiles())
        {
            if (file.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase) &&
                !IsGeneratedFile(file.Name))
            {
                yield return file.FullName;
            }
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if (ExcludedDirectories.Contains(child.Name) ||
                child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(child.FullName, extension))
                yield return file;
        }
    }

    private static bool IsGeneratedFile(string fileName) =>
        fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);

    private static int GetLine(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private sealed record PackageAsset(string PackageId, string Version, string Path);

    private sealed record ProjectAssets(
        IReadOnlyDictionary<string, IReadOnlyList<PackageAsset>> Packages,
        IReadOnlyList<PackageAsset> AllCompileAssets)
    {
        internal IReadOnlyList<PackageAsset> Find(string packageId, string? version) =>
            Packages.TryGetValue(packageId, out var values)
                ? values
                    .Where(asset => string.IsNullOrWhiteSpace(version) ||
                        string.Equals(asset.Version, version, StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : [];
    }
}
