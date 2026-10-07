using System.ComponentModel;
using Lasagna.Helpers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class LoadCommand : LasagnaCommand<LoadCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The stored item or bundle to pull into the current project.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--overwrite")]
        [Description("Replace files that already exist in the working directory.")]
        public bool Overwrite { get; init; }

        [CommandOption("--namespace <NAMESPACE>")]
        [Description("The target namespace. Defaults to the current project's namespace.")]
        public string? TargetNamespace { get; init; }

        [CommandOption("--destination <PATH>")]
        [Description("The relative folder where loaded files should be placed.")]
        public string Destination { get; init; } = ".";

        [CommandOption("--no-namespace-rewrite")]
        [Description("Copy files without changing namespaces.")]
        public bool NoNamespaceRewrite { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var targetNamespace = settings.TargetNamespace ?? DirectoryManager.GetProjectNamespace();

        if (!settings.NoNamespaceRewrite && targetNamespace is null)
            ConsoleUi.WriteWarning(
                "No .csproj was found above the working directory; namespaces will be copied unchanged.");

        var plan = TransferManager.BuildPlan(
            settings.Name,
            targetNamespace,
            !settings.NoNamespaceRewrite,
            settings.Destination);

        if (plan.Files.Count == 0)
            throw new InvalidDataException($"'{settings.Name}' does not contain any files.");

        AnsiConsole.Write(new Rule(
            $"[yellow]Pulling {(plan.IsBundle ? "bundle" : "item")} {Markup.Escape(plan.Name)}[/]"));

        AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(progressContext =>
            {
                var task = progressContext.AddTask(
                    "[green]Copying files[/]",
                    maxValue: plan.Files.Count);

                TransferManager.Pull(
                    plan,
                    settings.Overwrite,
                    progress =>
                    {
                        task.Description = $"[green]Copying {Markup.Escape(Path.GetFileName(progress.FilePath))}[/]";
                        task.Increment(1);
                    });
            });

        ConsoleUi.WriteSuccess(
            $"Pulled {plan.Files.Count} file{(plan.Files.Count == 1 ? string.Empty : "s")} " +
            $"into '{DirectoryManager.GetWorkingDirectory()}'.");

        return 0;
    }
}
