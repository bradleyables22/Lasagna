using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleCreateCommand : LasagnaCommand<BundleCreateCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name to give the bundle.")]
        public string Name { get; init; } = string.Empty;

        [CommandArgument(1, "<items>")]
        [Description("One or more stored item names to include.")]
        public string[] Items { get; init; } = [];

        [CommandOption("--destination <PATH>")]
        [Description("Place all initial items under this relative destination path.")]
        public string Destination { get; init; } = ".";

        [CommandOption("--namespace <NAMESPACE>")]
        [Description("Use this target namespace when the bundle is loaded.")]
        public string? TargetNamespace { get; init; }

        [CommandOption("--no-namespace-rewrite")]
        [Description("Keep namespaces unchanged when the bundle is loaded.")]
        public bool NoNamespaceRewrite { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Items.Length == 0)
            throw new InvalidOperationException("Provide at least one stored item for the bundle.");

        var manifest = BundleManager.Create(
            settings.Name,
            settings.Items.Select(item => new BundleItemReference
            {
                ItemName = item,
                Destination = settings.Destination,
                RewriteNamespace = !settings.NoNamespaceRewrite,
                TargetNamespace = settings.TargetNamespace
            }));

        ConsoleUi.WriteSummary(
            "Created bundle",
            ("Name", manifest.Name),
            ("Items", manifest.Items.Count.ToString()),
            ("Destination", settings.Destination));

        ConsoleUi.WriteSuccess($"Created bundle '{manifest.Name}'.");
        return 0;
    }
}
