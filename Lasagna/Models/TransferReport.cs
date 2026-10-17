namespace Lasagna.Models;

internal sealed record TransferReport(
    int FilesWritten,
    int FilesRewritten,
    int ReferencesRewritten);
