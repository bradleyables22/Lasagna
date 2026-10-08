
namespace Lasagna.Models;

internal sealed record TransferFile(
	string SourcePath,
	string DestinationPath,
	string? SourceNamespace,
	string? TargetNamespace,
	bool RewriteNamespace);
