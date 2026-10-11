using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BrowseItemCommand : LasagnaCommand<BrowseItemCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The stored item whose files to open in browser tabs.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var files = StorageManager.ReadFiles(settings.Name)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return BrowserFileOpener.Open(settings.Name, files);
    }
}
