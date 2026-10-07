using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleListCommand : LasagnaCommand<BundleListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Optionally show the item references inside one bundle.")]
        public string? Name { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Name))
        {
            WriteBundleItems(BundleManager.Read(settings.Name));
            return 0;
        }

        var bundles = BundleManager.List();

        if (bundles.Count == 0)
        {
            ConsoleUi.WriteInfo("No bundles yet. Use 'lasagna bundle create' to add one.");
            return 0;
        }

        var rows = bundles
            .OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
            .Select(bundle => (IReadOnlyList<string>)[
                bundle.Name,
                $"{bundle.Items.Count} item{(bundle.Items.Count == 1 ? string.Empty : "s")}"])
            .ToArray();

        ConsoleUi.WriteTable("Bundles", ["Name", "Contents"], rows);
        return 0;
    }

    private static void WriteBundleItems(BundleManifest bundle)
    {
        var rows = bundle.Items
            .Select(item => (IReadOnlyList<string>)[
                item.ItemName,
                item.Destination,
                item.RewriteNamespace ? "yes" : "no",
                item.TargetNamespace ?? "auto"])
            .ToArray();

        ConsoleUi.WriteTable(
            $"Bundle: {bundle.Name}",
            ["Item", "Destination", "Rewrite namespace", "Target namespace"],
            rows);
    }
}
