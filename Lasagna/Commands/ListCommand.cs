using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ListCommand : LasagnaCommand<ListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandOption("--items")]
        [Description("Show stored items only.")]
        public bool ItemsOnly { get; init; }

        [CommandOption("--bundles")]
        [Description("Show bundles only.")]
        public bool BundlesOnly { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.ItemsOnly && settings.BundlesOnly)
            throw new InvalidOperationException("Choose either --items or --bundles, not both.");

        var items = settings.BundlesOnly
            ? []
            : StorageManager.List();
        var bundles = settings.ItemsOnly
            ? []
            : BundleManager.List();

        if (items.Count == 0 && bundles.Count == 0)
        {
            ConsoleUi.WriteInfo("No stored items or bundles yet. Use 'lasagna save' to add one.");
            return 0;
        }

        var rows = new List<IReadOnlyList<string>>();

        foreach (var item in items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add([
                "item",
                item.Name,
                $"{item.Files.Count} file{(item.Files.Count == 1 ? string.Empty : "s")}",
                item.SourceNamespace ?? "-"]);
        }

        foreach (var bundle in bundles.OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add([
                "bundle",
                bundle.Name,
                $"{bundle.Items.Count} item{(bundle.Items.Count == 1 ? string.Empty : "s")}",
                "-"]);
        }

        ConsoleUi.WriteTable(
            "Lasagna pantry",
            ["Type", "Name", "Contents", "Namespace"],
            rows);
        return 0;
    }
}
