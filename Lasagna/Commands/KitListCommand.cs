using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class KitListCommand : LasagnaCommand<KitListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Optionally show the files inside one kit.")]
        public string? Name { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Name))
        {
            WriteKitFiles(KitManager.Read(settings.Name));
            return 0;
        }

        var kits = KitManager.List();

        if (kits.Count == 0)
        {
            ConsoleUi.WriteInfo("No kits yet. Use 'lasagna kit create' to capture a directory.");
            return 0;
        }

        var rows = kits
            .OrderBy(kit => kit.Name, StringComparer.OrdinalIgnoreCase)
            .Select(kit => (IReadOnlyList<string>)[
                kit.Name,
                $"{kit.Files.Count} file{(kit.Files.Count == 1 ? string.Empty : "s")}",
                kit.SourceNamespace ?? "-"])
            .ToArray();

        ConsoleUi.WriteTable("Kits", ["Name", "Contents", "Namespace"], rows);
        return 0;
    }

    private static void WriteKitFiles(KitManifest kit)
    {
        var rows = kit.Files
            .Select(file => (IReadOnlyList<string>)[file])
            .ToArray();

        ConsoleUi.WriteTable(
            $"Kit: {kit.Name}",
            ["File"],
            rows);
    }
}
