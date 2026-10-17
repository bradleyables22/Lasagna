using Lasagna.Enums;

namespace Lasagna.Models;

internal sealed record NamespaceRenameFile(
    string FilePath,
    string OriginalContent,
    NamespaceRewriteResult Rewrite)
{
    public bool Changed => Rewrite.Changed;

    public bool Succeeded => Rewrite.Succeeded;

    public string Content => Rewrite.Content;

    public IReadOnlyList<string> Changes => Rewrite.Changes;

    public string? Message => Rewrite.Message;

    public NamespaceRewriteStatus Status => Rewrite.Status;
}
