using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
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

        if (items.Count > 0)
            WriteItems(items);

        if (bundles.Count > 0)
            WriteBundles(bundles);

        return 0;
    }

    private static void WriteItems(IReadOnlyList<ItemManifest> items)
    {
        var rows = items
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => (IReadOnlyList<string>)[
                item.Name,
                $"{item.Files.Count} file{(item.Files.Count == 1 ? string.Empty : "s")}",
                item.SourceNamespace ?? "-"])
            .ToArray();

        ConsoleUi.WriteTable("Items", ["Name", "Contents", "Namespace"], rows);
    }

    private static void WriteBundles(IReadOnlyList<BundleManifest> bundles)
    {
        var rows = bundles
            .OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
            .Select(bundle => (IReadOnlyList<string>)[
                bundle.Name,
                $"{bundle.Items.Count} item{(bundle.Items.Count == 1 ? string.Empty : "s")}"])
            .ToArray();

        ConsoleUi.WriteTable("Bundles", ["Name", "Contents"], rows);
    }
}
