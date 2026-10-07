using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BundleAddCommand : LasagnaCommand<BundleAddCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<bundle>")]
        [Description("The bundle to update.")]
        public string Bundle { get; init; } = string.Empty;

        [CommandArgument(1, "<item>")]
        [Description("The stored item to add.")]
        public string Item { get; init; } = string.Empty;

        [CommandOption("--destination <PATH>")]
        [Description("The relative folder where this item will be copied.")]
        public string Destination { get; init; } = ".";

        [CommandOption("--namespace <NAMESPACE>")]
        [Description("Use this target namespace when the item is loaded.")]
        public string? TargetNamespace { get; init; }

        [CommandOption("--no-namespace-rewrite")]
        [Description("Keep this item's namespaces unchanged when loaded.")]
        public bool NoNamespaceRewrite { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var manifest = BundleManager.AddItem(
            settings.Bundle,
            settings.Item,
            settings.Destination,
            !settings.NoNamespaceRewrite,
            settings.TargetNamespace);

        ConsoleUi.WriteSuccess(
            $"Added '{settings.Item}' to bundle '{manifest.Name}' at '{settings.Destination}'.");
        return 0;
    }
}
