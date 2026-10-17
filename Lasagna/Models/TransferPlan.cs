
namespace Lasagna.Models;

internal enum TransferPlanKind
{
    Item,
    Bundle,
    Kit
}

internal sealed record TransferPlan(
	string Name,
	TransferPlanKind Kind,
	IReadOnlyList<TransferFile> Files);
