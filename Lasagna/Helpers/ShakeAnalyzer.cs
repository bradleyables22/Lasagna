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

    public static ShakePlan BuildPlan(string scope = ".")
    {
        var rootPath = DirectoryManager.GetDirectoryPath(scope);
        var sourceFiles = EnumerateFiles(rootPath, ".cs").ToArray();
        var referenceFiles = EnumerateTextReferenceFiles(rootPath).ToArray();
        var issues = new List<string>();

        if (sourceFiles.Length == 0)
        {
            return new ShakePlan(
                rootPath,
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
            sourceFiles.Length,
            typeGroups.Count,
            reports,
            issues);
    }

    public static ShakeApplyReport Apply(ShakePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return ApplyFiles(plan, plan.RemovableFiles);
    }

    public static ShakeApplyReport ApplyFile(
        ShakePlan plan,
        string filePath,
        bool approveReview = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedFilePath = Path.GetFullPath(filePath);
        var candidate = plan.Files.FirstOrDefault(file =>
            string.Equals(
                Path.GetFullPath(file.FilePath),
                resolvedFilePath,
                StringComparison.OrdinalIgnoreCase));

        if (candidate is null ||
            (candidate.Status != ShakeFileStatus.Removable &&
                !(approveReview && candidate.Status == ShakeFileStatus.Review)))
        {
            throw new InvalidOperationException(
                "The selected file is no longer an eligible removal candidate. Re-scan the scope and review the current evidence.");
        }

        return ApplyFiles(plan, [candidate]);
    }

    public static ShakeApplyReport ApplyDirectory(
        ShakePlan plan,
        string directoryPath,
        bool approveReview)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        var rootPath = Path.GetFullPath(plan.RootPath);
        var resolvedDirectory = Path.GetFullPath(directoryPath);

        if (!Directory.Exists(resolvedDirectory))
            throw new DirectoryNotFoundException(
                $"The directory '{directoryPath}' was not found.");

        if (!IsDescendantPath(rootPath, resolvedDirectory) ||
            IsReparsePoint(new DirectoryInfo(resolvedDirectory)))
        {
            throw new InvalidOperationException(
                "Only a child directory inside the selected shake scope can be removed.");
        }

        ValidateDirectoryTree(resolvedDirectory);

        var filesOnDisk = EnumerateAllFiles(resolvedDirectory).ToArray();
        var reports = plan.Files
            .Where(file => IsDescendantPath(resolvedDirectory, Path.GetFullPath(file.FilePath)))
            .ToArray();
        var reportPaths = reports
            .Select(file => Path.GetFullPath(file.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (filesOnDisk.Length == 0)
        {
            throw new InvalidOperationException(
                "The selected directory contains no files to remove.");
        }

        var unexpectedFiles = filesOnDisk
            .Where(file => !reportPaths.Contains(Path.GetFullPath(file)))
            .ToArray();

        if (unexpectedFiles.Length > 0 || reports.Length != filesOnDisk.Length)
        {
            throw new InvalidOperationException(
                "The selected directory contains files outside the shake results. " +
                "Review and remove those files separately before deleting the directory.");
        }

        var disallowed = reports
            .Where(file => file.Status != ShakeFileStatus.Removable &&
                !(approveReview && file.Status == ShakeFileStatus.Review))
            .ToArray();

        if (disallowed.Length > 0)
        {
            throw new InvalidOperationException(
                $"The selected directory contains {disallowed.Length} file{(disallowed.Length == 1 ? string.Empty : "s")} " +
                "that are referenced or protected. Remove those files separately only after reviewing their evidence.");
        }

        return ApplyFiles(plan, reports, resolvedDirectory);
    }

    private static ShakeApplyReport ApplyFiles(
        ShakePlan plan,
        IEnumerable<ShakeFileReport> candidates,
        string? directoryToRemove = null)
    {
        var files = candidates
            .Select(file => file.FilePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            return new ShakeApplyReport(0);

        foreach (var file in files)
            File.Delete(file);

        if (directoryToRemove is not null)
            Directory.Delete(directoryToRemove, recursive: true);

        return new ShakeApplyReport(files.Length);
    }

    private static bool IsDescendantPath(string rootPath, string path)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);

        return relativePath != "." &&
               !Path.IsPathRooted(relativePath) &&
               !relativePath.Equals("..", StringComparison.Ordinal) &&
               !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static void ValidateDirectoryTree(string rootPath)
    {
        var directory = new DirectoryInfo(rootPath);

        foreach (var file in directory.EnumerateFiles())
        {
            if (IsReparsePoint(file))
            {
                throw new InvalidOperationException(
                    $"The selected directory contains a linked file '{file.FullName}'.");
            }
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if (IsReparsePoint(child))
            {
                throw new InvalidOperationException(
                    $"The selected directory contains a linked directory '{child.FullName}'.");
            }

            if (ExcludedDirectories.Contains(child.Name))
            {
                throw new InvalidOperationException(
                    $"The selected directory contains an excluded build or tool directory '{child.FullName}'.");
            }

            ValidateDirectoryTree(child.FullName);
        }
    }

    private static IEnumerable<string> EnumerateAllFiles(string rootPath)
    {
        var directory = new DirectoryInfo(rootPath);

        foreach (var file in directory.EnumerateFiles())
            yield return file.FullName;

        foreach (var child in directory.EnumerateDirectories())
        {
            foreach (var file in EnumerateAllFiles(child.FullName))
                yield return file;
        }
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
                group.HasPolymorphicContract ||
                group.IndirectHints.Count > 0))
        {
            return new ShakeFileReport(
                filePath,
                ShakeFileStatus.Review,
                "No local semantic references were found, but the file has public, partial, attributed, polymorphic, or indirect-use indicators.",
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

    internal static IReadOnlyList<MetadataReference> GetMetadataReferences()
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

        internal bool HasPolymorphicContract =>
            Symbol.TypeKind == TypeKind.Class &&
            (Symbol.AllInterfaces.Length > 0 ||
             (Symbol.BaseType is not null &&
              Symbol.BaseType.SpecialType is not SpecialType.System_Object and not SpecialType.System_ValueType) ||
             Symbol.GetMembers().OfType<IMethodSymbol>().Any(method =>
                 method.IsAbstract || method.IsOverride || method.IsVirtual));
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
