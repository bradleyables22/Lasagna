using System.ComponentModel;
using Lasagna.Server;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BrowseDependenciesCommand : LasagnaCommand<BrowseDependenciesCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("The project or solution directory to inspect. Defaults to the current working directory.")]
        public string Scope { get; init; } = ".";
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        LocalWebHost.Run($"/dependencies?Scope={Uri.EscapeDataString(settings.Scope)}");
        return 0;
    }
}
