using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ItemListCommand : LasagnaCommand<ItemListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The stored item whose files should be listed.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var manifest = StorageManager.Read(settings.Name);

        if (manifest.Files.Count == 0)
        {
            ConsoleUi.WriteInfo($"Item '{manifest.Name}' contains no files.");
            return 0;
        }

        var rows = manifest.Files
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (IReadOnlyList<string>)[path])
            .ToArray();

        ConsoleUi.WriteTable($"Files in item '{manifest.Name}'", ["Path"], rows);
        return 0;
    }
}
