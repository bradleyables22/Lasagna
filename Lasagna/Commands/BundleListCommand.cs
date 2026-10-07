using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleListCommand : LasagnaCommand<BundleListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var bundles = BundleManager.List();

        if (bundles.Count == 0)
        {
            ConsoleUi.WriteInfo("No bundles yet. Use 'lasagna bundle create' to add one.");
            return 0;
        }

        var rows = new List<IReadOnlyList<string>>();

        foreach (var bundle in bundles.OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var item in bundle.Items)
            {
                rows.Add([
                    bundle.Name,
                    item.ItemName,
                    item.Destination,
                    item.RewriteNamespace ? "yes" : "no",
                    item.TargetNamespace ?? "auto"]);
            }
        }

        ConsoleUi.WriteTable(
            "Bundles",
            ["Bundle", "Item", "Destination", "Rewrite namespace", "Target namespace"],
            rows);
        return 0;
    }
}
