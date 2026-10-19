using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class ShakeCommand : LasagnaCommand<ShakeCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("The directory to scan recursively. Defaults to the current working directory.")]
        public string Scope { get; init; } = ".";

        [CommandOption("--project <PATH>")]
        [Description("The .csproj to build after applying removals, or a directory containing exactly one project.")]
        public string? Project { get; init; }

        [CommandOption("--apply")]
        [Description("Remove only high-confidence unused local C# files, then build-validate the result.")]
        public bool Apply { get; init; }

        [CommandOption("--dry-run")]
        [Description("Preview the cleanup without writing files. This is the default.")]
        public bool DryRun { get; init; }
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        if (settings.Apply && settings.DryRun)
            throw new InvalidOperationException("Use either --apply or --dry-run, not both.");

        var plan = ShakeAnalyzer.BuildPlan(settings.Scope, settings.Project);
        WriteSummary(plan, settings.Apply);
        WriteEvidence(plan);

        if (!settings.Apply)
        {
            ConsoleUi.WriteInfo(
                "Preview only. Re-run with --apply after reviewing the reference evidence above.");
            return 0;
        }

        var result = ShakeAnalyzer.Apply(plan);
        ConsoleUi.WriteSuccess(
            $"Removed {result.FilesRemoved} unused source file{(result.FilesRemoved == 1 ? string.Empty : "s")}. " +
            "The project build passed and the cleanup was kept.");

        if (!string.IsNullOrWhiteSpace(result.BuildOutput))
            Console.WriteLine(result.BuildOutput.TrimEnd());

        return 0;
    }

    private static void WriteSummary(ShakePlan plan, bool applying)
    {
        ConsoleUi.WriteSummary(
            applying ? "Leaf shake" : "Leaf shake preview",
            ("Scope", plan.RootPath),
            ("Build project", plan.BuildProjectPath ?? "not identified"),
            ("C# files scanned", plan.FilesScanned.ToString()),
            ("Types scanned", plan.TypesScanned.ToString()),
            ("Removable files", plan.RemovableFiles.Count.ToString()),
            ("Review required", plan.ReviewFiles.Count.ToString()),
            ("Referenced files", plan.ReferencedFiles.Count.ToString()),
            ("Reference records", plan.ReferenceCount.ToString()),
            ("Indirect-use hints", plan.IndirectHintCount.ToString()),
            ("Diagnostics", plan.Issues.Count.ToString()));
    }

    private static void WriteEvidence(ShakePlan plan)
    {
        WriteSection(
            "Candidates eligible for removal",
            plan.RemovableFiles,
            plan.RootPath,
            includeReferences: false);
        WriteSection(
            "Files kept for review",
            plan.ReviewFiles,
            plan.RootPath,
            includeReferences: true);
        WriteSection(
            "Files with local references",
            plan.ReferencedFiles,
            plan.RootPath,
            includeReferences: true);

        if (plan.ProtectedFiles.Count > 0)
        {
            Console.WriteLine("Protected files");
            foreach (var file in plan.ProtectedFiles)
                Console.WriteLine($"  - {RelativePath(plan.RootPath, file.FilePath)}: {file.Reason}");
            Console.WriteLine();
        }

        foreach (var issue in plan.Issues)
            ConsoleUi.WriteWarning(issue);
    }

    private static void WriteSection(
        string title,
        IReadOnlyList<ShakeFileReport> files,
        string rootPath,
        bool includeReferences)
    {
        if (files.Count == 0)
            return;

        Console.WriteLine(title);
        foreach (var file in files)
        {
            Console.WriteLine($"  - {RelativePath(rootPath, file.FilePath)}");
            Console.WriteLine($"    Reason: {file.Reason}");

            foreach (var symbol in file.Symbols)
            {
                Console.WriteLine(
                    $"    Type: {symbol.DisplayName} (line {symbol.DeclarationLine}, {symbol.Accessibility})");

                if (includeReferences)
                {
                    WriteReferenceLines("Local reference", symbol.References, rootPath);
                    WriteReferenceLines("Indirect hint", symbol.IndirectHints, rootPath);
                }
                else
                {
                    Console.WriteLine("      Local semantic references: 0");
                    Console.WriteLine("      Indirect-use hints: 0");
                }
            }
        }

        Console.WriteLine();
    }

    private static void WriteReferenceLines(
        string label,
        IReadOnlyList<ShakeReference> references,
        string rootPath)
    {
        if (references.Count == 0)
        {
            Console.WriteLine($"      {label}: none");
            return;
        }

        foreach (var reference in references)
        {
            Console.WriteLine(
                $"      {label}: {RelativePath(rootPath, reference.FilePath)}:{reference.Line} " +
                $"({reference.Reason})");
        }
    }

    private static string RelativePath(string rootPath, string path) =>
        Path.GetRelativePath(rootPath, path).Replace('\\', '/');
}
