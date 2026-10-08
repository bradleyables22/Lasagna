using Lasagna.Enums;

namespace Lasagna.Models;

internal sealed record NamespaceRewriteResult(
string FilePath,
string Content,
NamespaceRewriteStatus Status,
IReadOnlyList<string> Changes,
string? Message = null)
{
	public bool Changed => Status == NamespaceRewriteStatus.Rewritten;

	public bool Succeeded => Status is
		NamespaceRewriteStatus.Unchanged or
		NamespaceRewriteStatus.Rewritten or
		NamespaceRewriteStatus.NotApplicable;
}
