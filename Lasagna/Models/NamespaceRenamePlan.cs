namespace Lasagna.Models;

internal sealed record NamespaceRenamePlan(
    string RootPath,
    string SourceNamespace,
    string TargetNamespace,
    IReadOnlyList<NamespaceRenameFile> Files)
{
    public IReadOnlyList<NamespaceRenameFile> ChangedFiles =>
        Files
            .Where(file => file.Changed)
            .ToArray();

    public IReadOnlyList<NamespaceRenameFile> FailedFiles =>
        Files
            .Where(file => !file.Succeeded)
            .ToArray();

    public int ReferencesRewritten =>
        ChangedFiles.Sum(file => file.Changes.Count);
}
