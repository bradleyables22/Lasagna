using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Shared;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Shake;

[ContentInvocationPrefix("shake")]
internal static class ShakePanel
{
    internal const string HostId = "shake-panel";

    private const string ApplyDialogId = "shake-apply-confirmation";

    internal static class ActionIds
    {
        internal const string Scan = "shake.scan";
        internal const string Apply = "shake.apply";
    }

    internal static IHtmlContent Render(
        string scope = ".",
        ShakePlan? plan = null,
        string? message = null,
        bool isError = false)
        => FluentHtml.Div(panel =>
        {
            panel.Id(HostId)
                .Add(RenderForm(scope));

            if (!string.IsNullOrWhiteSpace(message))
                panel.Add(PageFeedback.Render(message, isError));

            if (plan is not null)
                panel.Add(RenderPlan(plan));
        });

    private static IHtmlContent RenderForm(string scope)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Div(body =>
                {
                    body.Class("card-body")
                        .H2(h2 => h2.Class("h4", "mb-2").Text("Shake unused local source"))
                        .P(p => p.Class("text-body-secondary", "mb-4")
                            .Text("Scan the current codebase recursively, show the references that protect each file, and remove only high-confidence unused C# files after confirmation."))
                        .Add(FluentHtml.Form(form =>
                        {
                            form.Id("shake-form")
                                .Heimdall(heimdall => heimdall
                                    .Submit(ActionIds.Scan)
                                    .PayloadFromClosestForm()
                                    .Target($"#{HostId}")
                                    .SwapOuter()
                                    .PreventDefault()
                                    .Disable());

                            form.Div(row => row.Class("row", "g-3", "align-items-end")
                                .Div(column => column.Class("col-lg-6")
                                    .Label(label => label
                                        .For("shake-scope")
                                        .Class("form-label")
                                        .Text("Directory scope"))
                                    .Input(Html.InputType.text, input => input
                                        .Id("shake-scope")
                                        .Name("Scope")
                                        .Value(string.IsNullOrWhiteSpace(scope) ? "." : scope)
                                        .Class("form-control", "font-monospace")
                                        .Placeholder("."))
                                    .Div(help => help.Class("form-text")
                                        .Text("Defaults to the folder where Lasagna was launched; scanning is recursive.")))
                                .Div(column => column.Class("col-lg-4")
                                    .Label(label => label
                                        .For("shake-project")
                                        .Class("form-label")
                                        .Text("Build project (optional)"))
                                    .Input(Html.InputType.text, input => input
                                        .Id("shake-project")
                                        .Name("Project")
                                        .Class("form-control", "font-monospace")
                                        .Placeholder("ClaimsApi.csproj"))
                                    .Div(help => help.Class("form-text")
                                        .Text("Used after applying changes; one project is auto-detected when possible.")))
                                .Div(column => column.Class("col-lg-2")
                                    .Button(button => button
                                        .Type("submit")
                                        .Class("btn", "btn-primary", "w-100")
                                        .Text("Scan"))));

                            form.Div(help => help.Class("form-text", "mt-3")
                                .Text("Only local .cs source files are candidates. Build output, generated files, packages, and framework assemblies are excluded."));
                        }));
                });
        });

    private static IHtmlContent RenderPlan(ShakePlan plan)
        => FluentHtml.Fragment(fragment =>
        {
            fragment.Add(RenderSummary(plan));

            if (plan.RemovableFiles.Count > 0)
                fragment.Add(RenderCandidateSection(plan));
            else
                fragment.Add(RenderEmptyCandidates());

            if (plan.ReviewFiles.Count > 0)
                fragment.Add(RenderEvidenceSection(
                    "Review required",
                    "These files have no local semantic references, but their public, partial, attributed, or indirect-use indicators make automatic removal unsafe.",
                    plan.ReviewFiles,
                    plan.RootPath,
                    "border-warning"));

            if (plan.ReferencedFiles.Count > 0)
                fragment.Add(RenderEvidenceSection(
                    "Referenced files kept",
                    "These files have local semantic references. Each reference below is evidence for keeping the file.",
                    plan.ReferencedFiles,
                    plan.RootPath,
                    "border-secondary"));

            if (plan.ProtectedFiles.Count > 0)
                fragment.Add(RenderEvidenceSection(
                    "Protected files",
                    "These files were outside the safe removal rules, such as files without a discovered top-level type or application entry points.",
                    plan.ProtectedFiles,
                    plan.RootPath,
                    "border-secondary"));

            if (plan.Issues.Count > 0)
                fragment.Add(RenderIssues(plan.Issues));
        });

    private static IHtmlContent RenderSummary(ShakePlan plan)
        => FluentHtml.Div(row => row.Class("row", "g-3", "mb-4")
            .Add(SummaryCard("C# files", plan.FilesScanned.ToString(), "Source files scanned recursively."))
            .Add(SummaryCard("Candidates", plan.RemovableFiles.Count.ToString(), "Files eligible after reference checks."))
            .Add(SummaryCard("Review", plan.ReviewFiles.Count.ToString(), "Files that need human review."))
            .Add(SummaryCard("References", plan.ReferenceCount.ToString(), "Local semantic references found.")));

    private static IHtmlContent SummaryCard(string label, string value, string description)
        => FluentHtml.Div(column => column.Class("col-sm-6", "col-xl-3")
            .Div(card => card.Class("card", "h-100", "shadow-sm")
                .Div(body => body.Class("card-body")
                    .Div(labelBlock => labelBlock.Class("small", "text-body-secondary").Text(label))
                    .Div(valueBlock => valueBlock.Class("display-6", "fw-semibold", "font-monospace").Text(value))
                    .P(p => p.Class("small", "text-body-secondary", "mb-0").Text(description)))));

    private static IHtmlContent RenderCandidateSection(ShakePlan plan)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-center", "gap-3", "flex-wrap")
                    .Div(copy => copy
                        .H2(h2 => h2.Class("h4", "mb-1").Text("Candidates eligible for removal"))
                        .P(p => p.Class("text-body-secondary", "mb-0")
                            .Text("Every candidate below has zero local semantic references and zero indirect-use hints.")))
                    .Add(RenderApplyButton(plan)))
                .Div(body => body.Class("card-body", "p-3")
                    .Div(list =>
                    {
                        list.Class("d-grid", "gap-2");
                        foreach (var file in plan.RemovableFiles)
                            list.Add(RenderFileEvidence(file, plan.RootPath, includeReferences: false));
                    })
                    .Add(RenderApplyDialog(plan)));
        });

    private static IHtmlContent RenderEmptyCandidates()
        => FluentHtml.Article(article => article
            .Class("card", "shadow-sm", "mb-4")
            .Div(body => body.Class("card-body")
                .H2(h2 => h2.Class("h4", "mb-2").Text("No automatic removals"))
                .P(p => p.Class("text-body-secondary", "mb-0")
                    .Text("No file met the high-confidence removal rules in this scope."))));

    private static IHtmlContent RenderEvidenceSection(
        string title,
        string description,
        IReadOnlyList<ShakeFileReport> files,
        string rootPath,
        string borderClass)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-1").Text(title))
                    .P(p => p.Class("text-body-secondary", "mb-0").Text(description)))
                .Div(body => body.Class("card-body", "p-3")
                    .Div(list =>
                    {
                        list.Class("d-grid", "gap-2");
                        foreach (var file in files)
                            list.Add(RenderFileEvidence(file, rootPath, includeReferences: true, borderClass));
                    }));
        });

    private static IHtmlContent RenderFileEvidence(
        ShakeFileReport file,
        string rootPath,
        bool includeReferences,
        string borderClass = "border-secondary")
        => FluentHtml.Tag("details", details =>
        {
            details.Class("border", borderClass, "rounded", "p-3")
                .Tag("summary", summary => summary
                    .Class("d-flex", "justify-content-between", "align-items-baseline", "gap-3", "flex-wrap")
                    .Span(span => span.Class("font-monospace", "text-break").Text(RelativePath(rootPath, file.FilePath)))
                    .Add(Html.Tag("small", Html.Attr("class", "text-body-secondary"), Html.Text(file.Status.ToString()))))
                .Div(content =>
                {
                    content.Class("mt-3")
                        .P(p => p.Class("small", "text-body-secondary").Text(file.Reason));

                    foreach (var symbol in file.Symbols)
                    {
                        content.Div(symbolBlock =>
                        {
                            symbolBlock.Class("border-top", "pt-3", "mt-3")
                                .Div(title => title
                                    .Class("d-flex", "justify-content-between", "gap-3", "flex-wrap")
                                    .Strong(strong => strong.Text(symbol.DisplayName))
                                    .Add(Html.Tag(
                                        "small",
                                        Html.Attr("class", "text-body-secondary"),
                                        Html.Text($"line {symbol.DeclarationLine} · {symbol.Accessibility}"))));

                            if (!includeReferences)
                            {
                                symbolBlock.Div(proof => proof.Class("small", "mt-2")
                                    .Div(line => line
                                        .Text("Local semantic references: ")
                                        .Strong(strong => strong.Text("0")))
                                    .Div(line => line
                                        .Text("Indirect-use hints: ")
                                        .Strong(strong => strong.Text("0"))));
                                return;
                            }

                            symbolBlock.Add(RenderReferences("Local semantic references", symbol.References, rootPath));
                            symbolBlock.Add(RenderReferences("Indirect-use hints", symbol.IndirectHints, rootPath));
                        });
                    }
                });
        });

    private static IHtmlContent RenderReferences(
        string title,
        IReadOnlyList<ShakeReference> references,
        string rootPath)
        => FluentHtml.Div(block =>
        {
            block.Class("mt-3")
                .Div(label => label.Class("small", "fw-semibold").Text(title));

            if (references.Count == 0)
            {
                block.Div(empty => empty.Class("small", "text-body-secondary").Text("None found."));
                return;
            }

            block.Ul(list =>
            {
                list.Class("small", "mb-0", "ps-4");
                foreach (var reference in references)
                {
                    list.Li(item => item
                        .Span(span => span.Class("font-monospace").Text(
                            $"{RelativePath(rootPath, reference.FilePath)}:{reference.Line}"))
                        .Text($" — {reference.Reason}"));
                }
            });
        });

    private static IHtmlContent RenderIssues(IReadOnlyList<string> issues)
        => FluentHtml.Article(article => article
            .Class("card", "shadow-sm", "mb-4", "border-warning")
            .Div(body => body.Class("card-body")
                .H2(h2 => h2.Class("h5", "mb-2").Text("Scan notes"))
                .Ul(list =>
                {
                    list.Class("mb-0");
                    foreach (var issue in issues)
                        list.Li(item => item.Text(issue));
                })));

    private static IHtmlContent RenderApplyButton(ShakePlan plan)
        => FluentHtml.Button(button => button
            .Type("button")
            .Class("btn", "btn-warning")
            .CommandFor(ApplyDialogId)
            .Command(Html.CommandType.show_modal)
            .Text($"Remove {plan.RemovableFiles.Count} candidate{(plan.RemovableFiles.Count == 1 ? string.Empty : "s")}"));

    private static IHtmlContent RenderApplyDialog(ShakePlan plan)
        => ConfirmationDialog.Render(
            ApplyDialogId,
            "Remove unused source files?",
            $"This will delete {plan.RemovableFiles.Count} local C# file{(plan.RemovableFiles.Count == 1 ? string.Empty : "s")}. The project will be built afterward; if the build fails, the files are restored.",
            FluentHtml.Form(form =>
            {
                form.Id("shake-apply-form")
                    .Heimdall(heimdall => heimdall
                        .Submit(ActionIds.Apply)
                        .PayloadFromClosestForm()
                        .Target($"#{HostId}")
                        .SwapOuter()
                        .PreventDefault()
                        .Disable());

                form.Input(Html.InputType.hidden, input => input
                    .Name("Scope")
                    .Value(plan.RootPath));
                form.Input(Html.InputType.hidden, input => input
                    .Name("Project")
                    .Value(plan.BuildProjectPath ?? string.Empty));

                form.Button(confirm => confirm
                    .Type("submit")
                    .Class("btn", "btn-warning")
                    .Text("Remove and build"));
            }));

    [ContentInvocation("scan")]
    public static IHtmlContent Scan([ContentPayload] ShakePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            var plan = ShakeAnalyzer.BuildPlan(scope, payload.Project);
            return ShakePage.Render(scope, plan);
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return ShakePage.Render(payload.Scope, message: exception.Message, isError: true);
        }
    }

    [ContentInvocation("apply")]
    public static IHtmlContent Apply([ContentPayload] ShakePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            var plan = ShakeAnalyzer.BuildPlan(scope, payload.Project);
            var result = ShakeAnalyzer.Apply(plan);

            return ShakePage.Render(
                scope,
                message: $"Removed {result.FilesRemoved} source file{(result.FilesRemoved == 1 ? string.Empty : "s")}; the build passed.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return ShakePage.Render(payload.Scope, message: exception.Message, isError: true);
        }
    }

    private static bool IsExpectedFailure(Exception exception) => exception is
        ArgumentException or
        DirectoryNotFoundException or
        FileNotFoundException or
        IOException or
        InvalidDataException or
        InvalidOperationException or
        TimeoutException or
        UnauthorizedAccessException;

    private static string RelativePath(string rootPath, string path) =>
        Path.GetRelativePath(rootPath, path).Replace('\\', '/');

    public sealed class ShakePayload
    {
        public string Scope { get; set; } = ".";

        public string? Project { get; set; }
    }
}
