using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class LoadCommand : LasagnaCommand<LoadCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The stored item, bundle, or kit to pull into the current project.")]
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

        [CommandOption("--migrate")]
        [Description("Compatibility alias; namespace alignment is automatic during load.")]
        public bool Migrate { get; init; }
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

        ConsoleUi.WriteInfo(
            $"Pulling {GetSourceLabel(plan.Kind)} '{plan.Name}'...");
        var report = TransferManager.Pull(
            plan,
            settings.Overwrite,
            progress => ConsoleUi.WriteProgress(
                "Copying",
                progress.Completed,
                progress.Total,
                progress.FilePath));

        if (!settings.NoNamespaceRewrite &&
            (report.FilesRewritten > 0 || report.ReferencesRewritten > 0))
        {
            ConsoleUi.WriteInfo(
                $"Namespace alignment rewrote {report.FilesRewritten} file{(report.FilesRewritten == 1 ? string.Empty : "s")} " +
                $"and {report.ReferencesRewritten} namespace reference{(report.ReferencesRewritten == 1 ? string.Empty : "s")}.");
        }

        ConsoleUi.WriteSuccess(
            $"Pulled {plan.Files.Count} file{(plan.Files.Count == 1 ? string.Empty : "s")} " +
            $"into '{DirectoryManager.GetWorkingDirectory()}'.");

        return 0;
    }

    private static string GetSourceLabel(TransferPlanKind kind) => kind switch
    {
        TransferPlanKind.Bundle => "bundle",
        TransferPlanKind.Kit => "kit",
        _ => "item"
    };
}
