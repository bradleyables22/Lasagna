using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class DependencyReplaceCommand : LasagnaCommand<DependencyReplaceCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "<package>")]
        [Description("The direct NuGet package reference to replace.")]
        public string PackageId { get; init; } = string.Empty;

        [CommandOption("--with-kit <KIT>")]
        [Description("The stored kit containing the source code replacement.")]
        public string Kit { get; init; } = string.Empty;

        [CommandOption("--project <PATH>")]
        [Description("The .csproj file or directory containing exactly one project. Defaults to the current directory.")]
        public string Project { get; init; } = ".";

        [CommandOption("--destination <PATH>")]
        [Description("The relative project folder where kit files should be injected.")]
        public string Destination { get; init; } = ".";

        [CommandOption("--namespace <NAMESPACE>")]
        [Description("The target namespace. Defaults to the selected project's namespace.")]
        public string? TargetNamespace { get; init; }

        [CommandOption("--no-namespace-rewrite")]
        [Description("Keep kit namespaces and existing project references unchanged.")]
        public bool NoNamespaceRewrite { get; init; }

        [CommandOption("--overwrite")]
        [Description("Replace files already present at the destination.")]
        public bool Overwrite { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview the replacement without writing files.")]
        public bool DryRun { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Kit))
            throw new InvalidOperationException("Specify the replacement kit with --with-kit <KIT>.");

        var projectPath = ResolveProjectPath(settings.Project);
        var plan = KitReplacementManager.BuildPlan(
            settings.PackageId,
            settings.Kit,
            projectPath,
            settings.Destination,
            settings.TargetNamespace,
            !settings.NoNamespaceRewrite,
            settings.Overwrite);

        WriteSummary(plan, settings.DryRun);

        if (settings.DryRun)
        {
            ConsoleUi.WriteInfo("Preview only. Re-run without --dry-run to apply this replacement.");
            return 0;
        }

        var report = KitReplacementManager.Apply(
            plan,
            progress: (progress) => ConsoleUi.WriteProgress(
                "Injecting",
                progress.Completed,
                progress.Total,
                progress.FilePath));

        ConsoleUi.WriteSuccess(
            $"Replaced package '{plan.PackageId}' with kit '{plan.KitName}' in " +
            $"'{Path.GetDirectoryName(plan.ProjectPath)}'.");
        ConsoleUi.WriteSummary(
            "Replacement applied",
            ("Files written", report.FilesWritten.ToString()),
            ("Kit files injected", report.KitFilesWritten.ToString()),
            ("Project files rewritten", report.ProjectFilesRewritten.ToString()),
            ("Namespace references rewritten", report.NamespaceReferencesRewritten.ToString()),
            ("Package references removed", report.PackageReferencesRemoved.ToString()));

        return 0;
    }

    private static string ResolveProjectPath(string path)
    {
        var resolved = DirectoryManager.GetExistingPath(path);
        if (File.Exists(resolved))
        {
            if (!string.Equals(Path.GetExtension(resolved), ".csproj", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--project must point to a .csproj file or a directory containing one project.");

            return Path.GetFullPath(resolved);
        }

        var project = DirectoryManager.FindProjectFile(resolved);
        if (project is null)
        {
            throw new InvalidOperationException(
                $"Could not identify exactly one .csproj beneath '{resolved}'. Specify --project with a project file or narrower directory.");
        }

        return Path.GetFullPath(project);
    }

    private static void WriteSummary(KitReplacementPlan plan, bool dryRun)
    {
        ConsoleUi.WriteSummary(
            dryRun ? "Kit replacement preview" : "Kit replacement",
            ("Package", plan.PackageId),
            ("Kit", plan.KitName),
            ("Project", plan.ProjectPath),
            ("Destination", plan.DestinationPath),
            ("Source namespace", plan.SourceNamespace ?? "unchanged"),
            ("Target namespace", plan.TargetNamespace ?? "unchanged"),
            ("Kit files", plan.KitFileCount.ToString()),
            ("Existing project files rewritten", plan.ExistingProjectFileCount.ToString()),
            ("Namespace references", plan.NamespaceReferencesRewritten.ToString()),
            ("Package references removed", plan.PackageReferencesRemoved.ToString()),
            ("Existing destination files", plan.ExistingFileCount.ToString()));
    }
}
