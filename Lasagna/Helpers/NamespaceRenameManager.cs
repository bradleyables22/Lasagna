using Lasagna.Models;

namespace Lasagna.Helpers;

internal static class NamespaceRenameManager
{
    private static readonly HashSet<string> ExcludedDirectories = new(
        [".git", ".vs", "bin", "obj", "TestResults", ".idea"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NamespaceAwareExtensions = new(
        [".cs", ".razor", ".cshtml"],
        StringComparer.OrdinalIgnoreCase);

    public static NamespaceRenamePlan BuildPlan(
        string rootPath,
        string sourceNamespace,
        string targetNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetNamespace);

        _ = NamespaceMap.Create(sourceNamespace, targetNamespace);

        var resolvedRoot = DirectoryManager.GetDirectoryPath(rootPath);
        var files = EnumerateNamespaceFiles(resolvedRoot).ToArray();
        var results = files
            .Select(file =>
            {
                var original = File.ReadAllText(file);
                var rewrite = NamespaceRewriter.RewriteContent(
                    file,
                    original,
                    sourceNamespace,
                    targetNamespace);

                return new NamespaceRenameFile(file, original, rewrite);
            })
            .ToArray();

        return new NamespaceRenamePlan(
            resolvedRoot,
            sourceNamespace.Trim(),
            targetNamespace.Trim(),
            results);
    }

    public static void Apply(NamespaceRenamePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (plan.FailedFiles.Count > 0)
        {
            var failures = string.Join(
                Environment.NewLine,
                plan.FailedFiles.Select(file =>
                    $"- {file.FilePath}: {file.Message ?? file.Status.ToString()}"));

            throw new InvalidDataException(
                $"Namespace rename stopped before writing because {plan.FailedFiles.Count} file(s) could not be safely rewritten.{Environment.NewLine}{failures}");
        }

        foreach (var file in plan.ChangedFiles)
            File.WriteAllText(file.FilePath, file.Content);
    }

    private static IEnumerable<string> EnumerateNamespaceFiles(string rootPath)
    {
        var root = new DirectoryInfo(rootPath);

        if (IsReparsePoint(root))
            yield break;

        foreach (var file in root.EnumerateFiles())
        {
            if (NamespaceAwareExtensions.Contains(file.Extension) &&
                !IsGeneratedFile(file.Name))
            {
                yield return file.FullName;
            }
        }

        foreach (var directory in root.EnumerateDirectories())
        {
            if (ExcludedDirectories.Contains(directory.Name) ||
                IsReparsePoint(directory))
            {
                continue;
            }

            foreach (var file in EnumerateNamespaceFiles(directory.FullName))
                yield return file;
        }
    }

    private static bool IsGeneratedFile(string fileName)
    {
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase) ||
               fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(FileSystemInfo entry)
    {
        return entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
