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
        [Description("Override the namespace automatically detected from the source project and files.")]
        public string? SourceNamespace { get; init; }

        [CommandOption("--no-companions")]
        [Description("Skip the Razor companion file prompt.")]
        public bool NoCompanions { get; init; }

    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Files.Length == 0)
            throw new InvalidOperationException("Provide at least one file or directory to save.");

        var files = DirectoryManager.CollectFiles(settings.Files, includeRazorCompanions: false);
        var companionFiles = settings.NoCompanions
            ? []
            : SelectCompanions(DirectoryManager.FindRazorCompanions(settings.Files));

        files = files
            .Concat(companionFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sources = DirectoryManager.CollectSources(
                settings.Files,
                includeRazorCompanions: false)
            .Concat(companionFiles)
            .ToArray();

        if (files.Count == 0)
            throw new InvalidOperationException("No files were found to save.");

        var sourceNamespace = settings.SourceNamespace ??
            NamespaceDetector.Detect(
                files,
                DirectoryManager.GetProjectNamespace(files[0]));

        if (sourceNamespace is null)
        {
            ConsoleUi.WriteWarning(
                "No source namespace could be detected; stored files will load without " +
                "namespace rewriting unless overridden.");
        }

        ItemManifest? manifest = null;

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start($"[yellow]Saving {Markup.Escape(settings.Name)}...[/]", _ =>
            {
                manifest = StorageManager.Create(
                    settings.Name,
                    sources,
                    sourceNamespace);
            });

        ConsoleUi.WriteSummary(
            "Saved item",
            ("Name", manifest!.Name),
            ("Files", manifest.Files.Count.ToString()),
            ("Storage", StorageManager.GetItemPath(manifest.Name)));

        ConsoleUi.WriteSuccess($"Saved '{manifest.Name}'.");
        return 0;
    }

    private static IReadOnlyList<string> SelectCompanions(IReadOnlyList<string> companionFiles)
    {
        if (companionFiles.Count == 0)
            return [];

        if (!ConsoleUi.SupportsInteractivePrompts)
        {
            ConsoleUi.WriteInfo(
                $"Including {companionFiles.Count} discovered Razor companion " +
                $"file{(companionFiles.Count == 1 ? string.Empty : "s")}.");
            return companionFiles;
        }

        var workingDirectory = DirectoryManager.GetWorkingDirectory();
        var prompt = new MultiSelectionPrompt<string>()
            .Title("Select [yellow]Razor companion files[/] to include:")
            .InstructionsText("[grey](Press [blue]<space>[/] to toggle, [green]<enter>[/] to save)[/]")
            .NotRequired()
            .PageSize(10);

        prompt.Converter = path => Markup.Escape(
            Path.GetRelativePath(workingDirectory, path));
        prompt.AddChoices(companionFiles);

        foreach (var companionFile in companionFiles)
            prompt.Select(companionFile);

        return AnsiConsole.Prompt(prompt);
    }
}
