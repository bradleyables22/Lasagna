using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Shared;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Namespaces;

[ContentInvocationPrefix("namespace")]
internal static class NamespaceRenamePanel
{
    internal const string HostId = "namespace-rename-panel";

    private const string ApplyDialogId = "namespace-rename-apply-confirmation";

    internal static class ActionIds
    {
        internal const string Preview = "namespace.preview";
        internal const string Apply = "namespace.apply";
    }

    internal static IHtmlContent Render(
        NamespaceRenamePayload? values = null,
        NamespaceRenamePlan? plan = null,
        string? message = null,
        bool isError = false)
    {
        values ??= new NamespaceRenamePayload();

        return FluentHtml.Div(panel =>
        {
            panel.Id(HostId)
                .Add(RenderForm(values));

            if (!string.IsNullOrWhiteSpace(message))
                panel.Add(PageFeedback.Render(message, isError));

            if (plan is not null)
                panel.Add(RenderPreview(plan));
        });
    }

    private static IHtmlContent RenderForm(NamespaceRenamePayload values)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Div(body =>
                {
                    body.Class("card-body")
                        .H2(h2 => h2.Class("h4", "mb-2").Text("Preview a namespace rename"))
                        .P(p => p.Class("text-body-secondary", "mb-4")
                            .Text("Scan an existing project before changing it. Preview never writes files; applying changes requires a separate confirmation."))
                        .Add(FluentHtml.Form(form =>
                        {
                            form.Id("namespace-rename-form")
                                .Heimdall(heimdall => heimdall
                                    .Submit(ActionIds.Preview)
                                    .PayloadFromClosestForm()
                                    .Target("#app")
                                    .SwapInner()
                                    .PreventDefault()
                                    .Disable());

                            form.Div(row =>
                            {
                                row.Class("row", "g-3")
                                    .Div(column => column.Class("col-lg-4")
                                        .Label(label => label
                                            .For("namespace-rename-from")
                                            .Class("form-label")
                                            .Text("From namespace"))
                                        .Input(Html.InputType.text, input => input
                                            .Id("namespace-rename-from")
                                            .Name("From")
                                            .Value(values.From)
                                            .Class("form-control", "font-monospace")
                                            .Placeholder("Thing.Api")
                                            .Required()))
                                    .Div(column => column.Class("col-lg-4")
                                        .Label(label => label
                                            .For("namespace-rename-to")
                                            .Class("form-label")
                                            .Text("To namespace"))
                                        .Input(Html.InputType.text, input => input
                                            .Id("namespace-rename-to")
                                            .Name("To")
                                            .Value(values.To)
                                            .Class("form-control", "font-monospace")
                                            .Placeholder("MyCompany.OrderApi")
                                            .Required()))
                                    .Div(column => column.Class("col-lg-4")
                                        .Label(label => label
                                            .For("namespace-rename-scope")
                                            .Class("form-label")
                                            .Text("Directory scope"))
                                        .Input(Html.InputType.text, input => input
                                            .Id("namespace-rename-scope")
                                            .Name("Scope")
                                            .Value(string.IsNullOrWhiteSpace(values.Scope) ? "." : values.Scope)
                                            .Class("form-control", "font-monospace")
                                            .Placeholder(".")
                                            .Required()));
                            });

                            form.Div(help => help.Class("form-text", "mt-3")
                                .Text("C#, Razor, and .cshtml files are scanned recursively. Build folders, generated files, comments, strings, project files, and package references are left alone."));

                            form.Button(button => button
                                .Type("submit")
                                .Class("btn", "btn-primary", "mt-3")
                                .Text("Preview changes"));
                        }));
                });
        });

    private static IHtmlContent RenderPreview(NamespaceRenamePlan plan)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline", "gap-3", "flex-wrap")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Git-style preview"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{plan.ChangedFiles.Count} file{(plan.ChangedFiles.Count == 1 ? string.Empty : "s")} changed")));
                })
                .Div(body =>
                {
                    body.Class("card-body")
                        .Div(summary => summary.Class("row", "g-3", "mb-4")
                            .Add(SummaryStat("From", plan.SourceNamespace))
                            .Add(SummaryStat("To", plan.TargetNamespace))
                            .Add(SummaryStat("Files scanned", plan.Files.Count.ToString()))
                            .Add(SummaryStat("Mappings", plan.ReferencesRewritten.ToString())));

                    if (plan.ChangedFiles.Count == 0)
                    {
                        body.Div(empty => empty.Class("alert", "alert-success", "mb-0")
                            .Attr("aria-live", "polite")
                            .Text("No matching namespace references were found in this scope."));
                        return;
                    }

                    body.Div(note => note.Class("alert", "alert-warning", "d-flex", "justify-content-between", "align-items-center", "gap-3", "flex-wrap")
                        .Div(copy => copy
                            .Strong(strong => strong.Text("Preview only. "))
                            .Span(span => span.Text("Review the changes below before applying them.")))
                        .Add(RenderApplyButton()));

                    body.Div(diff =>
                    {
                        diff.Class("lasagna-namespace-diff__tree");

                        var tree = BuildDiffTree(plan.RootPath, plan.ChangedFiles);

                        foreach (var file in tree.Files
                                     .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                        {
                            diff.Add(RenderFileDiff(file.File));
                        }

                        foreach (var folder in tree.Folders.Values
                                     .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            diff.Add(RenderDiffFolder(folder));
                        }
                    });

                    body.Add(RenderApplyDialog(plan));
                });
        });

    private static IHtmlContent SummaryStat(string label, string value)
        => FluentHtml.Div(column => column.Class("col-md-3")
            .Div(card => card.Class("border", "border-secondary-subtle", "rounded", "p-3", "h-100")
                .Div(title => title.Class("small", "text-body-secondary").Text(label))
                .Div(content => content.Class("font-monospace", "text-break").Text(value))));

    private static IHtmlContent RenderApplyButton()
        => FluentHtml.Button(button => button
            .Type("button")
            .Class("btn", "btn-warning")
            .CommandFor(ApplyDialogId)
            .Command(Html.CommandType.show_modal)
            .Text("Apply changes"));

    private static IHtmlContent RenderApplyDialog(NamespaceRenamePlan plan)
        => ConfirmationDialog.Render(
            ApplyDialogId,
            "Apply namespace changes?",
            $"This will update {plan.ChangedFiles.Count} file{(plan.ChangedFiles.Count == 1 ? string.Empty : "s")} under the selected scope. Review your source-control diff afterward.",
            FluentHtml.Form(form =>
        {
            form.Id("namespace-rename-apply-form")
                .Heimdall(heimdall => heimdall
                    .Submit(ActionIds.Apply)
                    .PayloadFromClosestForm()
                    .Target("#app")
                    .SwapInner()
                    .PreventDefault()
                    .Disable());

            form.Input(Html.InputType.hidden, input => input
                .Name("From")
                .Value(plan.SourceNamespace));
            form.Input(Html.InputType.hidden, input => input
                .Name("To")
                .Value(plan.TargetNamespace));
            form.Input(Html.InputType.hidden, input => input
                .Name("Scope")
                .Value(plan.RootPath));

            form.Button(confirm => confirm
                .Type("submit")
                .Class("btn", "btn-warning")
                .Text("Apply changes"));
        }));

    private static IHtmlContent RenderDiffFolder(DiffFolderNode folder)
        => FluentHtml.Tag("details", details =>
        {
            details.Class("lasagna-kit-tree__folder")
                .Tag("summary", summary => summary
                    .Class("lasagna-kit-tree__summary")
                    .Span(span => span.Class("font-monospace").Text(folder.Name))
                    .Add(Html.Tag(
                        "small",
                        Html.Attr("class", "text-body-secondary ms-2"),
                        Html.Text($"{folder.FileCount} file{(folder.FileCount == 1 ? string.Empty : "s")}"))))
                .Div(children =>
                {
                    children.Class("lasagna-kit-tree__children");

                    foreach (var file in folder.Files
                                 .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderFileDiff(file.File));
                    }

                    foreach (var child in folder.Folders.Values
                                 .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderDiffFolder(child));
                    }
                });
        });

    private static IHtmlContent RenderFileDiff(NamespaceRenameFile file)
        => FluentHtml.Tag("details", details =>
        {
            details.Class("lasagna-namespace-diff__file")
                .Tag("summary", summary => summary
                    .Class("lasagna-namespace-diff__summary")
                    .Span(span => span.Class("font-monospace").Text(Path.GetFileName(file.FilePath)))
                    .Add(Html.Tag(
                        "small",
                        Html.Attr("class", "text-body-secondary"),
                        Html.Text($"{file.Changes.Count} mapping{(file.Changes.Count == 1 ? string.Empty : "s")}"))))
                .Div(lines =>
                {
                    lines.Class("lasagna-namespace-diff__lines");

                    foreach (var line in BuildDiff(file.OriginalContent, file.Content))
                        lines.Add(RenderDiffLine(line));
                });
        });

    private static DiffFolderNode BuildDiffTree(
        string rootPath,
        IReadOnlyList<NamespaceRenameFile> files)
    {
        var root = new DiffFolderNode(string.Empty);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(rootPath, file.FilePath).Replace('\\', '/');
            var segments = relativePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (!current.Folders.TryGetValue(segments[index], out var child))
                {
                    child = new DiffFolderNode(segments[index]);
                    current.Folders.Add(segments[index], child);
                }

                current = child;
            }

            current.Files.Add(new DiffFile(relativePath, file));
        }

        return root;
    }

    private static IHtmlContent RenderDiffLine(DiffLine line)
        => FluentHtml.Div(row =>
        {
            row.Class("lasagna-namespace-diff__line", $"lasagna-namespace-diff__line--{line.Kind.ToString().ToLowerInvariant()}")
                .Span(oldNumber => oldNumber
                    .Class("lasagna-namespace-diff__number")
                    .Text(line.OldLineNumber?.ToString() ?? string.Empty))
                .Span(newNumber => newNumber
                    .Class("lasagna-namespace-diff__number")
                    .Text(line.NewLineNumber?.ToString() ?? string.Empty))
                .Span(code => code
                    .Class("lasagna-namespace-diff__code")
                    .Text($"{GetPrefix(line.Kind)}{line.Text}"));
        });

    private static IEnumerable<DiffLine> BuildDiff(string original, string rewritten)
    {
        var oldLines = SplitLines(original);
        var newLines = SplitLines(rewritten);
        var maxLength = Math.Max(oldLines.Length, newLines.Length);
        var changed = new HashSet<int>();

        for (var index = 0; index < maxLength; index++)
        {
            var oldLine = index < oldLines.Length ? oldLines[index] : null;
            var newLine = index < newLines.Length ? newLines[index] : null;

            if (!string.Equals(oldLine, newLine, StringComparison.Ordinal))
            {
                changed.Add(index);
                if (index > 0)
                    changed.Add(index - 1);
                if (index + 1 < maxLength)
                    changed.Add(index + 1);
            }
        }

        for (var index = 0; index < maxLength; index++)
        {
            if (!changed.Contains(index))
                continue;

            var oldLine = index < oldLines.Length ? oldLines[index] : null;
            var newLine = index < newLines.Length ? newLines[index] : null;

            if (oldLine is not null && newLine is not null &&
                string.Equals(oldLine, newLine, StringComparison.Ordinal))
            {
                yield return new DiffLine(DiffKind.Context, index + 1, index + 1, oldLine);
                continue;
            }

            if (oldLine is not null)
                yield return new DiffLine(DiffKind.Removed, index + 1, null, oldLine);

            if (newLine is not null)
                yield return new DiffLine(DiffKind.Added, null, index + 1, newLine);
        }
    }

    private static string[] SplitLines(string content)
    {
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        return lines.Length > 1 && lines[^1].Length == 0
            ? lines[..^1]
            : lines;
    }

    private static string GetPrefix(DiffKind kind) => kind switch
    {
        DiffKind.Removed => "- ",
        DiffKind.Added => "+ ",
        _ => "  "
    };

    private sealed record DiffLine(
        DiffKind Kind,
        int? OldLineNumber,
        int? NewLineNumber,
        string Text);

    private enum DiffKind
    {
        Context,
        Removed,
        Added
    }

    private sealed record DiffFile(
        string RelativePath,
        NamespaceRenameFile File);

    private sealed class DiffFolderNode(string name)
    {
        internal string Name { get; } = name;

        internal Dictionary<string, DiffFolderNode> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<DiffFile> Files { get; } = [];

        internal int FileCount => Files.Count + Folders.Values.Sum(folder => folder.FileCount);
    }

    [ContentInvocation("preview")]
    public static IHtmlContent Preview([ContentPayload] NamespaceRenamePayload payload)
    {
        try
        {
            var plan = NamespaceRenameManager.BuildPlan(
                payload.Scope,
                payload.From,
                payload.To);

            return NamespaceRenamePage.Render(payload, plan);
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return NamespaceRenamePage.Render(payload, message: exception.Message, isError: true);
        }
    }

    [ContentInvocation("apply")]
    public static IHtmlContent Apply([ContentPayload] NamespaceRenamePayload payload)
    {
        try
        {
            var plan = NamespaceRenameManager.BuildPlan(
                payload.Scope,
                payload.From,
                payload.To);
            NamespaceRenameManager.Apply(plan);

            return NamespaceRenamePage.Render(
                payload,
                message: $"Applied namespace rename to {plan.ChangedFiles.Count} file{(plan.ChangedFiles.Count == 1 ? string.Empty : "s")}.");
        }
        catch (Exception exception) when (IsExpectedFailure(exception))
        {
            return NamespaceRenamePage.Render(payload, message: exception.Message, isError: true);
        }
    }

    private static bool IsExpectedFailure(Exception exception) => exception is
        ArgumentException or
        DirectoryNotFoundException or
        FileNotFoundException or
        IOException or
        InvalidDataException or
        UnauthorizedAccessException;

    public sealed class NamespaceRenamePayload
    {
        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public string Scope { get; set; } = ".";
    }
}
