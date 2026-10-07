using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleDeleteCommand : LasagnaCommand<BundleDeleteCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The bundle to delete.")]
        public string Name { get; init; } = string.Empty;

    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (!BundleManager.Exists(settings.Name))
            throw new DirectoryNotFoundException($"Bundle '{settings.Name}' does not exist.");

        BundleManager.Delete(settings.Name);
        ConsoleUi.WriteSuccess($"Deleted bundle '{settings.Name}'.");
        return 0;
    }
}
