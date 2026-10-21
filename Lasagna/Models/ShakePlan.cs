namespace Lasagna.Models;

internal enum ShakeFileStatus
{
    Removable,
    Review,
    Referenced,
    Protected
}

internal sealed record ShakeReference(
    string FilePath,
    int Line,
    string Symbol,
    string Reason);

internal sealed record ShakeSymbolEvidence(
    string DisplayName,
    string FilePath,
    int DeclarationLine,
    string Accessibility,
    bool IsPublicApi,
    bool IsPartial,
    bool HasAttributes,
    IReadOnlyList<ShakeReference> References,
    IReadOnlyList<ShakeReference> IndirectHints);

internal sealed record ShakeFileReport(
    string FilePath,
    ShakeFileStatus Status,
    string Reason,
    IReadOnlyList<ShakeSymbolEvidence> Symbols)
{
    public int ReferenceCount => Symbols.Sum(symbol => symbol.References.Count);

    public int IndirectHintCount => Symbols.Sum(symbol => symbol.IndirectHints.Count);
}

internal sealed record ShakePlan(
    string RootPath,
    int FilesScanned,
    int TypesScanned,
    IReadOnlyList<ShakeFileReport> Files,
    IReadOnlyList<string> Issues)
{
    public IReadOnlyList<ShakeFileReport> RemovableFiles =>
        Files.Where(file => file.Status == ShakeFileStatus.Removable).ToArray();

    public IReadOnlyList<ShakeFileReport> ReviewFiles =>
        Files.Where(file => file.Status == ShakeFileStatus.Review).ToArray();

    public IReadOnlyList<ShakeFileReport> ReferencedFiles =>
        Files.Where(file => file.Status == ShakeFileStatus.Referenced).ToArray();

    public IReadOnlyList<ShakeFileReport> ProtectedFiles =>
        Files.Where(file => file.Status == ShakeFileStatus.Protected).ToArray();

    public int ReferenceCount => Files.Sum(file => file.ReferenceCount);

    public int IndirectHintCount => Files.Sum(file => file.IndirectHintCount);
}

internal sealed record ShakeApplyReport(
    int FilesRemoved);
