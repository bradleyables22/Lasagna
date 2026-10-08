
namespace Lasagna.Models;

internal sealed record TransferPlan(
	string Name,
	bool IsBundle,
	IReadOnlyList<TransferFile> Files);
