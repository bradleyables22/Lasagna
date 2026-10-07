using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ClearCommand : LasagnaCommand<ClearCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandOption("--force")]
        [Description("Clear the pantry without asking for confirmation.")]
        public bool Force { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var bundles = BundleManager.List();
        var items = StorageManager.List();
        var total = bundles.Count + items.Count;

        if (total == 0)
        {
            ConsoleUi.WriteInfo("The Lasagna pantry is already empty.");
            return 0;
        }

        var table = new Table
        {
            Border = TableBorder.Rounded
        };
        table.BorderColor(Color.Red);
        table.AddColumn("[red]Type[/]");
        table.AddColumn("[red]Name[/]");

        foreach (var bundle in bundles)
            table.AddRow("bundle", Markup.Escape(bundle.Name));

        foreach (var item in items)
            table.AddRow("item", Markup.Escape(item.Name));

        AnsiConsole.Write(new Panel(table)
            .Header("[red]Everything in the pantry[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Red));

        if (!settings.Force && !AnsiConsole.Confirm("Remove everything listed above?"))
        {
            ConsoleUi.WriteInfo("Nothing was removed.");
            return 0;
        }

        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
            .Start(progressContext =>
            {
                var task = progressContext.AddTask("[red]Clearing pantry[/]", maxValue: total);

                foreach (var bundle in bundles)
                {
                    BundleManager.Delete(bundle.Name);
                    task.Increment(1);
                }

                foreach (var item in items)
                {
                    StorageManager.Delete(item.Name);
                    task.Increment(1);
                }
            });

        ConsoleUi.WriteSuccess($"Removed {total} stored entr{(total == 1 ? "y" : "ies")}.");
        return 0;
    }
}
