using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ItemAddCommand : LasagnaCommand<ItemAddCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<item>")]
        [Description("The stored item to update.")]
        public string Item { get; init; } = string.Empty;

        [CommandArgument(1, "<files>")]
        [Description("One or more files or directories to add.")]
        public string[] Files { get; init; } = [];

        [CommandOption("--no-companions")]
        [Description("Skip automatic Razor companion file discovery.")]
        public bool NoCompanions { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Files.Length == 0)
            throw new InvalidOperationException("Provide at least one file or directory to add.");

        var sources = DirectoryManager.CollectSources(
            settings.Files,
            includeRazorCompanions: !settings.NoCompanions);
        var previousFileCount = StorageManager.Read(settings.Item).Files.Count;

        ConsoleUi.WriteInfo($"Adding files to item '{settings.Item}'...");
        var manifest = StorageManager.AddFiles(settings.Item, sources);
        var addedFileCount = manifest.Files.Count - previousFileCount;

        ConsoleUi.WriteSummary(
            "Updated item",
            ("Name", manifest.Name),
            ("Files", manifest.Files.Count.ToString()));
        ConsoleUi.WriteSuccess(
            $"Added {addedFileCount} file{(addedFileCount == 1 ? string.Empty : "s")} to '{manifest.Name}'.");
        return 0;
    }
}
