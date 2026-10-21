namespace Lasagna.Models;

internal enum PackageReferenceStatus
{
    PotentiallyUnnecessary,
    Used,
    Review
}

internal sealed record PackageReferenceEvidence(
    string FilePath,
    int Line,
    string Reason);

internal sealed record PackageReferenceReport(
    string ProjectName,
    string ProjectPath,
    string PackageId,
    string? Version,
    PackageReferenceStatus Status,
    string Reason,
    IReadOnlyList<PackageReferenceEvidence> Evidence,
    bool HasCompileAssets);

internal sealed record PackageReferencePlan(
    string RootPath,
    int ProjectsScanned,
    IReadOnlyList<PackageReferenceReport> References,
    IReadOnlyList<string> Issues)
{
    internal IReadOnlyList<PackageReferenceReport> PotentiallyUnnecessary =>
        References
            .Where(reference => reference.Status == PackageReferenceStatus.PotentiallyUnnecessary)
            .ToArray();

    internal IReadOnlyList<PackageReferenceReport> Used =>
        References
            .Where(reference => reference.Status == PackageReferenceStatus.Used)
            .ToArray();

    internal IReadOnlyList<PackageReferenceReport> Review =>
        References
            .Where(reference => reference.Status == PackageReferenceStatus.Review)
            .ToArray();
}
