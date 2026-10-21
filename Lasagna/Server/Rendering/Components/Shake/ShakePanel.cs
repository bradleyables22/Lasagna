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
    private const string FeedbackHostId = "shake-feedback";
    private const string SummaryHostId = "shake-summary";
    private const string CandidatesHostId = "shake-candidates";
    private const string CandidateActionsHostId = "shake-candidate-actions";
    private const string ReviewHostId = "shake-review";
    private const string ReferencedHostId = "shake-referenced";
    private const string ProtectedHostId = "shake-protected";

    internal static class ActionIds
    {
        internal const string Scan = "shake.scan";
        internal const string Apply = "shake.apply";
        internal const string Remove = "shake.remove";
        internal const string RemoveDirectory = "shake.remove-directory";
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

            panel.Div(feedback => feedback
                .Id(FeedbackHostId)
                .Add(string.IsNullOrWhiteSpace(message)
                    ? Empty()
                    : PageFeedback.Render(message, isError)));

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
                                    .Target("#app")
                                    .SwapInner()
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
                                .Div(column => column.Class("col-lg-6")
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
                    "These files have no local semantic references, but their public, partial, attributed, polymorphic, or indirect-use indicators make automatic removal unsafe.",
                    plan.ReviewFiles,
                    plan,
                    ReviewHostId,
                    "border-warning",
                    approveReview: true));

            if (plan.ReferencedFiles.Count > 0)
                fragment.Add(RenderEvidenceSection(
                    "Referenced files kept",
                    "These files have local semantic references. Each reference below is evidence for keeping the file.",
                    plan.ReferencedFiles,
                    plan,
                    ReferencedHostId,
                    "border-secondary",
                    approveReview: false));

            if (plan.ProtectedFiles.Count > 0)
                fragment.Add(RenderEvidenceSection(
                    "Protected files",
                    "These files were outside the safe removal rules, such as files without a discovered top-level type or application entry points.",
                    plan.ProtectedFiles,
                    plan,
                    ProtectedHostId,
                    "border-secondary",
                    approveReview: false));

            if (plan.Issues.Count > 0)
                fragment.Add(RenderIssues(plan.Issues));
        });

    private static IHtmlContent RenderSummary(ShakePlan plan)
        => FluentHtml.Div(row => row.Class("row", "g-3", "mb-4")
            .Id(SummaryHostId)
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
            article.Id(CandidatesHostId)
                .Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-center", "gap-3", "flex-wrap")
                    .Div(copy => copy
                        .H2(h2 => h2.Class("h4", "mb-1").Text("Candidates eligible for removal"))
                        .P(p => p.Class("text-body-secondary", "mb-0")
                            .Text("Every candidate below has zero local semantic references and zero indirect-use hints.")))
                    .Div(actions => actions
                        .Id(CandidateActionsHostId)
                        .Add(RenderApplyButton(plan))))
                .Div(body => body.Class("card-body", "p-3")
                    .Add(RenderEvidenceTree(
                        plan.RemovableFiles,
                        plan,
                        includeReferences: false,
                        approveReview: false))
                    .Add(RenderApplyDialog(plan)));
        });

    private static IHtmlContent RenderEmptyCandidates()
        => FluentHtml.Article(article => article
            .Id(CandidatesHostId)
            .Class("card", "shadow-sm", "mb-4")
            .Div(body => body.Class("card-body")
                .H2(h2 => h2.Class("h4", "mb-2").Text("No automatic removals"))
                .P(p => p.Class("text-body-secondary", "mb-0")
                    .Text("No file met the high-confidence removal rules in this scope."))));

    private static IHtmlContent RenderEvidenceSection(
        string title,
        string description,
        IReadOnlyList<ShakeFileReport> files,
        ShakePlan plan,
        string sectionId,
        string borderClass,
        bool approveReview)
        => FluentHtml.Article(article =>
        {
            article.Id(sectionId)
                .Class("card", "shadow-sm", "mb-4")
                .Header(header => header.Class("card-header", "bg-body-tertiary")
                    .H2(h2 => h2.Class("h4", "mb-1").Text(title))
                    .P(p => p.Class("text-body-secondary", "mb-0").Text(description)))
                .Div(body => body.Class("card-body", "p-3")
                    .Add(RenderEvidenceTree(
                        files,
                        plan,
                        includeReferences: true,
                        approveReview,
                        borderClass: borderClass)));
        });

    private static IHtmlContent RenderEvidenceTree(
        IReadOnlyList<ShakeFileReport> files,
        ShakePlan plan,
        bool includeReferences,
        bool approveReview,
        string borderClass = "border-secondary")
    {
        var tree = BuildEvidenceTree(plan.RootPath, files);

        return FluentHtml.Div(treeHost =>
        {
            treeHost.Class("lasagna-kit-tree", "d-grid", "gap-2");

            foreach (var file in tree.Files
                         .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                treeHost.Add(RenderEvidenceFile(
                    file.Report,
                    plan,
                    includeReferences,
                    approveReview,
                    borderClass));
            }

            foreach (var folder in tree.Folders.Values
                         .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                treeHost.Add(RenderEvidenceFolder(
                    folder,
                    plan,
                    includeReferences,
                    approveReview,
                    borderClass));
            }
        });
    }

    private static IHtmlContent RenderEvidenceFile(
        ShakeFileReport file,
        ShakePlan plan,
        bool includeReferences,
        bool approveReview,
        string borderClass = "border-secondary")
    {
        var canRemove = file.Status == ShakeFileStatus.Removable ||
            (approveReview && file.Status == ShakeFileStatus.Review);
        var dialogId = canRemove
            ? ConfirmationDialog.CreateId("shake-remove", file.FilePath)
            : null;
        var actionLabel = file.Status == ShakeFileStatus.Review
            ? "Approve deletion"
            : "Remove this file";

        return FluentHtml.Div(host =>
        {
            host.Id(EvidenceFileHostId(file.FilePath))
                .Add(RenderFileEvidence(
                file,
                plan.RootPath,
                includeReferences,
                borderClass,
                removeDialogId: dialogId,
                removeButtonText: actionLabel));

            if (dialogId is not null)
            {
                host.Add(RenderSingleRemoveDialog(
                    file,
                    plan,
                    dialogId,
                    approveReview: file.Status == ShakeFileStatus.Review));
            }
        });
    }

    private static IHtmlContent RenderEvidenceFolder(
        EvidenceFolderNode folder,
        ShakePlan plan,
        bool includeReferences,
        bool approveReview,
        string borderClass)
    {
        var directoryPath = Path.Combine(
            plan.RootPath,
            folder.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var canRemove = CanOfferDirectoryRemoval(plan, directoryPath, approveReview);
        var dialogId = canRemove
            ? ConfirmationDialog.CreateId("shake-remove-directory", directoryPath)
            : null;

        return FluentHtml.Tag("details", details =>
        {
            details.Id(EvidenceFolderHostId(directoryPath))
                .Class("lasagna-kit-tree__folder")
                .Tag("summary", summary => summary
                    .Class("lasagna-kit-tree__summary")
                    .Span(span => span.Class("font-monospace").Text(folder.Name))
                    .Add(Html.Tag(
                        "small",
                        Html.Attr("class", "text-body-secondary ms-2"),
                        Html.Text($"{folder.FileCount} file{(folder.FileCount == 1 ? string.Empty : "s")}"))))
                .Div(children =>
                {
                    children.Class("lasagna-kit-tree__children", "d-grid", "gap-2");

                    if (dialogId is not null)
                    {
                        children.Div(actions => actions
                            .Class("d-flex", "justify-content-end", "gap-2", "mb-2")
                            .Add(ConfirmationDialog.Trigger(
                                dialogId,
                                approveReview ? "Approve & remove folder" : "Remove folder",
                                "btn",
                                "btn-outline-danger",
                                "btn-sm")));
                    }

                    foreach (var file in folder.Files
                                 .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderEvidenceFile(
                            file.Report,
                            plan,
                            includeReferences,
                            approveReview,
                            borderClass));
                    }

                    foreach (var child in folder.Folders.Values
                                 .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderEvidenceFolder(
                            child,
                            plan,
                            includeReferences,
                            approveReview,
                            borderClass));
                    }
                });

            if (dialogId is not null)
            {
                details.Add(RenderDirectoryRemoveDialog(
                    plan,
                    folder,
                    dialogId,
                    approveReview));
            }
        });
    }

    private static bool CanOfferDirectoryRemoval(
        ShakePlan plan,
        string directoryPath,
        bool approveReview)
    {
        var reports = plan.Files
            .Where(file => IsDescendantPath(directoryPath, file.FilePath))
            .ToArray();

        return reports.Length > 0 && reports.All(file =>
            approveReview
                ? file.Status == ShakeFileStatus.Review
                : file.Status == ShakeFileStatus.Removable);
    }

    private static IHtmlContent RenderFileEvidence(
        ShakeFileReport file,
        string rootPath,
        bool includeReferences,
        string borderClass = "border-secondary",
        string? removeDialogId = null,
        string removeButtonText = "Remove this file")
        => FluentHtml.Tag("details", details =>
        {
            details.Class("border", borderClass, "rounded", "p-3")
                .Tag("summary", summary => summary
                    .Class("d-flex", "justify-content-between", "align-items-baseline", "gap-3", "flex-wrap")
                    .Span(span => span.Class("font-monospace", "text-break").Text(RelativePath(rootPath, file.FilePath)))
                    .Add(RenderFileStatus(file)))
                .Div(content =>
                {
                    content.Class("mt-3")
                        .P(p => p.Class("small", "text-body-secondary").Text(file.Reason));

                    if (removeDialogId is not null)
                    {
                        content.Div(actions => actions
                            .Class("d-flex", "justify-content-end", "mt-3")
                            .Add(ConfirmationDialog.Trigger(
                                removeDialogId,
                                removeButtonText,
                                "btn",
                                "btn-outline-danger",
                                "btn-sm")));
                    }

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

    private static IHtmlContent RenderFileStatus(ShakeFileReport file)
        => FluentHtml.Fragment(fragment =>
        {
            fragment.Add(Html.Tag(
                "small",
                Html.Attr("class", "text-body-secondary"),
                Html.Text(file.Status.ToString())));

            if (file.Status == ShakeFileStatus.Review && file.IndirectHintCount > 0)
            {
                fragment.Add(Html.Tag(
                    "small",
                    Html.Attr("class", "text-warning-emphasis"),
                    Html.Text($"{file.IndirectHintCount} potential indirect usage{(file.IndirectHintCount == 1 ? string.Empty : "s")}")));
            }
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
            $"This will directly delete {plan.RemovableFiles.Count} local C# file{(plan.RemovableFiles.Count == 1 ? string.Empty : "s")}. No build or restore will run.",
            FluentHtml.Form(form =>
            {
                form.Id("shake-apply-form");

                form.Input(Html.InputType.hidden, input => input
                    .Name("Scope")
                    .Value(plan.RootPath));
                form.Button(confirm => confirm
                    .Type("button")
                    .Class("btn", "btn-warning")
                    .Heimdall(heimdall => heimdall
                        .Click(ActionIds.Apply)
                        .PayloadFromClosestForm()
                        .Target($"#{HostId}")
                        .SwapNone()
                        .PreventDefault()
                        .Disable())
                    .Text("Remove candidates"));
            }));

    private static IHtmlContent RenderSingleRemoveDialog(
        ShakeFileReport file,
        ShakePlan plan,
        string dialogId,
        bool approveReview)
        => ConfirmationDialog.Render(
            dialogId,
            approveReview ? "Approve deletion?" : "Remove this source file?",
            approveReview
                ? $"This will approve and directly delete {RelativePath(plan.RootPath, file.FilePath)}. No build or restore will run."
                : $"This will directly delete {RelativePath(plan.RootPath, file.FilePath)}. No build or restore will run.",
            FluentHtml.Form(form =>
            {
                form.Id($"{dialogId}-form");

                form.Input(Html.InputType.hidden, input => input
                    .Name("Scope")
                    .Value(plan.RootPath));
                form.Input(Html.InputType.hidden, input => input
                    .Name("FilePath")
                    .Value(file.FilePath));
                form.Input(Html.InputType.hidden, input => input
                    .Name("ApproveReview")
                    .Value(approveReview ? "true" : "false"));

                form.Button(confirm => confirm
                    .Type("button")
                    .Class("btn", "btn-danger")
                    .Heimdall(heimdall => heimdall
                        .Click(ActionIds.Remove)
                        .PayloadFromClosestForm()
                        .Target($"#{HostId}")
                        .SwapNone()
                        .PreventDefault()
                        .Disable())
                    .Text(approveReview ? "Approve and remove" : "Remove file"));
            }));

    private static IHtmlContent RenderDirectoryRemoveDialog(
        ShakePlan plan,
        EvidenceFolderNode folder,
        string dialogId,
        bool approveReview)
        => ConfirmationDialog.Render(
            dialogId,
            approveReview ? "Approve directory deletion?" : "Remove this directory?",
            approveReview
                ? $"This will approve and directly delete the entire '{folder.RelativePath}' directory and its reviewed children. No build or restore will run."
                : $"This will directly delete the entire '{folder.RelativePath}' directory and its unused children. No build or restore will run.",
            FluentHtml.Form(form =>
            {
                form.Id($"{dialogId}-form");

                form.Input(Html.InputType.hidden, input => input
                    .Name("Scope")
                    .Value(plan.RootPath));
                form.Input(Html.InputType.hidden, input => input
                    .Name("DirectoryPath")
                    .Value(Path.Combine(
                        plan.RootPath,
                        folder.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
                form.Input(Html.InputType.hidden, input => input
                    .Name("ApproveReview")
                    .Value(approveReview ? "true" : "false"));

                form.Button(confirm => confirm
                    .Type("button")
                    .Class("btn", "btn-danger")
                    .Heimdall(heimdall => heimdall
                        .Click(ActionIds.RemoveDirectory)
                        .PayloadFromClosestForm()
                        .Target($"#{HostId}")
                        .SwapNone()
                        .PreventDefault()
                        .Disable())
                    .Text(approveReview ? "Approve and remove folder" : "Remove folder"));
            }));

    [ContentInvocation("scan")]
    public static IHtmlContent Scan([ContentPayload] ShakePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            var plan = ShakeAnalyzer.BuildPlan(scope);
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
            var plan = ShakeAnalyzer.BuildPlan(scope);
            var result = ShakeAnalyzer.Apply(plan);
            var refreshedPlan = ShakeAnalyzer.BuildPlan(scope);

            return RenderRemovalResponse(
                refreshedPlan,
                removedSelector: null,
                message: $"Removed {result.FilesRemoved} source file{(result.FilesRemoved == 1 ? string.Empty : "s")}; no build or restore was run.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return RenderActionError(exception.Message);
        }
    }

    [ContentInvocation("remove")]
    public static IHtmlContent Remove([ContentPayload] ShakePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            var plan = ShakeAnalyzer.BuildPlan(scope);
            var result = ShakeAnalyzer.ApplyFile(plan, payload.FilePath, payload.ApproveReview);
            var refreshedPlan = ShakeAnalyzer.BuildPlan(scope);

            return RenderRemovalResponse(
                refreshedPlan,
                $"#{EvidenceFileHostId(payload.FilePath)}",
                payload.ApproveReview
                    ? $"Approved and removed {Path.GetFileName(payload.FilePath)} ({result.FilesRemoved} file); no build or restore was run."
                    : $"Removed {Path.GetFileName(payload.FilePath)} ({result.FilesRemoved} file); no build or restore was run.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return RenderActionError(exception.Message);
        }
    }

    [ContentInvocation("remove-directory")]
    public static IHtmlContent RemoveDirectory([ContentPayload] ShakePayload payload)
    {
        try
        {
            var scope = string.IsNullOrWhiteSpace(payload.Scope) ? "." : payload.Scope;
            var plan = ShakeAnalyzer.BuildPlan(scope);
            var result = ShakeAnalyzer.ApplyDirectory(
                plan,
                payload.DirectoryPath,
                payload.ApproveReview);
            var refreshedPlan = ShakeAnalyzer.BuildPlan(scope);

            return RenderRemovalResponse(
                refreshedPlan,
                $"#{EvidenceFolderHostId(payload.DirectoryPath)}",
                payload.ApproveReview
                    ? $"Approved and removed the directory ({result.FilesRemoved} file{(result.FilesRemoved == 1 ? string.Empty : "s")}); no build or restore was run."
                    : $"Removed the directory ({result.FilesRemoved} file{(result.FilesRemoved == 1 ? string.Empty : "s")}); no build or restore was run.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return RenderActionError(exception.Message);
        }
    }

    private static IHtmlContent RenderRemovalResponse(
        ShakePlan refreshedPlan,
        string? removedSelector,
        string message)
        => FluentHtml.Fragment(fragment =>
        {
            if (!string.IsNullOrWhiteSpace(removedSelector))
            {
                fragment.Heimdall().Invocation(
                    removedSelector,
                    HeimdallHtml.Swap.Outer,
                    Empty());
            }

            fragment.Heimdall().Invocation(
                $"#{SummaryHostId}",
                HeimdallHtml.Swap.Outer,
                RenderSummary(refreshedPlan));
            fragment.Heimdall().Invocation(
                $"#{FeedbackHostId}",
                HeimdallHtml.Swap.Inner,
                PageFeedback.Render(message, isError: false));

            if (refreshedPlan.RemovableFiles.Count == 0)
            {
                fragment.Heimdall().Invocation(
                    $"#{CandidatesHostId}",
                    HeimdallHtml.Swap.Outer,
                    RenderEmptyCandidates());
            }
            else
            {
                fragment.Heimdall().Invocation(
                    $"#{CandidateActionsHostId}",
                    HeimdallHtml.Swap.Inner,
                    RenderApplyButton(refreshedPlan));
            }

            if (refreshedPlan.ReviewFiles.Count == 0)
            {
                fragment.Heimdall().Invocation(
                    $"#{ReviewHostId}",
                    HeimdallHtml.Swap.Outer,
                    Empty());
            }

            fragment.Heimdall().Abort("shake-removal-updated");
        });

    private static IHtmlContent RenderActionError(string message)
        => FluentHtml.Fragment(fragment =>
        {
            fragment.Heimdall().Invocation(
                $"#{FeedbackHostId}",
                HeimdallHtml.Swap.Inner,
                PageFeedback.Render(message, isError: true));
            fragment.Heimdall().Abort("shake-action-failed");
        });

    private static IHtmlContent Empty() => new HtmlString(string.Empty);

    private static string EvidenceFileHostId(string filePath) =>
        ConfirmationDialog.CreateId("shake-file", Path.GetFullPath(filePath));

    private static string EvidenceFolderHostId(string directoryPath) =>
        ConfirmationDialog.CreateId("shake-folder", Path.GetFullPath(directoryPath));

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

    private static bool IsDescendantPath(string rootPath, string path)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);

        return relativePath != "." &&
               !Path.IsPathRooted(relativePath) &&
               !relativePath.Equals("..", StringComparison.Ordinal) &&
               !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
               !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static EvidenceFolderNode BuildEvidenceTree(
        string rootPath,
        IReadOnlyList<ShakeFileReport> files)
    {
        var root = new EvidenceFolderNode(string.Empty, string.Empty);

        foreach (var file in files)
        {
            var relativePath = RelativePath(rootPath, file.FilePath);
            var segments = relativePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            for (var index = 0; index < segments.Length - 1; index++)
            {
                var relativeFolder = string.Join('/', segments.Take(index + 1));

                if (!current.Folders.TryGetValue(segments[index], out var child))
                {
                    child = new EvidenceFolderNode(segments[index], relativeFolder);
                    current.Folders.Add(segments[index], child);
                }

                current = child;
            }

            current.Files.Add(new EvidenceFileNode(relativePath, file));
        }

        return root;
    }

    private sealed record EvidenceFileNode(
        string RelativePath,
        ShakeFileReport Report);

    private sealed class EvidenceFolderNode(
        string name,
        string relativePath)
    {
        internal string Name { get; } = name;

        internal string RelativePath { get; } = relativePath;

        internal Dictionary<string, EvidenceFolderNode> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<EvidenceFileNode> Files { get; } = [];

        internal int FileCount => Files.Count + Folders.Values.Sum(folder => folder.FileCount);
    }

    public sealed class ShakePayload
    {
        public string Scope { get; set; } = ".";


        public string FilePath { get; set; } = string.Empty;

        public string DirectoryPath { get; set; } = string.Empty;

        public bool ApproveReview { get; set; }
    }
}
