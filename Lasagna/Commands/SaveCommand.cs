using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class SaveCommand : LasagnaCommand<SaveCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name to give the stored item.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<files>")]
        [Description("One or more files or directories to save.")]
        public string[] Files { get; init; } = [];

        [CommandOption("--source-namespace <NAMESPACE>")]
        [Description("The namespace found in the source files.")]
        public string? SourceNamespace { get; init; }

        [CommandOption("--no-companions")]
        [Description("Do not automatically include Razor code-behind, CSS, or JavaScript companions.")]
        public bool NoCompanions { get; init; }

    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Files.Length == 0)
            throw new InvalidOperationException("Provide at least one file or directory to save.");

        var files = DirectoryManager.CollectFiles(settings.Files, !settings.NoCompanions);
        var sources = DirectoryManager.CollectSources(settings.Files, !settings.NoCompanions);

        if (files.Count == 0)
            throw new InvalidOperationException("No files were found to save.");

        ItemManifest? manifest = null;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start($"[yellow]Saving {Markup.Escape(settings.Name)}...[/]", _ =>
            {
                manifest = StorageManager.Create(
                    settings.Name,
                    sources,
                    settings.SourceNamespace);
            });

        ConsoleUi.WriteSummary(
            "Saved item",
            ("Name", manifest!.Name),
            ("Files", manifest.Files.Count.ToString()),
            ("Storage", StorageManager.GetItemPath(manifest.Name)));

        ConsoleUi.WriteSuccess($"Saved '{manifest.Name}'.");
        return 0;
    }
}
