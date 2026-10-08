using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class TransferManager
{
    public static TransferPlan BuildPlan(string name, string? targetNamespace, bool rewriteNamespace, string destination = ".")
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
                    destination))
                .ToArray();

            return new TransferPlan(name, true, files);
        }

        if (StorageManager.ItemExists(name))
        {
            return new TransferPlan(
                name,
                false,
                BuildItemFiles(name, ".", rewriteNamespace, targetNamespace, destination).ToArray());
        }

        throw new DirectoryNotFoundException($"No storage item or bundle named '{name}' exists.");
    }

    public static void Pull(TransferPlan plan, bool overwrite, Action<TransferProgress>? progress = null)
    {
        ValidatePlan(plan, overwrite);

        var completed = 0;

        foreach (var file in plan.Files)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);

            if (ShouldRewrite(file))
            {
                var result = NamespaceRewriter.RewriteFile(
                    file.SourcePath,
                    file.SourceNamespace!,
                    file.TargetNamespace!);

                if (!result.Succeeded)
                    throw new InvalidDataException(
                        result.Message ?? $"Could not rewrite namespace in '{file.SourcePath}'.");

                File.WriteAllText(file.DestinationPath, result.Content);
            }
            else
                File.Copy(file.SourcePath, file.DestinationPath, overwrite);

            completed++;
            progress?.Invoke(new TransferProgress(
                completed,
                plan.Files.Count,
                file.DestinationPath));
        }
    }

    private static IEnumerable<TransferFile> BuildItemFiles(string itemName, string destination, bool rewriteNamespace,string? targetNamespace, string loadDestination)
    {
        var manifest = StorageManager.Read(itemName);
        var itemPath = StorageManager.GetItemPath(itemName);
        var normalizedLoadDestination = NormalizeRelativeDestination(loadDestination);
        var destinationRoot = DirectoryManager.ResolvePath(Path.Combine(normalizedLoadDestination, destination));

        foreach (var relativeFile in manifest.Files)
        {
            var relativePath = relativeFile.Replace('/', Path.DirectorySeparatorChar);
            var sourcePath = DirectoryManager.ResolvePath(Path.Combine(itemPath, relativePath));
            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));

            yield return new TransferFile(
                sourcePath,
                destinationPath,
                manifest.SourceNamespace,
                targetNamespace,
                rewriteNamespace);
        }
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
}
