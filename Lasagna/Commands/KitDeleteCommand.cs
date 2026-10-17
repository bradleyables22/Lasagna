using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class KitDeleteCommand : LasagnaCommand<KitDeleteCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The kit to delete.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        KitManager.Delete(settings.Name);
        ConsoleUi.WriteSuccess($"Deleted kit '{settings.Name}'.");
        return 0;
    }
}
