namespace Lasagna.Models;

internal enum KitReplacementFileKind
{
    KitFile,
    ProjectSource,
    ProjectFile
}

internal sealed record KitReplacementFile(
    string SourcePath,
    string DestinationPath,
    byte[] Content,
    byte[]? OriginalContent,
    KitReplacementFileKind Kind,
    bool NamespaceRewritten,
    int ReferencesRewritten)
{
    internal bool AlreadyExists => OriginalContent is not null;
}

internal sealed record KitReplacementPlan(
    string PackageId,
    string KitName,
    string ProjectPath,
    string DestinationPath,
    string? SourceNamespace,
    string? TargetNamespace,
    IReadOnlyList<KitReplacementFile> Files,
    int PackageReferencesRemoved)
{
    internal int KitFileCount => Files.Count(file => file.Kind == KitReplacementFileKind.KitFile);

    internal int ExistingProjectFileCount => Files.Count(file => file.Kind == KitReplacementFileKind.ProjectSource);

    internal int NamespaceReferencesRewritten => Files.Sum(file => file.ReferencesRewritten);

    internal int ExistingFileCount => Files.Count(file => file.AlreadyExists);
}

internal sealed record KitReplacementReport(
    int FilesWritten,
    int KitFilesWritten,
    int ProjectFilesRewritten,
    int NamespaceReferencesRewritten,
    int PackageReferencesRemoved);
