using Lasagna.Server;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class BrowseWebCommand : LasagnaCommand<BrowseWebCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        LocalWebHost.Run();
        return 0;
    }
}
