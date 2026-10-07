using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class RemoveCommand : LasagnaCommand<RemoveCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The item or bundle to remove.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--item")]
        [Description("Treat the name as an item.")]
        public bool Item { get; init; }

        [CommandOption("--bundle")]
        [Description("Treat the name as a bundle.")]
        public bool Bundle { get; init; }

        [CommandOption("--force")]
        [Description("Remove without asking for confirmation.")]
        public bool Force { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Item && settings.Bundle)
            throw new InvalidOperationException("Choose either --item or --bundle, not both.");

        var isBundle = settings.Bundle || (!settings.Item && BundleManager.Exists(settings.Name));
        var exists = isBundle
            ? BundleManager.Exists(settings.Name)
            : StorageManager.ItemExists(settings.Name);

        if (!exists)
            throw new DirectoryNotFoundException(
                $"No {(isBundle ? "bundle" : "item")} named '{settings.Name}' exists.");

        var type = isBundle ? "bundle" : "item";

        if (!settings.Force && !AnsiConsole.Confirm(
                $"Remove the [red]{Markup.Escape(type)} '{Markup.Escape(settings.Name)}'[/]?"))
        {
            ConsoleUi.WriteInfo("Nothing was removed.");
            return 0;
        }

        if (isBundle)
            BundleManager.Delete(settings.Name);
        else
            StorageManager.Delete(settings.Name);

        ConsoleUi.WriteSuccess($"Removed {type} '{settings.Name}'.");
        return 0;
    }
}
