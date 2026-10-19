using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class ShakeAnalyzer
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

    private static readonly HashSet<string> TextReferenceExtensions = new(
        [".cs", ".razor", ".cshtml", ".json", ".xml", ".config", ".yaml", ".yml"],
        StringComparer.OrdinalIgnoreCase);

    public static ShakePlan BuildPlan(string scope = ".", string? projectPath = null)
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);
        var sourceFiles = EnumerateFiles(rootPath, ".cs").ToArray();
        var referenceFiles = EnumerateTextReferenceFiles(rootPath).ToArray();
        var issues = new List<string>();

        if (sourceFiles.Length == 0)
        {
            return new ShakePlan(
                rootPath,
                ResolveBuildProject(rootPath, projectPath),
                0,
                0,
                [],
                ["No C# source files were found in the selected scope."]);
        }

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithDocumentationMode(DocumentationMode.Parse);
        var trees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(
                File.ReadAllText(path),
                parseOptions,
                path))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "Lasagna.Shake.Analysis",
            trees,
            GetMetadataReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        var typeGroups = CollectTypes(compilation, issues);
        var semanticReferences = CollectSemanticReferences(compilation, typeGroups);
        var textualHints = CollectTextualHints(
            rootPath,
            referenceFiles,
            typeGroups.Values);

        foreach (var group in typeGroups.Values)
        {
            group.References.AddRange(semanticReferences.GetValueOrDefault(group.Symbol, []));
            group.IndirectHints.AddRange(textualHints.GetValueOrDefault(group.Symbol, []));
        }

        var reports = sourceFiles
            .Select(path => BuildFileReport(rootPath, path, typeGroups.Values, issues))
            .OrderBy(report => report.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (issues.Count > 0)
        {
            reports = reports
                .Select(report => report.Status == ShakeFileStatus.Removable
                    ? report with
                    {
                        Status = ShakeFileStatus.Review,
                        Reason = "The scan found C# parse errors, so automatic removal is disabled until the scope parses cleanly."
                    }
                    : report)
                .ToArray();
        }

        return new ShakePlan(
            rootPath,
            ResolveBuildProject(rootPath, projectPath),
            sourceFiles.Length,
            typeGroups.Count,
            reports,
            issues);
    }

    public static ShakeApplyReport Apply(ShakePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var files = plan.RemovableFiles
            .Select(file => file.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            return new ShakeApplyReport(0, plan.BuildProjectPath, "No files were removed.");

        if (string.IsNullOrWhiteSpace(plan.BuildProjectPath))
        {
            throw new InvalidOperationException(
                "A single .csproj could not be identified for build validation. " +
                "Provide --project before applying the shake; no files were removed.");
        }

        var backups = files.ToDictionary(
            path => path,
            File.ReadAllBytes,
            StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in files)
                File.Delete(file);

            var buildOutput = string.IsNullOrWhiteSpace(plan.BuildProjectPath)
                ? "No project file was identified; the removed files were not build-validated."
                : Build(plan.BuildProjectPath);

            return new ShakeApplyReport(
                files.Length,
                plan.BuildProjectPath,
                buildOutput);
        }
        catch
        {
            foreach (var backup in backups)
                File.WriteAllBytes(backup.Key, backup.Value);

            throw;
        }
    }

    public static string? ResolveBuildProject(string rootPath, string? projectPath = null)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var resolved = DirectoryManager.GetExistingPath(projectPath);
            if (File.Exists(resolved))
            {
                if (!Path.GetExtension(resolved).Equals(
                        ".csproj",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "The build target must be a .csproj file or a directory containing one project.",
                        nameof(projectPath));
                }

                return Path.GetFullPath(resolved);
            }

            var selected = FindSingleProject(resolved);
            if (selected is null)
            {
                throw new InvalidOperationException(
                    $"Could not identify exactly one .csproj beneath '{projectPath}'.");
            }

            return selected;
        }

        return DirectoryManager.FindProjectFile(rootPath) ?? FindSingleProject(rootPath);
    }

    private static Dictionary<INamedTypeSymbol, TypeGroup> CollectTypes(
        CSharpCompilation compilation,
        ICollection<string> issues)
    {
        var groups = new Dictionary<INamedTypeSymbol, TypeGroup>(
            SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var parseErrors = tree
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Take(3)
                .ToArray();
            foreach (var error in parseErrors)
                issues.Add($"{Path.GetFileName(tree.FilePath)}: {error.GetMessage()}");

            foreach (var node in root.DescendantNodes().Where(IsTopLevelTypeDeclaration))
            {
                var symbol = GetDeclaredType(model, node);
                if (symbol is null)
                    continue;

                if (!groups.TryGetValue(symbol, out var group))
                {
                    group = new TypeGroup(symbol);
                    groups.Add(symbol, group);
                }

                group.Parts.Add(new TypePart(
                    tree.FilePath,
                    node,
                    GetLine(node),
                    GetAccessibility(node),
                    HasAttributes(node),
                    IsPartial(node),
                    HasEntryPoint(node)));
            }
        }

        return groups;
    }

    private static Dictionary<INamedTypeSymbol, List<ShakeReference>> CollectSemanticReferences(
        CSharpCompilation compilation,
        IReadOnlyDictionary<INamedTypeSymbol, TypeGroup> groups)
    {
        var references = new Dictionary<INamedTypeSymbol, List<ShakeReference>>(
            SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var node in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(node).Symbol
                    ?? model.GetTypeInfo(node).Type;
                var topLevelType = GetTopLevelType(symbol, groups);

                if (topLevelType is null || IsDeclarationName(node))
                    continue;

                if (!references.TryGetValue(topLevelType.Symbol, out var values))
                {
                    values = [];
                    references.Add(topLevelType.Symbol, values);
                }

                values.Add(new ShakeReference(
                    tree.FilePath,
                    GetLine(node),
                    topLevelType.DisplayName,
                    "Local semantic reference"));
            }
        }

        return references;
    }

    private static Dictionary<INamedTypeSymbol, List<ShakeReference>> CollectTextualHints(
        string rootPath,
        IReadOnlyList<string> referenceFiles,
        IEnumerable<TypeGroup> groups)
    {
        var bySimpleName = groups
            .GroupBy(group => group.Symbol.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var hints = new Dictionary<INamedTypeSymbol, List<ShakeReference>>(
            SymbolEqualityComparer.Default);

        foreach (var file in referenceFiles)
        {
            var extension = Path.GetExtension(file);
            var content = File.ReadAllText(file);

            if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var tree = CSharpSyntaxTree.ParseText(content, path: file);
                foreach (var literal in tree.GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>()
                             .Where(node => node.IsKind(SyntaxKind.StringLiteralExpression)))
                {
                    AddTextualHints(
                        hints,
                        bySimpleName,
                        literal.Token.ValueText,
                        file,
                        GetLine(literal),
                        "Possible string-based or reflection reference");
                }

                continue;
            }

            var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var group in groups)
                {
                    if (!lines[index].Contains(group.Symbol.Name, StringComparison.Ordinal))
                        continue;

                    AddHint(
                        hints,
                        group,
                        new ShakeReference(
                            file,
                            index + 1,
                            group.DisplayName,
                            $"Possible indirect reference in {Path.GetExtension(file)} content"));
                }
            }
        }

        return hints;
    }

    private static void AddTextualHints(
        IDictionary<INamedTypeSymbol, List<ShakeReference>> hints,
        IReadOnlyDictionary<string, TypeGroup[]> bySimpleName,
        string value,
        string filePath,
        int line,
        string reason)
    {
        foreach (var pair in bySimpleName)
        {
            if (!value.Contains(pair.Key, StringComparison.Ordinal))
                continue;

            foreach (var group in pair.Value)
            {
                AddHint(
                    hints,
                    group,
                    new ShakeReference(filePath, line, group.DisplayName, reason));
            }
        }
    }

    private static void AddHint(
        IDictionary<INamedTypeSymbol, List<ShakeReference>> hints,
        TypeGroup group,
        ShakeReference reference)
    {
        if (!hints.TryGetValue(group.Symbol, out var values))
        {
            values = [];
            hints.Add(group.Symbol, values);
        }

        if (!values.Any(existing =>
                existing.FilePath.Equals(reference.FilePath, StringComparison.OrdinalIgnoreCase) &&
                existing.Line == reference.Line &&
                existing.Reason == reference.Reason))
        {
            values.Add(reference);
        }
    }

    private static ShakeFileReport BuildFileReport(
        string rootPath,
        string filePath,
        IEnumerable<TypeGroup> allGroups,
        ICollection<string> issues)
    {
        var parts = allGroups
            .SelectMany(group => group.Parts.Select(part => (Group: group, Part: part)))
            .Where(entry => entry.Part.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (parts.Length == 0)
        {
            return new ShakeFileReport(
                filePath,
                ShakeFileStatus.Protected,
                "No top-level type declaration was discovered; the file was left untouched.",
                []);
        }

        var symbols = parts
            .Select(entry => ToEvidence(entry.Group, entry.Part))
            .ToArray();
        var uniqueGroups = parts
            .Select(entry => entry.Group)
            .DistinctBy(group => group.Symbol, SymbolEqualityComparer.Default)
            .ToArray();

        if (uniqueGroups.Any(group => group.HasEntryPoint))
        {
            return new ShakeFileReport(
                filePath,
                ShakeFileStatus.Protected,
                "The file contains an application entry point.",
                symbols);
        }

        if (uniqueGroups.Any(group => group.ExternalReferences.Count > 0))
        {
            return new ShakeFileReport(
                filePath,
                ShakeFileStatus.Referenced,
                "One or more local types have semantic references elsewhere in the scanned codebase.",
                symbols);
        }

        if (uniqueGroups.Any(group =>
                group.IsPublicApi ||
                group.IsPartial ||
                group.HasAttributes ||
                group.IndirectHints.Count > 0))
        {
            return new ShakeFileReport(
                filePath,
                ShakeFileStatus.Review,
                "No local semantic references were found, but the file has public, partial, attributed, or indirect-use indicators.",
                symbols);
        }

        return new ShakeFileReport(
            filePath,
            ShakeFileStatus.Removable,
            "Every top-level type has zero local semantic references and no indirect-use indicators.",
            symbols);
    }

    private static ShakeSymbolEvidence ToEvidence(TypeGroup group, TypePart part)
        => new(
            group.DisplayName,
            part.FilePath,
            part.Line,
            part.Accessibility,
            group.IsPublicApi,
            group.IsPartial,
            group.HasAttributes,
            group.ExternalReferences,
            group.IndirectHints);

    private static IEnumerable<string> EnumerateTextReferenceFiles(string rootPath)
    {
        foreach (var extension in TextReferenceExtensions)
        {
            foreach (var file in EnumerateFiles(rootPath, extension))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateFiles(string rootPath, string extension)
    {
        var directory = new DirectoryInfo(rootPath);
        if (!directory.Exists || IsReparsePoint(directory))
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
            if (ExcludedDirectories.Contains(child.Name) || IsReparsePoint(child))
                continue;

            foreach (var file in EnumerateFiles(child.FullName, extension))
                yield return file;
        }
    }

    private static bool IsTopLevelTypeDeclaration(SyntaxNode node)
    {
        if (node is not BaseTypeDeclarationSyntax and not DelegateDeclarationSyntax)
            return false;

        return node.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax;
    }

    private static INamedTypeSymbol? GetDeclaredType(
        SemanticModel model,
        SyntaxNode node)
        => node switch
        {
            BaseTypeDeclarationSyntax type => model.GetDeclaredSymbol(type),
            DelegateDeclarationSyntax @delegate => model.GetDeclaredSymbol(@delegate),
            _ => null
        };

    private static TypeGroup? GetTopLevelType(
        ISymbol? symbol,
        IReadOnlyDictionary<INamedTypeSymbol, TypeGroup> groups)
    {
        var type = symbol switch
        {
            INamedTypeSymbol namedType => namedType,
            IMethodSymbol method => method.ContainingType,
            IPropertySymbol property => property.ContainingType,
            IFieldSymbol field => field.ContainingType,
            IEventSymbol @event => @event.ContainingType,
            _ => null
        };

        if (type is null)
            return null;

        while (type.ContainingType is not null)
            type = type.ContainingType;

        return groups.GetValueOrDefault(type);
    }

    private static bool IsDeclarationName(SimpleNameSyntax node)
    {
        return node.Parent is VariableDeclaratorSyntax or
            ParameterSyntax or
            MethodDeclarationSyntax or
            PropertyDeclarationSyntax or
            EventDeclarationSyntax or
            DelegateDeclarationSyntax;
    }

    private static bool HasEntryPoint(SyntaxNode node)
    {
        return node.DescendantNodes().OfType<MethodDeclarationSyntax>()
                   .Any(method => method.Identifier.Text.Equals("Main", StringComparison.Ordinal)) ||
               node.SyntaxTree.GetRoot().DescendantNodes().OfType<GlobalStatementSyntax>().Any();
    }

    private static bool HasAttributes(SyntaxNode node)
        => node switch
        {
            BaseTypeDeclarationSyntax type => type.AttributeLists.Count > 0,
            DelegateDeclarationSyntax @delegate => @delegate.AttributeLists.Count > 0,
            _ => false
        };

    private static bool IsPartial(SyntaxNode node)
        => node switch
        {
            BaseTypeDeclarationSyntax type => type.Modifiers.Any(SyntaxKind.PartialKeyword),
            _ => false
        };

    private static string GetAccessibility(SyntaxNode node)
    {
        var modifiers = node switch
        {
            BaseTypeDeclarationSyntax type => type.Modifiers,
            DelegateDeclarationSyntax @delegate => @delegate.Modifiers,
            _ => default
        };

        if (modifiers.Any(SyntaxKind.PublicKeyword))
            return "public";
        if (modifiers.Any(SyntaxKind.InternalKeyword))
            return "internal";
        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
            return "protected";
        if (modifiers.Any(SyntaxKind.PrivateKeyword))
            return "private";

        return "implicit";
    }

    private static int GetLine(SyntaxNode node)
        => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static bool IsGeneratedFile(string fileName)
    {
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(FileSystemInfo entry)
        => entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    private static IReadOnlyList<MetadataReference> GetMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")?.ToString();

        if (!string.IsNullOrWhiteSpace(trusted))
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
                if (File.Exists(path))
                    paths.Add(path);
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
                paths.Add(assembly.Location);
        }

        return paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    private static string? FindSingleProject(string rootPath)
    {
        var projects = EnumerateProjectFiles(rootPath).ToArray();
        return projects.Length == 1 ? projects[0] : null;
    }

    private static IEnumerable<string> EnumerateProjectFiles(string rootPath)
    {
        var directory = new DirectoryInfo(rootPath);
        if (!directory.Exists || IsReparsePoint(directory))
            yield break;

        foreach (var file in directory.EnumerateFiles("*.csproj"))
            yield return file.FullName;

        foreach (var child in directory.EnumerateDirectories())
        {
            if (ExcludedDirectories.Contains(child.Name) || IsReparsePoint(child))
                continue;

            foreach (var project in EnumerateProjectFiles(child.FullName))
                yield return project;
        }
    }

    private static string Build(string projectPath)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--nologo");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet build after shake.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            throw new TimeoutException("The build did not finish within two minutes. Removed files were restored.");
        }

        var output = outputTask.GetAwaiter().GetResult() + errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            var details = output.Length > 4000 ? output[^4000..] : output;
            throw new InvalidDataException(
                $"The build failed after shake. Removed files were restored.{Environment.NewLine}{details}");
        }

        return output;
    }

    private sealed class TypeGroup(INamedTypeSymbol symbol)
    {
        internal INamedTypeSymbol Symbol { get; } = symbol;

        internal string DisplayName => Symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        internal List<TypePart> Parts { get; } = [];

        internal List<ShakeReference> References { get; } = [];

        internal List<ShakeReference> IndirectHints { get; } = [];

        internal IReadOnlyList<ShakeReference> ExternalReferences => References
            .Where(reference => !Parts.Any(part =>
                part.FilePath.Equals(reference.FilePath, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(reference =>
                (reference.FilePath, reference.Line, reference.Reason))
            .ToArray();

        internal bool IsPublicApi =>
            Parts.Any(part => part.Accessibility is "public" or "protected");

        internal bool IsPartial => Parts.Count > 1 || Parts.Any(part => part.IsPartial);

        internal bool HasAttributes => Parts.Any(part => part.HasAttributes);

        internal bool HasEntryPoint => Parts.Any(part => part.HasEntryPoint);
    }

    private sealed record TypePart(
        string FilePath,
        SyntaxNode Node,
        int Line,
        string Accessibility,
        bool HasAttributes,
        bool IsPartial,
        bool HasEntryPoint);
}
