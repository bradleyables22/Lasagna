using System.Text;
using System.Xml.Linq;
using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class KitReplacementManager
{
    private static readonly HashSet<string> ExcludedDirectories = new(
        [".git", ".vs", "bin", "obj", "TestResults", ".idea"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NamespaceAwareExtensions = new(
        [".cs", ".razor", ".cshtml"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] SharedBuildFileNames =
    [
        "Directory.Build.props",
        "Directory.Build.targets"
    ];

    internal static KitReplacementPlan BuildPlan(
        string packageId,
        string kitName,
        string projectPath,
        string destination,
        string? targetNamespace,
        bool rewriteNamespace,
        bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kitName);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        var normalizedPackageId = packageId.Trim();
        var normalizedProjectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(normalizedProjectPath))
            throw new FileNotFoundException($"The project file '{projectPath}' was not found.", normalizedProjectPath);

        if (!string.Equals(Path.GetExtension(normalizedProjectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Kit replacement currently targets C# project files (.csproj).", nameof(projectPath));

        var projectRoot = Path.GetDirectoryName(normalizedProjectPath)
            ?? throw new InvalidOperationException($"The project '{normalizedProjectPath}' has no containing directory.");
        var destinationRoot = ResolveDestination(projectRoot, destination);
        var kit = KitManager.Read(kitName);
        var kitRoot = KitManager.GetKitPath(kit.Name);
        var kitFiles = ReadKitFiles(kit, kitRoot);

        if (kitFiles.Count == 0)
            throw new InvalidDataException($"Kit '{kitName}' does not contain any files.");

        var effectiveTargetNamespace = rewriteNamespace
            ? targetNamespace?.Trim() ?? DirectoryManager.GetProjectNamespace(normalizedProjectPath)
            : null;
        var effectiveSourceNamespace = rewriteNamespace
            ? kit.SourceNamespace ?? NamespaceDetector.Detect(kitFiles)
            : null;

        if (rewriteNamespace && string.IsNullOrWhiteSpace(effectiveTargetNamespace))
        {
            throw new InvalidOperationException(
                "A target namespace could not be determined. Use --namespace or --no-namespace-rewrite.");
        }

        if (rewriteNamespace && string.IsNullOrWhiteSpace(effectiveSourceNamespace))
        {
            throw new InvalidDataException(
                $"The source namespace for kit '{kitName}' could not be detected. Use --no-namespace-rewrite or provide a kit with source namespace metadata.");
        }

        var destinationPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plannedFiles = new List<KitReplacementFile>();

        foreach (var kitFile in kit.Files)
        {
            var sourcePath = ResolveKitFile(kitRoot, kitFile);
            var destinationPath = Path.GetFullPath(Path.Combine(
                destinationRoot,
                kitFile.Replace('/', Path.DirectorySeparatorChar)));

            EnsureInside(destinationPath, projectRoot, "Kit files must remain inside the target project.");

            if (!destinationPaths.Add(destinationPath))
                throw new IOException($"The kit contains duplicate destination path '{kitFile}'.");

            if (string.Equals(destinationPath, normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A kit file cannot overwrite the selected project file.");

            if (Directory.Exists(destinationPath))
                throw new IOException($"The destination '{destinationPath}' is already a directory.");

            var original = File.Exists(destinationPath) ? File.ReadAllBytes(destinationPath) : null;
            if (original is not null && !overwrite)
            {
                throw new IOException(
                    $"The destination '{destinationPath}' already exists. Use --overwrite to replace it.");
            }

            var prepared = PrepareFile(
                sourcePath,
                destinationPath,
                KitReplacementFileKind.KitFile,
                effectiveSourceNamespace,
                effectiveTargetNamespace,
                rewriteNamespace);

            plannedFiles.Add(prepared with { OriginalContent = original });
        }

        if (rewriteNamespace &&
            !string.Equals(effectiveSourceNamespace, effectiveTargetNamespace, StringComparison.Ordinal))
        {
            foreach (var projectSource in EnumerateProjectSourceFiles(projectRoot))
            {
                if (string.Equals(projectSource, normalizedProjectPath, StringComparison.OrdinalIgnoreCase) ||
                    destinationPaths.Contains(projectSource))
                {
                    continue;
                }

                var result = NamespaceRewriter.RewriteFile(
                    projectSource,
                    effectiveSourceNamespace!,
                    effectiveTargetNamespace!);

                if (!result.Succeeded)
                {
                    throw new InvalidDataException(
                        result.Message ?? $"Could not rewrite namespace in '{projectSource}'.");
                }

                if (!result.Changed)
                    continue;

                plannedFiles.Add(new KitReplacementFile(
                    projectSource,
                    projectSource,
                    Encoding.UTF8.GetBytes(result.Content),
                    File.ReadAllBytes(projectSource),
                    KitReplacementFileKind.ProjectSource,
                    true,
                    result.Changes.Count));
            }
        }

        var projectFile = BuildProjectFileChange(
            normalizedProjectPath,
            normalizedPackageId,
            projectRoot,
            out var packageReferencesRemoved);
        plannedFiles.Add(projectFile);

        return new KitReplacementPlan(
            normalizedPackageId,
            kit.Name,
            normalizedProjectPath,
            destinationRoot,
            effectiveSourceNamespace,
            effectiveTargetNamespace,
            plannedFiles,
            packageReferencesRemoved);
    }

    internal static KitReplacementReport Apply(
        KitReplacementPlan plan,
        Action<TransferProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var written = new List<KitReplacementFile>();
        try
        {
            var kitFiles = plan.Files
                .Where(file => file.Kind == KitReplacementFileKind.KitFile)
                .ToArray();
            var completed = 0;

            foreach (var file in plan.Files)
            {
                var parent = Path.GetDirectoryName(file.DestinationPath);
                if (parent is not null)
                    Directory.CreateDirectory(parent);

                File.WriteAllBytes(file.DestinationPath, file.Content);
                written.Add(file);

                if (file.Kind == KitReplacementFileKind.KitFile)
                {
                    completed++;
                    progress?.Invoke(new TransferProgress(
                        completed,
                        kitFiles.Length,
                        file.DestinationPath));
                }
            }

            return new KitReplacementReport(
                plan.Files.Count,
                plan.KitFileCount,
                plan.ExistingProjectFileCount,
                plan.NamespaceReferencesRewritten,
                plan.PackageReferencesRemoved);
        }
        catch
        {
            Rollback(written);
            throw;
        }
    }

    private static KitReplacementFile PrepareFile(
        string sourcePath,
        string destinationPath,
        KitReplacementFileKind kind,
        string? sourceNamespace,
        string? targetNamespace,
        bool rewriteNamespace)
    {
        if (rewriteNamespace &&
            !string.IsNullOrWhiteSpace(sourceNamespace) &&
            !string.IsNullOrWhiteSpace(targetNamespace) &&
            NamespaceAwareExtensions.Contains(Path.GetExtension(sourcePath)))
        {
            var result = NamespaceRewriter.RewriteContent(
                sourcePath,
                File.ReadAllText(sourcePath),
                sourceNamespace,
                targetNamespace);

            if (!result.Succeeded)
            {
                throw new InvalidDataException(
                    result.Message ?? $"Could not rewrite namespace in '{sourcePath}'.");
            }

            return new KitReplacementFile(
                sourcePath,
                destinationPath,
                Encoding.UTF8.GetBytes(result.Content),
                null,
                kind,
                result.Changed,
                result.Changes.Count);
        }

        return new KitReplacementFile(
            sourcePath,
            destinationPath,
            File.ReadAllBytes(sourcePath),
            null,
            kind,
            false,
            0);
    }

    private static KitReplacementFile BuildProjectFileChange(
        string projectPath,
        string packageId,
        string projectRoot,
        out int removedCount)
    {
        var document = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
        var sharedReference = FindSharedPackageReference(projectRoot, packageId);
        if (sharedReference is not null)
        {
            throw new InvalidOperationException(
                $"Package '{packageId}' is referenced from shared build file '{sharedReference}'. Refusing to change a shared file automatically.");
        }

        var references = document
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
            .Where(element => IsPackageReference(element, packageId))
            .ToArray();

        if (references.Length == 0)
        {
            throw new InvalidOperationException(
                $"Project '{Path.GetFileName(projectPath)}' does not directly reference package '{packageId}'.");
        }

        foreach (var reference in references)
            reference.Remove();

        var updated = document.ToString(SaveOptions.DisableFormatting);
        removedCount = references.Length;
        return new KitReplacementFile(
            projectPath,
            projectPath,
            Encoding.UTF8.GetBytes(updated),
            File.ReadAllBytes(projectPath),
            KitReplacementFileKind.ProjectFile,
            false,
            0);
    }

    private static IReadOnlyList<string> ReadKitFiles(KitManifest manifest, string kitRoot)
    {
        var files = new List<string>(manifest.Files.Count);
        foreach (var relativePath in manifest.Files)
        {
            var path = ResolveKitFile(kitRoot, relativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Kit '{manifest.Name}' is missing '{relativePath}'.",
                    path);
            }

            files.Add(path);
        }

        return files;
    }

    private static string ResolveKitFile(string kitRoot, string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(
            kitRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        EnsureInside(path, kitRoot, "Kit paths must remain inside the kit folder.");
        return path;
    }

    private static string ResolveDestination(string projectRoot, string destination)
    {
        var normalized = destination.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized == ".")
            return Path.GetFullPath(projectRoot);

        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Replacement destinations must be relative paths inside the project.",
                nameof(destination));
        }

        var resolved = Path.GetFullPath(Path.Combine(projectRoot, normalized));
        EnsureInside(resolved, projectRoot, "Replacement destinations must remain inside the project.");
        return resolved;
    }

    private static void EnsureInside(string path, string root, string message)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);

        if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalizedRoot.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(message);
        }
    }

    private static IEnumerable<string> EnumerateProjectSourceFiles(string root)
    {
        foreach (var file in EnumerateFiles(root))
        {
            if (NamespaceAwareExtensions.Contains(Path.GetExtension(file)) &&
                !IsGeneratedFile(file))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        DirectoryInfo directory;
        try
        {
            directory = new DirectoryInfo(root);
            if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        IEnumerable<FileInfo> files;
        IEnumerable<DirectoryInfo> directories;
        try
        {
            files = directory.EnumerateFiles();
            directories = directory.EnumerateDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
            yield return file.FullName;

        foreach (var child in directories)
        {
            if (ExcludedDirectories.Contains(child.Name) ||
                child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            foreach (var file in EnumerateFiles(child.FullName))
                yield return file;
        }
    }

    private static bool IsGeneratedFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPackageReference(XElement element, string packageId)
    {
        var include = element.Attribute("Include")?.Value.Trim();
        var update = element.Attribute("Update")?.Value.Trim();
        return string.Equals(include, packageId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(update, packageId, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindSharedPackageReference(string projectRoot, string packageId)
    {
        var directory = new DirectoryInfo(projectRoot);
        while (directory is not null)
        {
            foreach (var fileName in SharedBuildFileNames)
            {
                var path = Path.Combine(directory.FullName, fileName);
                if (!File.Exists(path))
                    continue;

                try
                {
                    var document = XDocument.Load(path);
                    if (document.Descendants()
                        .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
                        .Any(element => IsPackageReference(element, packageId)))
                    {
                        return path;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
                {
                    throw new InvalidDataException($"Could not inspect shared build file '{path}'.", exception);
                }
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static void Rollback(IEnumerable<KitReplacementFile> written)
    {
        foreach (var file in written.Reverse())
        {
            try
            {
                if (file.OriginalContent is null)
                {
                    if (File.Exists(file.DestinationPath))
                        File.Delete(file.DestinationPath);
                }
                else
                {
                    File.WriteAllBytes(file.DestinationPath, file.OriginalContent);
                }
            }
            catch
            {
                // Preserve the original apply exception; the caller can inspect the affected paths.
            }
        }
    }
}
