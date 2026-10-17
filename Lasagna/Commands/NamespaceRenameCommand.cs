using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class NamespaceRenameCommand : LasagnaCommand<NamespaceRenameCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<from>")]
        [Description("The namespace prefix to replace.")]
        public string From { get; init; } = string.Empty;

        [CommandArgument(1, "<to>")]
        [Description("The namespace prefix to use instead.")]
        public string To { get; init; } = string.Empty;

        [CommandOption("--path <PATH>")]
        [Description("The directory to scan. Defaults to the current working directory.")]
        public string Scope { get; init; } = ".";

        [CommandOption("--apply")]
        [Description("Write the planned changes. Without this option, the command previews only.")]
        public bool Apply { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview changes without writing them. This is the default.")]
        public bool DryRun { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Apply && settings.DryRun)
        {
            throw new InvalidOperationException(
                "Use either --apply or --dry-run, not both.");
        }

        var plan = NamespaceRenameManager.BuildPlan(
            settings.Scope,
            settings.From,
            settings.To);

        if (plan.FailedFiles.Count > 0)
        {
            var failures = string.Join(
                Environment.NewLine,
                plan.FailedFiles.Select(file =>
                    $"- {Path.GetRelativePath(plan.RootPath, file.FilePath)}: " +
                    (file.Message ?? file.Status.ToString())));

            throw new InvalidDataException(
                $"Namespace rename stopped because {plan.FailedFiles.Count} file(s) could not be parsed.{Environment.NewLine}{failures}");
        }

        WriteSummary(plan, settings.Apply);

        if (settings.Apply)
        {
            NamespaceRenameManager.Apply(plan);
            ConsoleUi.WriteSuccess(
                $"Applied namespace rename to {plan.ChangedFiles.Count} file{(plan.ChangedFiles.Count == 1 ? string.Empty : "s")}.");
        }
        else
        {
            ConsoleUi.WriteInfo(
                "Preview only. Re-run with --apply to write these changes.");
        }

        return 0;
    }

    private static void WriteSummary(NamespaceRenamePlan plan, bool applying)
    {
        ConsoleUi.WriteSummary(
            applying ? "Namespace rename" : "Namespace rename preview",
            ("From", plan.SourceNamespace),
            ("To", plan.TargetNamespace),
            ("Scope", plan.RootPath),
            ("Files scanned", plan.Files.Count.ToString()),
            ("Files changed", plan.ChangedFiles.Count.ToString()),
            ("Namespace mappings", plan.ReferencesRewritten.ToString()));
    }
}
