using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ClearCommand : LasagnaCommand<ClearCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var bundles = BundleManager.List();
        var kits = KitManager.List();
        var items = StorageManager.List();
        var total = bundles.Count + kits.Count + items.Count;

        if (total == 0)
        {
            ConsoleUi.WriteInfo("The Lasagna pantry is already empty.");
            return 0;
        }

        var rows = new List<IReadOnlyList<string>>();

        foreach (var bundle in bundles)
            rows.Add(["bundle", bundle.Name]);

        foreach (var item in items)
            rows.Add(["item", item.Name]);

        foreach (var kit in kits)
            rows.Add(["kit", kit.Name]);

        ConsoleUi.WriteTable("Everything in the pantry", ["Type", "Name"], rows);

        var completed = 0;

        foreach (var bundle in bundles)
        {
            BundleManager.Delete(bundle.Name);
            ConsoleUi.WriteProgress("Removing", ++completed, total, bundle.Name);
        }

        foreach (var item in items)
        {
            StorageManager.Delete(item.Name);
            ConsoleUi.WriteProgress("Removing", ++completed, total, item.Name);
        }

        foreach (var kit in kits)
        {
            KitManager.Delete(kit.Name);
            ConsoleUi.WriteProgress("Removing", ++completed, total, kit.Name);
        }

        ConsoleUi.WriteSuccess($"Removed {total} stored entr{(total == 1 ? "y" : "ies")}.");
        return 0;
    }
}
