using System.ComponentModel;
using Lasagna.Helpers;
using Lasagna.Models;
using Spectre.Console.Cli;

namespace Lasagna.Commands;

internal sealed class DependencyListCommand : LasagnaCommand<DependencyListCommand.Settings>
{
    public sealed class Settings : LasagnaCommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("The project or solution directory to inspect. Defaults to the current working directory.")]
        public string Scope { get; init; } = ".";
    }

    protected override int ExecuteCommand(CommandContext context, Settings settings)
    {
        var report = DependencyAnalyzer.BuildReport(settings.Scope);

        ConsoleUi.WriteSummary(
            "Dependency inventory",
            ("Scope", report.RootPath),
            ("Solutions", report.Solutions.Count.ToString()),
            ("Projects", report.Projects.Count.ToString()),
            ("Packages", report.PackageNodes.Count.ToString()),
            ("Project references", report.ProjectReferenceCount.ToString()),
            ("Direct package references", report.DirectPackageReferenceCount.ToString()),
            ("Transitive package references", report.TransitivePackageReferenceCount.ToString()),
            ("Package sources", report.PackageSources.Count.ToString()),
            ("Diagnostics", report.Issues.Count.ToString()));

        foreach (var solution in report.Solutions)
        {
            Console.WriteLine($"Solution: {solution.Name}");
            foreach (var projectPath in solution.ProjectPaths)
                Console.WriteLine($"  - {Path.GetRelativePath(report.RootPath, projectPath)}");
            Console.WriteLine();
        }

        foreach (var project in report.Projects)
        {
            Console.WriteLine(project.Name);
            Console.WriteLine($"  {Path.GetRelativePath(report.RootPath, project.Path)}");

            foreach (var edge in report.Edges
                         .Where(edge => string.Equals(edge.From, project.NodeKey, StringComparison.OrdinalIgnoreCase))
                         .Where(edge => edge.Kind is DependencyEdgeKind.ProjectReference or DependencyEdgeKind.DirectPackageReference)
                         .OrderBy(edge => edge.Kind)
                         .ThenBy(edge => GetNode(report, edge.To)?.Name, StringComparer.OrdinalIgnoreCase))
            {
                WriteTree(report, edge.To, "  ", edge.Kind, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            Console.WriteLine();
        }

        if (report.PackageSources.Count > 0)
        {
            Console.WriteLine("Package sources");
            foreach (var source in report.PackageSources)
            {
                var state = source.IsEnabled ? "enabled" : "disabled";
                var kind = source.IsLocalFolder ? "local folder" : "remote/configured";
                Console.WriteLine($"  {source.Name} [{kind}, {state}] {source.Value}");
            }

            Console.WriteLine();
        }

        foreach (var issue in report.Issues)
        {
            var text = $"[{issue.Severity}] {issue.Message}" +
                       (string.IsNullOrWhiteSpace(issue.Path) ? string.Empty : $" ({issue.Path})");

            if (issue.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                ConsoleUi.WriteWarning(text);
            else
                ConsoleUi.WriteInfo(text);
        }

        return 0;
    }

    private static void WriteTree(
        DependencyReport report,
        string nodeKey,
        string indent,
        DependencyEdgeKind edgeKind,
        ISet<string> visited)
    {
        var node = GetNode(report, nodeKey);
        if (node is null)
            return;

        var label = node.Kind == DependencyNodeKind.Package && !string.IsNullOrWhiteSpace(node.Version)
            ? $"{node.Name} {node.Version}"
            : node.Name;
        var relationship = edgeKind == DependencyEdgeKind.ProjectReference
            ? "project"
            : edgeKind == DependencyEdgeKind.DirectPackageReference
                ? "direct"
                : "transitive";
        var source = string.IsNullOrWhiteSpace(node.Source) ? string.Empty : $" [{node.Source}]";

        Console.WriteLine($"{indent}└── {label} ({relationship}){source}");

        if (!visited.Add(nodeKey))
        {
            Console.WriteLine($"{indent}    ↳ already shown");
            return;
        }

        foreach (var edge in report.Edges
                     .Where(edge => string.Equals(edge.From, nodeKey, StringComparison.OrdinalIgnoreCase))
                     .Where(edge => edge.Kind is DependencyEdgeKind.ProjectReference or DependencyEdgeKind.TransitivePackageReference)
                     .OrderBy(edge => GetNode(report, edge.To)?.Name, StringComparer.OrdinalIgnoreCase))
        {
            WriteTree(report, edge.To, indent + "    ", edge.Kind, visited);
        }
    }

    private static DependencyNode? GetNode(DependencyReport report, string key) =>
        report.Nodes.FirstOrDefault(node => string.Equals(node.Key, key, StringComparison.OrdinalIgnoreCase));
}
