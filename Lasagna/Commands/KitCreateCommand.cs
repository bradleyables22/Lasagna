using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class KitCreateCommand : LasagnaCommand<KitCreateCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name to give the kit.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<directory>")]
        [Description("The directory whose contents should be captured.")]
        public string Directory { get; init; } = string.Empty;

        [CommandOption("--source-namespace <NAMESPACE>")]
        [Description("Override the namespace detected from the captured directory.")]
        public string? SourceNamespace { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var manifest = KitManager.Create(
            settings.Name,
            settings.Directory,
            settings.SourceNamespace);

        ConsoleUi.WriteSummary(
            "Created kit",
            ("Name", manifest.Name),
            ("Contents", $"{manifest.Files.Count} file{(manifest.Files.Count == 1 ? string.Empty : "s")}"),
            ("Source namespace", manifest.SourceNamespace ?? "auto-detection unavailable"));

        ConsoleUi.WriteSuccess($"Created kit '{manifest.Name}'.");
        return 0;
    }
}
