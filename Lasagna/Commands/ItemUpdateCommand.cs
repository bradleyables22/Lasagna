using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ItemUpdateCommand : LasagnaCommand<ItemUpdateCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The stored item to replace.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<files>")]
        [Description("One or more files or directories to replace the item with.")]
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
            throw new InvalidOperationException("Provide at least one file or directory to update.");

        var files = DirectoryManager.CollectFiles(
            settings.Files,
            includeRazorCompanions: false);

        IReadOnlyList<string> companionFiles = settings.NoCompanions
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
            throw new InvalidOperationException("No files were found to update.");

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

        ConsoleUi.WriteInfo($"Updating '{settings.Name}'...");
        var manifest = StorageManager.Update(settings.Name, sources, sourceNamespace);

        ConsoleUi.WriteSummary(
            "Updated item",
            ("Name", manifest.Name),
            ("Files", manifest.Files.Count.ToString()));

        ConsoleUi.WriteSuccess($"Updated '{manifest.Name}'.");
        return 0;
    }

    private static IReadOnlyList<string> SelectCompanions(IReadOnlyList<string> companionFiles)
    {
        if (companionFiles.Count == 0)
            return [];

        return ConsoleUi.SelectFiles(
            "Related Razor files found. Enter file numbers to exclude:",
            companionFiles,
            DirectoryManager.GetWorkingDirectory());
    }
}
