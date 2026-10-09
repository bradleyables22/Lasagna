using System.ComponentModel;
using System.Diagnostics;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class InspectCommand : LasagnaCommand<InspectCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The item or bundle whose files to open in the browser.")]
        public string Name { get; init; } = string.Empty;
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var files = GetFiles(settings.Name);

        if (files.Count == 0)
        {
            ConsoleUi.WriteInfo($"'{settings.Name}' contains no files to inspect.");
            return 0;
        }

        var opened = 0;

        foreach (var file in files)
        {
            var url = new Uri(Path.GetFullPath(file)).AbsoluteUri;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            ConsoleUi.WriteProgress("Opening", ++opened, files.Count, file);
        }

        ConsoleUi.WriteSuccess(
            $"Opened {files.Count} file{(files.Count == 1 ? string.Empty : "s")} " +
            $"from '{settings.Name}'.");
        return 0;
    }

    private static IReadOnlyList<string> GetFiles(string name)
    {
        if (BundleManager.Exists(name))
        {
            return BundleManager.Read(name)
                .Items
                .SelectMany(item => StorageManager.ReadFiles(item.ItemName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (StorageManager.ItemExists(name))
        {
            return StorageManager.ReadFiles(name)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        throw new DirectoryNotFoundException($"No stored item or bundle named '{name}' exists.");
    }
}
