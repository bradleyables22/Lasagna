using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Shake;

[ContentInvocationPrefix("shake.packages")]
internal static class PackageReferencePanel
{
    internal const string HostId = "shake-package-reference-panel";

    internal static class ActionIds
    {
        internal const string Load = "shake.packages.load";
    }

    internal static IHtmlContent Render(string scope)
        => FluentHtml.Div(host =>
        {
            host.Id(HostId)
                .Add(RenderDeferred(scope));
        });

    private static IHtmlContent RenderDeferred(string scope)
        => FluentHtml.Div(loader => loader
            .Class("card", "shadow-sm", "mb-4")
            .Heimdall(heimdall => heimdall
                .Load(ActionIds.Load)
                .Payload(new { Scope = scope })
                .Target($"#{HostId}")
                .SwapInner())
            .Div(body => body.Class("card-body", "d-flex", "align-items-center", "gap-3")
                .Add(Html.Tag("span", Html.Attr("class", "spinner-border spinner-border-sm"), Html.Attr("aria-hidden", "true")))
                .Div(copy => copy
                    .H2(h2 => h2.Class("h5", "mb-1").Text("Checking package references"))
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("Looking for direct NuGet references without source usage evidence...")))));

    private static IHtmlContent RenderContent(PackageReferencePlan plan)
        => FluentHtml.Article(article =>
        {
            var directReferenceLabel = plan.References.Count == 1
                ? "1 direct package reference"
                : $"{plan.References.Count} direct package references";

            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-1").Text("Package reference cleanup"))
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("Direct package references are checked against restored compile assets and source symbols. Candidates are suggestions only; runtime, reflection, analyzer, and build behavior still need review.")))
                .Div(body => body.Class("card-body")
                    .Tag("details", details => details
                        .Class("lasagna-package-cleanup-details")
                        .Tag("summary", summary => summary
                            .Class("lasagna-package-cleanup-details__summary", "fw-semibold")
                            .Text($"Show package details · {directReferenceLabel}"))
                        .Div(content => content
                            .Class("pt-3")
                            .Add(RenderSummary(plan))
                            .Add(RenderNoDirectReferences(plan))
                            .Add(RenderStatusSection(
                                "Potentially unnecessary",
                                "No semantic or textual source evidence was found. Verify the package is not used by runtime configuration, reflection, analyzers, generators, or build targets before removing it.",
                                plan.PotentiallyUnnecessary,
                                plan,
                                "border-warning"))
                            .Add(RenderStatusSection(
                                "Review required",
                                "The scan found an indirect-use indicator or could not safely prove absence.",
                                plan.Review,
                                plan,
                                "border-secondary"))
                            .Add(RenderStatusSection(
                                "Referenced packages kept",
                                "These direct references have semantic source evidence in the project.",
                                plan.Used,
                                plan,
                                "border-success"))
                            .Add(RenderIssues(plan.Issues)))));
        });

    private static IHtmlContent RenderSummary(PackageReferencePlan plan)
        => FluentHtml.Div(row => row.Class("row", "g-3", "mb-4")
            .Add(SummaryCard("Direct refs", plan.References.Count.ToString(), "Direct package references checked."))
            .Add(SummaryCard("Candidates", plan.PotentiallyUnnecessary.Count.ToString(), "References with no source evidence."))
            .Add(SummaryCard("Review", plan.Review.Count.ToString(), "References needing manual review."))
            .Add(SummaryCard("Used", plan.Used.Count.ToString(), "References with semantic source evidence.")));

    private static IHtmlContent RenderNoDirectReferences(PackageReferencePlan plan)
    {
        if (plan.References.Count > 0)
            return Empty();

        return FluentHtml.Div(empty => empty
            .Class("border", "rounded", "p-3", "text-body-secondary")
            .Text("No direct package references were found in the discovered projects."));
    }

    private static IHtmlContent SummaryCard(string label, string value, string description)
        => FluentHtml.Div(column => column.Class("col-sm-6", "col-xl-3")
            .Div(card => card.Class("card", "h-100", "border-0", "bg-body-tertiary")
                .Div(body => body.Class("card-body")
                    .Div(labelBlock => labelBlock.Class("small", "text-body-secondary").Text(label))
                    .Div(valueBlock => valueBlock.Class("display-6", "fw-semibold", "font-monospace").Text(value))
                    .P(p => p.Class("small", "text-body-secondary", "mb-0").Text(description)))));

    private static IHtmlContent RenderStatusSection(
        string title,
        string description,
        IReadOnlyList<PackageReferenceReport> references,
        PackageReferencePlan plan,
        string borderClass)
    {
        if (references.Count == 0)
            return Empty();

        return FluentHtml.Div(section => section
            .Class("border-top", "pt-4", "mt-4")
            .H3(h3 => h3.Class("h5", "mb-1").Text($"{title} ({references.Count})"))
            .P(p => p.Class("small", "text-body-secondary", "mb-3").Text(description))
            .Div(list =>
            {
                list.Class("d-grid", "gap-2");
                foreach (var reference in references)
                    list.Add(RenderReference(reference, plan, borderClass));
            }));
    }

    private static IHtmlContent RenderReference(
        PackageReferenceReport reference,
        PackageReferencePlan plan,
        string borderClass)
        => FluentHtml.Div(item => item
            .Class("border", borderClass, "rounded", "p-3")
            .Div(header => header.Class("d-flex", "justify-content-between", "align-items-baseline", "gap-3", "flex-wrap")
                .Div(copy => copy
                    .Strong(strong => strong.Class("font-monospace", "text-break").Text(reference.PackageId))
                    .Div(project => project.Class("small", "text-body-secondary", "mt-1")
                        .Text($"{reference.ProjectName} · {RelativePath(plan.RootPath, reference.ProjectPath)}")))
                .Div(meta => meta.Class("d-flex", "align-items-center", "gap-2")
                    .Add(Html.Tag("span", Html.Attr("class", StatusBadgeClass(reference.Status)), Html.Text(StatusLabel(reference.Status))))
                    .Add(reference.Version is null
                        ? Empty()
                        : Html.Tag("small", Html.Attr("class", "font-monospace text-body-secondary"), Html.Text(reference.Version)))))
            .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-2").Text(reference.Reason))
            .Add(RenderEvidence(reference, plan)));

    private static IHtmlContent RenderEvidence(
        PackageReferenceReport reference,
        PackageReferencePlan plan)
    {
        if (reference.Evidence.Count == 0)
            return Empty();

        return FluentHtml.Div(block => block
            .Class("mt-3")
            .Div(label => label.Class("small", "fw-semibold").Text("Evidence"))
            .Ul(list =>
            {
                list.Class("small", "mb-0", "ps-4");
                foreach (var evidence in reference.Evidence)
                {
                    list.Li(item => item
                        .Span(span => span.Class("font-monospace").Text(
                            $"{RelativePath(plan.RootPath, evidence.FilePath)}:{evidence.Line}"))
                        .Text($" — {evidence.Reason}"));
                }
            }));
    }

    private static IHtmlContent RenderIssues(IReadOnlyList<string> issues)
    {
        if (issues.Count == 0)
            return Empty();

        return FluentHtml.Div(section => section
            .Class("border-top", "pt-4", "mt-4")
            .H3(h3 => h3.Class("h5", "mb-2").Text("Scan notes"))
            .Ul(list =>
            {
                list.Class("small", "mb-0");
                foreach (var issue in issues)
                    list.Li(item => item.Text(issue));
            }));
    }

    private static string RelativePath(string rootPath, string path) =>
        Path.GetRelativePath(rootPath, path).Replace(Path.DirectorySeparatorChar, '/');

    private static IHtmlContent Empty() => new HtmlString(string.Empty);

    private static string StatusLabel(PackageReferenceStatus status) => status switch
    {
        PackageReferenceStatus.PotentiallyUnnecessary => "Potentially unnecessary",
        PackageReferenceStatus.Used => "Used",
        PackageReferenceStatus.Review => "Review",
        _ => status.ToString()
    };

    private static string StatusBadgeClass(PackageReferenceStatus status) => status switch
    {
        PackageReferenceStatus.PotentiallyUnnecessary => "badge text-bg-warning",
        PackageReferenceStatus.Used => "badge text-bg-success",
        PackageReferenceStatus.Review => "badge text-bg-secondary",
        _ => "badge text-bg-secondary"
    };

    [ContentInvocation("load")]
    public static IHtmlContent Load([ContentPayload] PackageReferencePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            return RenderContent(PackageReferenceAnalyzer.BuildPlan(scope));
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            DirectoryNotFoundException or
            FileNotFoundException or
            IOException or
            InvalidDataException or
            UnauthorizedAccessException or
            System.Xml.XmlException)
        {
            return PageFeedback.Render(exception.Message, isError: true);
        }
    }

    public sealed class PackageReferencePayload
    {
        public string Scope { get; set; } = ".";
    }
}
