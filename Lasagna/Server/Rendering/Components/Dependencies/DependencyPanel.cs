using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Lasagna.Server.Rendering.Components.Dependencies;

[ContentInvocationPrefix("dependencies")]
internal static class DependencyPanel
{
    internal const string HostId = "dependencies-panel";

    internal static class ActionIds
    {
        internal const string Scan = "dependencies.scan";
    }

    internal static IHtmlContent Render(
        string scope,
        DependencyReport? report = null,
        string? message = null,
        bool isError = false)
        => FluentHtml.Div(panel =>
        {
            panel.Id(HostId)
                .Add(RenderScopeForm(scope));

            if (!string.IsNullOrWhiteSpace(message))
                panel.Add(PageFeedback.Render(message, isError));

            if (report is not null)
                panel.Add(RenderReport(report));
        });

    private static IHtmlContent RenderScopeForm(string scope)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Div(body =>
                {
                    body.Class("card-body")
                        .H2(h2 => h2.Class("h4", "mb-2").Text("Dependency scope"))
                        .P(p => p.Class("text-body-secondary", "mb-4")
                            .Text("Inspect projects, packages, restored assets, NuGet configuration, and local folder feeds without changing files."))
                        .Add(FluentHtml.Form(form =>
                        {
                            form.Id("dependencies-scope-form")
                                .Heimdall(heimdall => heimdall
                                    .Submit(ActionIds.Scan)
                                    .PayloadFromClosestForm()
                                    .Target($"#{HostId}")
                                    .SwapOuter()
                                    .PreventDefault()
                                    .Disable());

                            form.Div(row => row.Class("row", "g-3", "align-items-end")
                                .Div(column => column.Class("col-lg-9")
                                    .Label(label => label
                                        .For("dependencies-scope")
                                        .Class("form-label")
                                        .Text("Directory or solution scope"))
                                    .Input(Html.InputType.text, input => input
                                        .Id("dependencies-scope")
                                        .Name("Scope")
                                        .Value(string.IsNullOrWhiteSpace(scope) ? "." : scope)
                                        .Class("form-control", "font-monospace")
                                        .Placeholder("."))
                                    .Div(help => help.Class("form-text")
                                        .Text("Relative paths are resolved from the directory where Lasagna was launched.")))
                                .Div(column => column.Class("col-lg-3")
                                    .Button(button => button
                                        .Type("submit")
                                        .Class("btn", "btn-primary", "w-100")
                                        .Text("Scan dependencies"))));
                        }));
                });
        });

    private static IHtmlContent RenderReport(DependencyReport report)
        => FluentHtml.Fragment(fragment =>
        {
            fragment.Add(RenderSummary(report));
            fragment.Add(RenderCoverage(report));
            fragment.Add(RenderSources(report));
            fragment.Add(RenderGraph(report));
            fragment.Add(RenderTree(report));

            if (report.Issues.Count > 0)
                fragment.Add(RenderIssues(report));
        });

    private static IHtmlContent RenderCoverage(DependencyReport report)
        => FluentHtml.Article(article =>
        {
            var configurations = report.Projects
                .SelectMany(project => project.Configurations)
                .Concat(report.Solutions.SelectMany(solution => solution.Configurations))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(configuration => configuration, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var frameworks = report.Projects
                .SelectMany(project => project.TargetFrameworks)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(framework => framework, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-0").Text("Analysis coverage")))
                .Div(body => body.Class("card-body")
                    .Div(row => row.Class("row", "g-4")
                        .Div(column => column.Class("col-lg-4")
                            .Strong(strong => strong.Text("Solutions"))
                            .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-2")
                                .Text(report.Solutions.Count == 0
                                    ? "No solution files found; projects were discovered beneath the scope."
                                    : string.Join(", ", report.Solutions.Select(solution => solution.Name)))))
                        .Div(column => column.Class("col-lg-4")
                            .Strong(strong => strong.Text("Configurations"))
                            .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-2")
                                .Text(configurations.Length == 0 ? "Not declared" : string.Join(", ", configurations))))
                        .Div(column => column.Class("col-lg-4")
                            .Strong(strong => strong.Text("Target frameworks"))
                            .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-2")
                                .Text(frameworks.Length == 0 ? "Not declared" : string.Join(", ", frameworks))))));
        });

    private static IHtmlContent RenderSummary(DependencyReport report)
        => FluentHtml.Div(row => row.Class("row", "g-3", "mb-4")
            .Add(SummaryCard("Projects", report.Projects.Count.ToString(), "Projects discovered beneath the scope."))
            .Add(SummaryCard("Packages", report.PackageNodes.Count.ToString(), "Unique package/version nodes."))
            .Add(SummaryCard("References", (report.DirectPackageReferenceCount + report.ProjectReferenceCount).ToString(), "Direct project and package references."))
            .Add(SummaryCard("Diagnostics", report.Issues.Count.ToString(), "Warnings and scan notes.")));

    private static IHtmlContent SummaryCard(string label, string value, string description)
        => FluentHtml.Div(column => column.Class("col-sm-6", "col-xl-3")
            .Div(card => card.Class("card", "h-100", "shadow-sm")
                .Div(body => body.Class("card-body")
                    .Div(labelBlock => labelBlock.Class("small", "text-body-secondary").Text(label))
                    .Div(valueBlock => valueBlock.Class("display-6", "fw-semibold", "font-monospace").Text(value))
                    .P(p => p.Class("small", "text-body-secondary", "mb-0").Text(description)))));

    private static IHtmlContent RenderTree(DependencyReport report)
    {
        var projectNodes = report.ProjectNodes
            .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var incomingProjects = report.Edges
            .Where(edge => edge.Kind == DependencyEdgeKind.ProjectReference)
            .Select(edge => edge.To)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roots = projectNodes
            .Where(node => !incomingProjects.Contains(node.Key))
            .ToArray();

        if (roots.Length == 0)
            roots = projectNodes;

        return FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                    .H2(h2 => h2.Class("h4", "mb-0").Text("Dependency tree"))
                    .Add(Html.Tag("small", Html.Attr("class", "text-body-secondary"), Html.Text("collapsed by default"))))
                .Div(body => body.Class("card-body", "p-3")
                    .Div(tree =>
                    {
                        tree.Class("lasagna-dependency-tree");

                        foreach (var node in roots)
                            tree.Add(RenderNode(
                                report,
                                node,
                                null,
                                new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
                    }));
        });
    }

    private static IHtmlContent RenderGraph(DependencyReport report)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "d-flex", "justify-content-between", "align-items-baseline", "gap-3")
                    .H2(h2 => h2.Class("h4", "mb-0").Text("Dependency graph"))
                    .Add(Html.Tag("small", Html.Attr("class", "text-body-secondary"), Html.Text("click a node to focus"))))
                .Div(body =>
                {
                    body.Class("card-body", "p-0")
                        .Div(shell =>
                        {
                            shell.Class("lasagna-dependency-graph-shell")
                                .Attr("data-dependency-graph-shell", "true")
                                .Div(toolbar => toolbar.Class("lasagna-dependency-graph__toolbar", "d-flex", "align-items-center", "gap-2", "flex-wrap")
                                    .Button(button => button
                                        .Type("button")
                                        .Class("btn", "btn-sm", "btn-outline-secondary")
                                        .Attr("data-dependency-graph-action", "fit")
                                        .Text("Fit graph"))
                                    .Button(button => button
                                        .Type("button")
                                        .Class("btn", "btn-sm", "btn-outline-secondary")
                                        .Attr("data-dependency-graph-action", "reset")
                                        .Text("Reset view"))
                                    .Div(legend => legend.Class("lasagna-dependency-graph__legend", "d-flex", "align-items-center", "gap-3", "flex-wrap")
                                        .Add(GraphLegendItem("project", "Project"))
                                        .Add(GraphLegendItem("package", "Package"))
                                        .Add(GraphLegendItem("framework", "Framework"))))
                                .Div(canvas => canvas
                                    .Id("dependency-graph-canvas")
                                    .Class("lasagna-dependency-graph__canvas")
                                    .Attr("data-dependency-graph", SerializeGraph(report))
                                    .Attr("role", "application")
                                    .Attr("aria-label", "Interactive dependency graph")
                                    .Text("Loading dependency graph..."))
                                .Div(selection => selection
                                    .Id("dependency-graph-selection")
                                    .Class("lasagna-dependency-graph__selection", "small", "text-body-secondary")
                                    .Attr("aria-live", "polite")
                                    .Text("Click a node to inspect its dependencies."));
                });
        });
        });

    private static IHtmlContent GraphLegendItem(string kind, string label)
        => FluentHtml.Span(item => item.Class("lasagna-dependency-graph__legend-item")
            .Span(dot => dot.Class("lasagna-dependency-graph__legend-dot", $"is-{kind}").Attr("aria-hidden", "true"))
            .Span(text => text.Text(label)));

    private static string SerializeGraph(DependencyReport report)
    {
        var orderedNodes = report.Nodes
            .OrderBy(node => node.Kind)
            .ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nodeIds = orderedNodes
            .Select((node, index) => (node.Key, Id: $"node-{index}"))
            .ToDictionary(entry => entry.Key, entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var directPackageKeys = report.Edges
            .Where(edge => edge.Kind == DependencyEdgeKind.DirectPackageReference)
            .Select(edge => edge.To)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = orderedNodes.Select((node, index) => new
        {
            data = new
            {
                id = $"node-{index}",
                key = node.Key,
                name = node.Name,
                label = node.Version is null ? node.Name : $"{node.Name}\n{node.Version}",
                kind = node.Kind.ToString(),
                version = node.Version,
                path = node.Path,
                source = node.Source,
                direct = directPackageKeys.Contains(node.Key)
            }
        }).ToArray();
        var edges = report.Edges
            .Select((edge, index) => (edge, index))
            .Where(entry => nodeIds.ContainsKey(entry.edge.From) && nodeIds.ContainsKey(entry.edge.To))
            .Select(entry => new
            {
                data = new
                {
                    id = $"edge-{entry.index}",
                    source = nodeIds[entry.edge.From],
                    target = nodeIds[entry.edge.To],
                    edgeKind = entry.edge.Kind.ToString(),
                    targetFramework = entry.edge.TargetFramework,
                    condition = entry.edge.Condition
                }
            }).ToArray();

        return JsonSerializer.Serialize(new { nodes, edges });
    }

    private static IHtmlContent RenderNode(
        DependencyReport report,
        DependencyNode node,
        DependencyEdge? incoming,
        ISet<string> path)
        => FluentHtml.Tag("details", details =>
        {
            var cycle = path.Contains(node.Key);
            var nextPath = new HashSet<string>(path, StringComparer.OrdinalIgnoreCase)
            {
                node.Key
            };
            var children = report.Edges
                .Where(edge => string.Equals(edge.From, node.Key, StringComparison.OrdinalIgnoreCase))
                        .Where(edge => edge.Kind is DependencyEdgeKind.ProjectReference
                            or DependencyEdgeKind.FrameworkReference
                            or DependencyEdgeKind.DirectPackageReference
                            or DependencyEdgeKind.TransitivePackageReference)
                .OrderBy(edge => report.Nodes.FirstOrDefault(candidate => candidate.Key == edge.To)?.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            details.Class("lasagna-dependency-tree__node")
                .Tag("summary", summary => summary
                    .Class("lasagna-dependency-tree__summary")
                    .Div(name => name.Class("lasagna-dependency-tree__name-row")
                        .Span(span => span.Class("lasagna-dependency-tree__name", "font-monospace").Text(node.Name)))
                    .Div(metadata => metadata.Class("lasagna-dependency-tree__metadata")
                        .Add(RenderNodeBadges(node, incoming, cycle))))
                .Div(content =>
                {
                    content.Class("lasagna-dependency-tree__children");

                    if (cycle)
                    {
                        content.Div(note => note.Class("small", "text-warning").Text("Cycle detected; branch stopped."));
                        return;
                    }

                    foreach (var edge in children)
                    {
                        var child = report.Nodes.FirstOrDefault(candidate =>
                            string.Equals(candidate.Key, edge.To, StringComparison.OrdinalIgnoreCase));
                        if (child is not null)
                            content.Add(RenderNode(report, child, edge, nextPath));
                    }

                    if (children.Length == 0)
                        content.Div(empty => empty.Class("small", "text-body-secondary").Text("No nested dependencies."));
                });
        });

    private static IHtmlContent RenderNodeBadges(
        DependencyNode node,
        DependencyEdge? incoming,
        bool cycle)
        => FluentHtml.Span(copy =>
        {
            copy.Class("d-flex", "align-items-center", "gap-2", "flex-wrap");

            copy.Add(Html.Tag(
                "span",
                Html.Attr("class", $"badge {GetNodeBadgeClass(node.Kind)}"),
                Html.Text(node.Kind.ToString())));

            if (!string.IsNullOrWhiteSpace(node.Version))
            {
                copy.Add(Html.Tag(
                    "span",
                    Html.Attr("class", "badge text-bg-secondary font-monospace"),
                    Html.Text(node.Version)));
            }

            if (incoming?.Kind == DependencyEdgeKind.DirectPackageReference)
            {
                copy.Add(Html.Tag("span", Html.Attr("class", "badge text-bg-primary"), Html.Text("direct")));
            }
            else if (incoming?.Kind == DependencyEdgeKind.TransitivePackageReference)
            {
                copy.Add(Html.Tag("span", Html.Attr("class", "badge text-bg-dark"), Html.Text("transitive")));
            }

            if (!string.IsNullOrWhiteSpace(node.Source))
            {
                copy.Add(Html.Tag(
                    "small",
                    Html.Attr("class", "text-body-secondary"),
                    Html.Text($"source: {node.Source}")));
            }

            if (cycle)
                copy.Add(Html.Tag("span", Html.Attr("class", "badge text-bg-warning"), Html.Text("cycle")));
        });

    private static IHtmlContent RenderSources(DependencyReport report)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-0").Text("NuGet sources")))
                .Div(body =>
                {
                    body.Class("card-body", "p-0");

                    if (report.PackageSources.Count == 0)
                    {
                        body.Div(empty => empty.Class("p-4", "text-body-secondary").Text("No NuGet.Config package sources were found."));
                        return;
                    }

                    body.Div(tableHost => tableHost.Class("table-responsive")
                        .Table(table =>
                        {
                            table.Class("table", "table-hover", "lasagna-dependency-sources", "mb-0")
                                .TableHead(head => head.TableRow(row => row
                                    .TableHeaderCell(cell => cell.Text("Source"))
                                    .TableHeaderCell(cell => cell.Text("Type"))
                                    .TableHeaderCell(cell => cell.Text("Location"))
                                    .TableHeaderCell(cell => cell.Text("Configuration"))))
                                .TableBody(tableBody =>
                                {
                                    foreach (var source in report.PackageSources)
                                    {
                                        tableBody.TableRow(row => row
                                            .TableDataCell(cell => cell.Text(source.Name))
                                            .TableDataCell(cell => cell.Text(source.IsLocalFolder ? "Local folder" : "Remote/configured"))
                                            .TableDataCell(cell => cell.Class("font-monospace", "text-break").Text(source.ResolvedPath ?? source.Value))
                                            .TableDataCell(cell => cell.Class("font-monospace", "text-break").Text(source.ConfigurationPath)));
                                    }
                                });
                        }));
                });
        });

    private static IHtmlContent RenderIssues(DependencyReport report)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-0").Text("Diagnostics")))
                .Div(body => body.Class("card-body")
                    .Ul(list =>
                    {
                        list.Class("mb-0");
                        foreach (var issue in report.Issues)
                        {
                            list.Li(item => item.Class("mb-2")
                                .Span(span => span.Class("badge", issue.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "text-bg-warning" : "text-bg-secondary").Text(issue.Severity))
                                .Span(span => span.Class("ms-2").Text(issue.Message))
                                .Div(path => path.Class("small", "text-body-secondary", "font-monospace").Text(issue.Path ?? string.Empty)));
                        }
                    }));
        });

    private static string GetNodeBadgeClass(DependencyNodeKind kind) => kind switch
    {
        DependencyNodeKind.Project => "text-bg-primary",
        DependencyNodeKind.Package => "text-bg-success",
        DependencyNodeKind.Framework => "text-bg-secondary",
        _ => "text-bg-secondary"
    };

    [ContentInvocation("scan")]
    public static IHtmlContent Scan([FromForm] DependencyScopePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            return Render(scope, DependencyAnalyzer.BuildReport(scope));
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            DirectoryNotFoundException or
            FileNotFoundException or
            IOException or
            UnauthorizedAccessException)
        {
            return Render(payload.Scope, message: exception.Message, isError: true);
        }
    }

    public sealed class DependencyScopePayload
    {
        public string Scope { get; set; } = ".";
    }
}
