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

        [CommandOption("--kits")]
        [Description("Show kits only.")]
        public bool KitsOnly { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var filters = new[] { settings.ItemsOnly, settings.BundlesOnly, settings.KitsOnly }
            .Count(value => value);

        if (filters > 1)
            throw new InvalidOperationException("Choose only one of --items, --bundles, or --kits.");

        var items = settings.BundlesOnly || settings.KitsOnly
            ? []
            : StorageManager.List();
        var bundles = settings.ItemsOnly || settings.KitsOnly
            ? []
            : BundleManager.List();
        var kits = settings.ItemsOnly || settings.BundlesOnly
            ? []
            : KitManager.List();

        if (items.Count == 0 && bundles.Count == 0 && kits.Count == 0)
        {
            ConsoleUi.WriteInfo("No stored items, bundles, or kits yet. Use 'lasagna item create' or 'lasagna kit create' to add one.");
            return 0;
        }

        if (items.Count > 0)
            WriteItems(items);

        if (bundles.Count > 0)
            WriteBundles(bundles);

        if (kits.Count > 0)
            WriteKits(kits);

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

    private static void WriteKits(IReadOnlyList<KitManifest> kits)
    {
        var rows = kits
            .OrderBy(kit => kit.Name, StringComparer.OrdinalIgnoreCase)
            .Select(kit => (IReadOnlyList<string>)[
                kit.Name,
                $"{kit.Files.Count} file{(kit.Files.Count == 1 ? string.Empty : "s")}",
                kit.SourceNamespace ?? "-"])
            .ToArray();

        ConsoleUi.WriteTable("Kits", ["Name", "Contents", "Namespace"], rows);
    }
}
