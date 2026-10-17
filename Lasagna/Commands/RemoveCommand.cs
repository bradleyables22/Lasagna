using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class RemoveCommand : LasagnaCommand<RemoveCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The item, bundle, or kit to remove.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--item")]
        [Description("Treat the name as an item.")]
        public bool Item { get; init; }

        [CommandOption("--bundle")]
        [Description("Treat the name as a bundle.")]
        public bool Bundle { get; init; }

        [CommandOption("--kit")]
        [Description("Treat the name as a kit.")]
        public bool Kit { get; init; }

    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var typeFlags = new[] { settings.Item, settings.Bundle, settings.Kit }
            .Count(value => value);

        if (typeFlags > 1)
            throw new InvalidOperationException("Choose only one of --item, --bundle, or --kit.");

        var type = settings.Bundle
            ? "bundle"
            : settings.Kit
                ? "kit"
                : settings.Item
                    ? "item"
                    : ResolveType(settings.Name);

        var exists = type switch
        {
            "bundle" => BundleManager.Exists(settings.Name),
            "kit" => KitManager.Exists(settings.Name),
            _ => StorageManager.ItemExists(settings.Name)
        };

        if (!exists)
            throw new DirectoryNotFoundException(
                $"No {type} named '{settings.Name}' exists.");

        switch (type)
        {
            case "bundle":
                BundleManager.Delete(settings.Name);
                break;
            case "kit":
                KitManager.Delete(settings.Name);
                break;
            default:
                StorageManager.Delete(settings.Name);
                break;
        }

        ConsoleUi.WriteSuccess($"Removed {type} '{settings.Name}'.");
        return 0;
    }

    private static string ResolveType(string name)
    {
        if (BundleManager.Exists(name))
            return "bundle";

        if (KitManager.Exists(name))
            return "kit";

        return "item";
    }
}
