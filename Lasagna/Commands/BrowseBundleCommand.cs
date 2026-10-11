using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BrowseBundleCommand : LasagnaCommand<BrowseBundleCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The bundle whose stored files to open in browser tabs.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var files = BundleManager.Read(settings.Name)
            .Items
            .SelectMany(item => StorageManager.ReadFiles(item.ItemName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return BrowserFileOpener.Open(settings.Name, files);
    }
}
