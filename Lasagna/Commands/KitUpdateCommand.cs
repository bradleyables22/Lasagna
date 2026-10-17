using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class KitUpdateCommand : LasagnaCommand<KitUpdateCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The kit to replace.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<directory>")]
        [Description("The directory whose contents should replace the kit.")]
        public string Directory { get; init; } = string.Empty;

        [CommandOption("--source-namespace <NAMESPACE>")]
        [Description("Override the namespace detected from the captured directory.")]
        public string? SourceNamespace { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var manifest = KitManager.Update(
            settings.Name,
            settings.Directory,
            settings.SourceNamespace);

        ConsoleUi.WriteSummary(
            "Updated kit",
            ("Name", manifest.Name),
            ("Contents", $"{manifest.Files.Count} file{(manifest.Files.Count == 1 ? string.Empty : "s")}"),
            ("Source namespace", manifest.SourceNamespace ?? "auto-detection unavailable"));

        ConsoleUi.WriteSuccess($"Updated kit '{manifest.Name}'.");
        return 0;
    }
}
