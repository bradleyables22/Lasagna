using Lasagna.Helpers;
using Spectre.Console;
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

        var table = new Table
        {
            Border = TableBorder.Rounded
        };
        table.BorderColor(Color.MediumPurple);
        table.AddColumn("[mediumpurple2]Bundle[/]");
        table.AddColumn("[mediumpurple2]Item[/]");
        table.AddColumn("[mediumpurple2]Destination[/]");
        table.AddColumn("[mediumpurple2]Rewrite namespace[/]");
        table.AddColumn("[mediumpurple2]Target namespace[/]");

        foreach (var bundle in bundles.OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var item in bundle.Items)
            {
                table.AddRow(
                    Markup.Escape(bundle.Name),
                    Markup.Escape(item.ItemName),
                    Markup.Escape(item.Destination),
                    item.RewriteNamespace ? "[green]yes[/]" : "[grey]no[/]",
                    Markup.Escape(item.TargetNamespace ?? "auto"));
            }
        }

        AnsiConsole.Write(table);
        return 0;
    }
}
