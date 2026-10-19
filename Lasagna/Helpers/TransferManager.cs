using Lasagna.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Lasagna.Helpers;

internal static class TransferManager
{
    public static TransferPlan BuildPlan(
        string name,
        string? targetNamespace,
        bool rewriteNamespace,
        string destination = ".",
        bool inferDestinationNamespace = false)
    {
        if (BundleManager.Exists(name))
        {
            var bundle = BundleManager.Read(name);
            var files = bundle.Items
                .SelectMany(item => BuildItemFiles(
                    item.ItemName,
                    item.Destination,
                    item.RewriteNamespace && rewriteNamespace,
                    item.TargetNamespace ?? targetNamespace,
                    destination,
                    inferDestinationNamespace,
                    item.TargetNamespace is null))
                .ToArray();

            return new TransferPlan(name, TransferPlanKind.Bundle, files);
        }

        if (KitManager.Exists(name))
        {
            var kit = KitManager.Read(name);
            var files = BuildKitFiles(
                    kit,
                    kit.SourceNamespace,
                    targetNamespace,
                    rewriteNamespace,
                    destination,
                    inferDestinationNamespace)
                .ToArray();

            return new TransferPlan(name, TransferPlanKind.Kit, files);
        }

        if (StorageManager.ItemExists(name))
        {
            return new TransferPlan(
                name,
                TransferPlanKind.Item,
                BuildItemFiles(
                    name,
                    ".",
                    rewriteNamespace,
                    targetNamespace,
                    destination,
                    inferDestinationNamespace,
                    inferDestinationNamespace).ToArray());
        }

        throw new DirectoryNotFoundException($"No storage item or bundle named '{name}' exists.");
    }

    public static TransferReport Pull(
        TransferPlan plan,
        bool overwrite,
        Action<TransferProgress>? progress = null)
    {
        ValidatePlan(plan, overwrite);

        var preparedFiles = plan.Files
            .Select(PrepareFile)
            .ToArray();

        var completed = 0;
        var rewrittenFiles = 0;
        var rewrittenReferences = 0;

        foreach (var prepared in preparedFiles)
        {
            var file = prepared.File;
            Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);

            if (prepared.Content is not null)
            {
                File.WriteAllText(file.DestinationPath, prepared.Content);
                if (prepared.Changed)
                    rewrittenFiles++;

                rewrittenReferences += prepared.ReferenceCount;
            }
            else
                File.Copy(file.SourcePath, file.DestinationPath, overwrite);

            completed++;
            progress?.Invoke(new TransferProgress(
                completed,
                plan.Files.Count,
                file.DestinationPath));
        }

        return new TransferReport(
            completed,
            rewrittenFiles,
            rewrittenReferences);
    }

    private static IEnumerable<TransferFile> BuildItemFiles(
        string itemName,
        string destination,
        bool rewriteNamespace,
        string? targetNamespace,
        string loadDestination,
        bool inferDestinationNamespace,
        bool inferTargetNamespace)
    {
        var manifest = StorageManager.Read(itemName);
        var itemPath = StorageManager.GetItemPath(itemName);
        var normalizedLoadDestination = NormalizeRelativeDestination(loadDestination);
        var destinationRoot = DirectoryManager.ResolvePath(Path.Combine(normalizedLoadDestination, destination));
        var effectiveTargetNamespace = inferDestinationNamespace && inferTargetNamespace
            ? GetDestinationNamespace(targetNamespace, normalizedLoadDestination, destination)
            : targetNamespace;
        var sourceFiles = manifest.Files
            .Select(relativeFile => Path.GetFullPath(Path.Combine(
                itemPath,
                relativeFile.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        var effectiveSourceNamespace = manifest.SourceNamespace ??
            (rewriteNamespace ? NamespaceDetector.Detect(sourceFiles) : null);

        for (var index = 0; index < manifest.Files.Count; index++)
        {
            var relativeFile = manifest.Files[index];
            var relativePath = relativeFile.Replace('/', Path.DirectorySeparatorChar);
            var sourcePath = sourceFiles[index];
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));

            yield return new TransferFile(
                sourcePath,
                destinationPath,
                effectiveSourceNamespace,
                effectiveTargetNamespace,
                rewriteNamespace);
        }
    }

    private static IEnumerable<TransferFile> BuildKitFiles(
        KitManifest kit,
        string? sourceNamespace,
        string? targetNamespace,
        bool rewriteNamespace,
        string loadDestination,
        bool inferDestinationNamespace)
    {
        var kitPath = KitManager.GetKitPath(kit.Name);
        var normalizedLoadDestination = NormalizeRelativeDestination(loadDestination);
        var destinationRoot = DirectoryManager.ResolvePath(normalizedLoadDestination);
        var effectiveTargetNamespace = inferDestinationNamespace
            ? GetDestinationNamespace(targetNamespace, normalizedLoadDestination, ".")
            : targetNamespace;
        var sourceFiles = kit.Files
            .Select(relativeFile => Path.GetFullPath(Path.Combine(
                kitPath,
                relativeFile.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        var effectiveSourceNamespace = sourceNamespace ??
            (rewriteNamespace ? NamespaceDetector.Detect(sourceFiles) : null);

        for (var index = 0; index < kit.Files.Count; index++)
        {
            var relativeFile = kit.Files[index];
            var relativePath = relativeFile.Replace('/', Path.DirectorySeparatorChar);
            var sourcePath = sourceFiles[index];
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));

            yield return new TransferFile(
                sourcePath,
                destinationPath,
                effectiveSourceNamespace,
                effectiveTargetNamespace,
                rewriteNamespace);
        }
    }

    private static PreparedTransferFile PrepareFile(TransferFile file)
    {
        if (!ShouldRewrite(file))
            return new PreparedTransferFile(file, null, false, 0);

        var result = NamespaceRewriter.RewriteFile(
            file.SourcePath,
            file.SourceNamespace!,
            file.TargetNamespace!);

        if (!result.Succeeded)
        {
            throw new InvalidDataException(
                result.Message ?? $"Could not rewrite namespace in '{file.SourcePath}'.");
        }

        return new PreparedTransferFile(
            file,
            result.Content,
            result.Changed,
            result.Changes.Count);
    }

    private static string? GetDestinationNamespace(
        string? baseNamespace,
        string loadDestination,
        string itemDestination)
    {
        if (string.IsNullOrWhiteSpace(baseNamespace))
            return baseNamespace;

        var normalizedLoadDestination = NormalizeRelativeDestination(loadDestination);
        var normalizedItemDestination = NormalizeRelativeDestination(itemDestination);
        var effectiveDestination = normalizedItemDestination == "."
            ? normalizedLoadDestination
            : normalizedItemDestination;

        if (effectiveDestination == ".")
            return baseNamespace;

        var folderName = effectiveDestination
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault();

        if (string.IsNullOrWhiteSpace(folderName) ||
            !SyntaxFacts.IsValidIdentifier(folderName))
        {
            return baseNamespace;
        }

        var namespaceSegment = char.ToUpperInvariant(folderName[0]) + folderName[1..];
        return $"{baseNamespace}.{namespaceSegment}";
    }

    private static string NormalizeRelativeDestination(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        if (destination == ".")
            return destination;

        var normalized = destination
            .Replace('\\', '/')
            .Trim('/');

        if (Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new ArgumentException(
                "Load destinations must be relative paths inside the working directory.",
                nameof(destination));
        }

        return normalized;
    }

    private static void ValidatePlan(TransferPlan plan, bool overwrite)
    {
        var duplicates = plan.Files
            .GroupBy(file => file.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicates is not null)
            throw new IOException($"Multiple files would be written to '{duplicates.Key}'.");

        foreach (var file in plan.Files)
        {
            if (!File.Exists(file.SourcePath))
                throw new FileNotFoundException($"Stored file '{file.SourcePath}' is missing.", file.SourcePath);

            if (!overwrite && File.Exists(file.DestinationPath))
                throw new IOException(
                    $"The destination '{file.DestinationPath}' already exists. Use --overwrite to replace it.");
        }
    }

    private static bool ShouldRewrite(TransferFile file)
    {
        return file.RewriteNamespace &&
               !string.IsNullOrWhiteSpace(file.SourceNamespace) &&
               !string.IsNullOrWhiteSpace(file.TargetNamespace) &&
               IsNamespaceAwareFile(file.SourcePath);
    }

    private static bool IsNamespaceAwareFile(string path)
    {
        return Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               Path.GetExtension(path).Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               Path.GetExtension(path).Equals(".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PreparedTransferFile(
        TransferFile File,
        string? Content,
        bool Changed,
        int ReferenceCount);
}
