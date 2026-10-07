using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleDeleteCommand : LasagnaCommand<BundleDeleteCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The bundle to delete.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--force")]
        [Description("Delete without asking for confirmation.")]
        public bool Force { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (!BundleManager.Exists(settings.Name))
            throw new DirectoryNotFoundException($"Bundle '{settings.Name}' does not exist.");

        if (!settings.Force && !AnsiConsole.Confirm(
                $"Delete bundle [red]'{Markup.Escape(settings.Name)}'[/]?"))
        {
            ConsoleUi.WriteInfo("Nothing was deleted.");
            return 0;
        }

        BundleManager.Delete(settings.Name);
        ConsoleUi.WriteSuccess($"Deleted bundle '{settings.Name}'.");
        return 0;
    }
}
