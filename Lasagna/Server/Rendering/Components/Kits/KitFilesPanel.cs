using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Kits;

[ContentInvocationPrefix("kits.files")]
internal static class KitFilesPanel
{
    internal static class ActionIds
    {
        internal const string Add = "kits.files.add";
        internal const string Remove = "kits.files.remove";
    }

    internal static IHtmlContent Render(string kitName)
    {
        var kitPath = KitManager.GetKitPath(kitName);
        var files = KitManager.ReadFiles(kitName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new StoredFile(
                Path.GetRelativePath(kitPath, path).Replace('\\', '/')))
            .ToArray();

        return FluentHtml.Div(wrapper =>
        {
            wrapper.Add(RenderAddForm(kitName))
                .Add(RenderFileList(kitName, files));

            for (var index = 0; index < files.Length; index++)
                wrapper.Add(RenderRemoveDialog(kitName, files[index].RelativePath, index));
        });
    }

    private static IHtmlContent RenderAddForm(string kitName)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm", "mb-4")
                .Div(body =>
                {
                    body.Class("card-body")
                        .H2(h2 => h2.Class("h4", "mb-2").Text("Add files"))
                        .P(p => p.Class("text-body-secondary", "mb-3")
                            .Text("Add one or more files to this kit. Existing paths are protected and must be removed before they can be replaced."))
                        .Add(FluentHtml.Form(form =>
                        {
                            form.Id("kit-files-add")
                                .MultipartFormData()
                                .Heimdall(heimdall => heimdall
                                    .OnSubmit(ActionIds.Add)
                                    .PayloadFromClosestForm()
                                    .Target("#app")
                                    .SwapInner()
                                    .PreventDefault()
                                    .Disable());

                            form.Input(Html.InputType.hidden, input => input
                                .Name("KitName")
                                .Value(kitName));

                            form.Label(label => label
                                .For("kit-files-input")
                                .Class("form-label")
                                .Text("Files"));

                            form.Input(Html.InputType.file, input => input
                                .Id("kit-files-input")
                                .Name("files")
                                .Attr("multiple", "multiple")
                                .Class("form-control")
                                .Required());

                            form.Div(help => help.Class("form-text")
                                .Text("Project and solution files such as .csproj, .sln, and .slnx plus generated directories are skipped."));

                            form.Button(button => button
                                .Type("submit")
                                .Class("btn", "btn-primary", "mt-3")
                                .Text("Add files"));
                        }));
                });
        });

    private static IHtmlContent RenderFileList(
        string kitName,
        IReadOnlyList<StoredFile> files)
    {
        var tree = BuildTree(files);
        var fileIndexes = files
            .Select((file, index) => (file.RelativePath, Index: index))
            .ToDictionary(
                entry => entry.RelativePath,
                entry => entry.Index,
                StringComparer.OrdinalIgnoreCase);

        return FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Captured files"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{files.Count} file{(files.Count == 1 ? string.Empty : "s")} in this kit")));
                });

            if (files.Count == 0)
            {
                article.Div(body => body.Class("card-body")
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("This kit has no files.")));
                return;
            }

            article.Div(body =>
            {
                body.Class("card-body", "p-0")
                    .Div(treeHost =>
                    {
                        treeHost.Class("lasagna-kit-tree", "p-3");

                        foreach (var file in tree.Files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                        {
                            treeHost.Add(RenderFileRow(
                                kitName,
                                file,
                                fileIndexes[file.RelativePath]));
                        }

                        foreach (var folder in tree.Folders.Values.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            treeHost.Add(RenderFolder(kitName, folder, fileIndexes));
                        }
                    });
            });
        });
    }

    private static IHtmlContent RenderFolder(
        string kitName,
        FolderNode folder,
        IReadOnlyDictionary<string, int> fileIndexes)
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

                    foreach (var file in folder.Files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderFileRow(
                            kitName,
                            file,
                            fileIndexes[file.RelativePath]));
                    }

                    foreach (var child in folder.Folders.Values.OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        children.Add(RenderFolder(kitName, child, fileIndexes));
                    }
                });
        });

    private static IHtmlContent RenderFileRow(
        string kitName,
        StoredFile file,
        int index)
    {
        var fileUrl = PageLinks.KitFile(kitName, file.RelativePath);
        var dialogId = $"kit-file-remove-{index}";

        return FluentHtml.Div(row =>
        {
            row.Class("lasagna-kit-tree__file", "bg-body", "border", "border-secondary-subtle", "d-flex", "justify-content-between", "align-items-center", "gap-3", "flex-wrap")
                .Div(copy => copy
                    .A(link => link
                        .Href(fileUrl)
                        .Class("font-monospace", "link-light", "text-decoration-none", "fw-semibold")
                        .Text(Path.GetFileName(file.RelativePath)))
                    .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-1")
                        .Text(GetFileKind(file.RelativePath))))
                .Div(actions => actions.Class("d-flex", "gap-2")
                    .A(view => view
                        .Href(fileUrl)
                        .Role("button")
                        .Class("btn", "btn-sm", "btn-outline-primary")
                        .Text("Open"))
                    .Add(RemoveButton(dialogId)));
        });
    }

    private static FolderNode BuildTree(IReadOnlyList<StoredFile> files)
    {
        var root = new FolderNode(string.Empty);

        foreach (var file in files)
        {
            var segments = file.RelativePath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (!current.Folders.TryGetValue(segments[index], out var child))
                {
                    child = new FolderNode(segments[index]);
                    current.Folders.Add(segments[index], child);
                }

                current = child;
            }

            current.Files.Add(file);
        }

        return root;
    }

    private static IHtmlContent RemoveButton(string dialogId)
        => FluentHtml.Button(button => button
            .Type("button")
            .Class("btn", "btn-sm", "btn-outline-danger")
            .CommandFor(dialogId)
            .Command(Html.CommandType.show_modal)
            .Text("Remove"));

    private static IHtmlContent RenderRemoveDialog(
        string kitName,
        string relativePath,
        int index)
    {
        var dialogId = $"kit-file-remove-{index}";
        var titleId = $"{dialogId}-title";

        return FluentHtml.Tag("dialog", dialog =>
        {
            dialog.Id(dialogId)
                .Class("lasagna-confirmation-dialog")
                .Aria("labelledby", titleId)
                .Div(panel =>
                {
                    panel.Class("lasagna-confirmation-dialog__panel", "p-4")
                        .H2(h2 => h2
                            .Id(titleId)
                            .Class("h4", "mb-3")
                            .Text("Remove this file?"))
                        .P(p => p.Class("text-body-secondary", "mb-4")
                            .Text($"Remove '{relativePath}' from the kit? This cannot be undone."))
                        .Div(actions => actions.Class("d-flex", "justify-content-end", "gap-2")
                            .Button(cancel => cancel
                                .Type("button")
                                .Class("btn", "btn-outline-secondary")
                                .CommandFor(dialogId)
                                .Command(Html.CommandType.close)
                                .Text("Cancel"))
                            .Button(confirm => confirm
                                .Type("button")
                                .Class("btn", "btn-danger")
                                .CommandFor(dialogId)
                                .Command(Html.CommandType.close)
                                .Heimdall(heimdall => heimdall
                                    .Click(ActionIds.Remove)
                                    .Payload(new RemoveFilePayload
                                    {
                                        KitName = kitName,
                                        RelativePath = relativePath
                                    })
                                    .Target("#app")
                                    .SwapInner()
                                    .Disable())
                                .Text("Remove file")));
                });
        });
    }

    [RequestSizeLimit(50_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
    [ContentInvocation("add")]
    public static IHtmlContent Add(
        [FromForm] AddFilesPayload payload,
        [FromForm(Name = "files")] IFormFileCollection files)
    {
        try
        {
            var manifest = KitManager.AddUploadedFiles(payload.KitName, files);
            return KitPage.Render(
                manifest.Name,
                $"Added {files.Count} file{(files.Count == 1 ? string.Empty : "s")} to '{manifest.Name}'.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException)
        {
            return KitPage.Render(payload.KitName, exception.Message, isError: true);
        }
    }

    [ContentInvocation("remove")]
    public static IHtmlContent Remove([ContentPayload] RemoveFilePayload payload)
    {
        try
        {
            var manifest = KitManager.RemoveFile(payload.KitName, payload.RelativePath);
            return KitPage.Render(
                manifest.Name,
                $"Removed '{payload.RelativePath}' from '{manifest.Name}'.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException)
        {
            return KitPage.Render(payload.KitName, exception.Message, isError: true);
        }
    }

    private static string GetFileKind(string relativePath) => Path.GetExtension(relativePath).ToLowerInvariant() switch
    {
        ".cs" => "C# source",
        ".razor" => "Razor component",
        ".csproj" => ".NET project file",
        ".css" => "Stylesheet",
        ".js" => "JavaScript",
        ".json" => "JSON",
        ".md" => "Markdown",
        _ => "File"
    };

    private sealed record StoredFile(string RelativePath);

    private sealed class FolderNode(string name)
    {
        internal string Name { get; } = name;

        internal Dictionary<string, FolderNode> Folders { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<StoredFile> Files { get; } = [];

        internal int FileCount => Files.Count + Folders.Values.Sum(folder => folder.FileCount);
    }

    public sealed class AddFilesPayload
    {
        public string KitName { get; set; } = string.Empty;
    }

    public sealed class RemoveFilePayload
    {
        public string KitName { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;
    }
}
