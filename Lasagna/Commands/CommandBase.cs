using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal abstract class LasagnaCommandSettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Show additional diagnostic details when a command fails.")]
    public bool Verbose { get; init; }
}

internal abstract class LasagnaCommand<TSettings> : Command<TSettings> where TSettings : LasagnaCommandSettings
{
    public sealed override int Execute(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            return ExecuteCommand(context, settings);
        }
        catch (OperationCanceledException)
        {
            ConsoleUi.WriteWarning("Operation cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            ConsoleUi.WriteError(exception.Message);

            if (settings.Verbose)
                Console.Error.WriteLine(exception);

            return 1;
        }
    }

    protected abstract int ExecuteCommand(CommandContext context, TSettings settings);
}
