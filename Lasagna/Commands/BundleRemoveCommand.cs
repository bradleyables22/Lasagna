using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleRemoveCommand : LasagnaCommand<BundleRemoveCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<bundle>")]
        [Description("The bundle to update.")]
        public string Bundle { get; init; } = string.Empty;

        [CommandArgument(1, "<item>")]
        [Description("The stored item to remove.")]
        public string Item { get; init; } = string.Empty;

        [CommandOption("--destination <PATH>")]
        [Description("Only remove the item reference at this destination.")]
        public string? Destination { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var manifest = BundleManager.RemoveItem(
            settings.Bundle,
            settings.Item,
            settings.Destination);

        ConsoleUi.WriteSuccess(
            $"Removed '{settings.Item}' from bundle '{manifest.Name}'.");
        return 0;
    }
}
