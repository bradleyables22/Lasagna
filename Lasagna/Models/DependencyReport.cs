namespace Lasagna.Models;

internal enum DependencyNodeKind
{
    Project,
    Package,
    Framework
}

internal enum DependencyEdgeKind
{
    ProjectReference,
    DirectPackageReference,
    TransitivePackageReference,
    FrameworkReference
}

internal sealed record DependencyNode(
    string Key,
    string Name,
    DependencyNodeKind Kind,
    string? Version = null,
    string? Path = null,
    string? Source = null);

internal sealed record DependencyEdge(
    string From,
    string To,
    DependencyEdgeKind Kind,
    string? TargetFramework = null,
    string? Condition = null,
    string? Source = null);

internal sealed record DependencyProject(
    string NodeKey,
    string Name,
    string Path,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> Configurations,
    IReadOnlyList<string> Solutions,
    bool HasAssetsFile);

internal sealed record DependencySolution(
    string Name,
    string Path,
    IReadOnlyList<string> ProjectPaths,
    IReadOnlyList<string> Configurations);

internal sealed record DependencyPackageSource(
    string Name,
    string Value,
    string? ResolvedPath,
    string ConfigurationPath,
    bool IsLocalFolder,
    bool IsEnabled);

internal sealed record DependencyIssue(
    string Severity,
    string Message,
    string? Path = null);

internal sealed record DependencyReport(
    string RootPath,
    IReadOnlyList<DependencySolution> Solutions,
    IReadOnlyList<DependencyProject> Projects,
    IReadOnlyList<DependencyNode> Nodes,
    IReadOnlyList<DependencyEdge> Edges,
    IReadOnlyList<DependencyPackageSource> PackageSources,
    IReadOnlyList<DependencyIssue> Issues)
{
    internal IReadOnlyList<DependencyNode> PackageNodes =>
        Nodes.Where(node => node.Kind == DependencyNodeKind.Package).ToArray();

    internal IReadOnlyList<DependencyNode> ProjectNodes =>
        Nodes.Where(node => node.Kind == DependencyNodeKind.Project).ToArray();

    internal IReadOnlyList<DependencyNode> FrameworkNodes =>
        Nodes.Where(node => node.Kind == DependencyNodeKind.Framework).ToArray();

    internal int ProjectReferenceCount =>
        Edges.Count(edge => edge.Kind == DependencyEdgeKind.ProjectReference);

    internal int DirectPackageReferenceCount =>
        Edges.Count(edge => edge.Kind == DependencyEdgeKind.DirectPackageReference);

    internal int TransitivePackageReferenceCount =>
        Edges.Count(edge => edge.Kind == DependencyEdgeKind.TransitivePackageReference);
}
