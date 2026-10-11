using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ItemRemoveCommand : LasagnaCommand<ItemRemoveCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<item>")]
        [Description("The stored item to update.")]
        public string Item { get; init; } = string.Empty;

        [CommandArgument(1, "<files>")]
        [Description("One or more relative file or directory paths inside the item.")]
        public string[] Files { get; init; } = [];
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Files.Length == 0)
            throw new InvalidOperationException("Provide at least one file or directory path to remove.");

        var previousFileCount = StorageManager.Read(settings.Item).Files.Count;
        var manifest = StorageManager.RemoveFiles(settings.Item, settings.Files);
        var removedFileCount = previousFileCount - manifest.Files.Count;

        ConsoleUi.WriteSummary(
            "Updated item",
            ("Name", manifest.Name),
            ("Files", manifest.Files.Count.ToString()));
        ConsoleUi.WriteSuccess(
            $"Removed {removedFileCount} file{(removedFileCount == 1 ? string.Empty : "s")} from '{manifest.Name}'.");
        return 0;
    }
}
