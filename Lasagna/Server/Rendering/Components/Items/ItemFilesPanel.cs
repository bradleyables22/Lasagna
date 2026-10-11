using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Items;

[ContentInvocationPrefix("item")]
internal static class ItemFilesPanel
{
    internal static IHtmlContent Render(string itemName)
    {
        var itemPath = StorageManager.GetItemPath(itemName);
        var files = StorageManager.ReadFiles(itemName)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return FluentHtml.Div(panel =>
        {
            panel.Add(AddFilesCard(itemName))
                .Add(FilesCard(itemName, itemPath, files));
        });
    }

    private static IHtmlContent AddFilesCard(string itemName)
        => FluentHtml.Article(card =>
        {
            card.Class("card", "shadow-sm", "mb-4")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary")
                        .H2(h2 => h2.Class("h4", "mb-1").Text("Add files"))
                        .P(p => p.Class("small", "text-body-secondary", "mb-0")
                            .Text("Add files or a folder to this item. Existing files are kept."));
                })
                .Div(body =>
                {
                    body.Class("card-body")
                        .Add(FluentHtml.Form(form =>
                        {
                            form.Id("item-add-files")
                                .MultipartFormData()
                                .Attr("data-require-one", "[data-upload-input]")
                                .Attr("data-validation-message", "Choose at least one file or folder.")
                                .Heimdall(heimdall => heimdall
                                    .Submit("item.files.add")
                                    .PayloadFromClosestForm()
                                    .Target("#app")
                                    .SwapInner()
                                    .PreventDefault()
                                    .Disable());

                            form.Input(Html.InputType.hidden, input => input
                                .Name("Name")
                                .Value(itemName));

                            form.Div(field =>
                            {
                                field.Class("mb-3")
                                    .Label(label => label
                                        .For("item-files")
                                        .Class("form-label")
                                        .Text("Files (at least one required)"));

                                field.Input(Html.InputType.file, input => input
                                    .Id("item-files")
                                    .Name("files")
                                    .Attr("data-upload-input", "true")
                                    .Attr("multiple", "multiple")
                                    .Class("form-control"));

                                field.Div(help => help.Class("form-text")
                                    .Text("Choose multiple files with Ctrl or Command."));
                            });

                            form.Div(field =>
                            {
                                field.Class("mb-3")
                                    .Label(label => label
                                        .For("item-folder")
                                        .Class("form-label")
                                        .Text("Folder selection (optional)"));

                                field.Input(Html.InputType.file, input => input
                                    .Id("item-folder")
                                    .Name("files")
                                    .Attr("data-upload-input", "true")
                                    .Attr("webkitdirectory", "webkitdirectory")
                                    .Attr("directory", "directory")
                                    .Class("form-control"));

                                field.Div(help => help.Class("form-text")
                                    .Text("Folder selection is supported by browsers that provide directory uploads."));
                            });

                            form.Button(button => button
                                .Type("submit")
                                .Class("btn", "btn-primary")
                                .Text("Add files"));
                        }));
                });
        });

    private static IHtmlContent FilesCard(string itemName,string itemPath,IReadOnlyList<string> files)=> FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Files"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{files.Count} file{(files.Count == 1 ? string.Empty : "s")} in this item")));
                });

            if (files.Count == 0)
            {
                article.Div(body => body.Class("card-body")
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("This item has no files.")));
                return;
            }

            article.Div(body =>
            {
                body.Class("card-body", "p-0")
                    .Ul(list =>
                    {
                        list.Class("list-group", "list-group-flush");

                        foreach (var file in files)
                        {
                            var relativePath = Path.GetRelativePath(itemPath, file).Replace('\\', '/');
                            var fileUrl = PageLinks.File(itemName, relativePath);

                            list.Li(row =>
                            {
                                row.Class("list-group-item", "bg-body", "border-secondary-subtle", "d-flex", "justify-content-between", "align-items-center", "gap-3", "py-3", "flex-wrap")
                                    .Div(copy => copy
                                        .A(link => link
                                            .Href(fileUrl)
                                            .Class("font-monospace", "link-light", "text-decoration-none", "fw-semibold")
                                            .Text(relativePath))
                                        .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-1")
                                            .Text(GetFileKind(relativePath))))
                                    .Div(actions => actions.Class("d-flex", "align-items-center", "gap-2")
                                        .A(view => view
                                            .Href(fileUrl)
                                            .Role("button")
                                            .Class("btn", "btn-sm", "btn-outline-primary")
                                            .Text("Open"))
                                        .Add(RemoveFileButton(itemName, relativePath)));
                            });
                        }
                    });
            });
        });

    private static IHtmlContent RemoveFileButton(string itemName, string relativePath)
        => FluentHtml.Button(button =>
        {
            button.Class("btn", "btn-sm", "btn-outline-danger")
                .Type("button")
                .Heimdall(heimdall => heimdall
                    .Click("item.file.remove")
                    .Payload(new RemoveFilePayload
                    {
                        Name = itemName,
                        Path = relativePath
                    })
                    .Target("#app")
                    .SwapInner()
                    .Disable())
                .Text("Remove");
        });

    private static string GetFileKind(string relativePath) => Path.GetExtension(relativePath).ToLowerInvariant() switch
        {
            ".cs" => "C# source",
            ".razor" => "Razor component",
            ".css" => "Stylesheet",
            ".js" => "JavaScript",
            ".json" => "JSON",
            ".md" => "Markdown",
            _ => "Text"
        };

	[ContentInvocation("files.add")]
	public static IHtmlContent AddFiles([FromForm] ItemFilesPayload payload, [FromForm(Name = "files")] IFormFileCollection files)
	{
		if (string.IsNullOrWhiteSpace(payload.Name))
			return ItemsPage.Render("Choose an item before adding files.", isError: true);

		try
		{
			var previousFileCount = StorageManager.Read(payload.Name).Files.Count;
			var manifest = StorageManager.AddUploadedFiles(payload.Name, files);
			var addedFileCount = manifest.Files.Count - previousFileCount;

			return ItemPage.Render(
				manifest.Name,
				$"Added {addedFileCount} file{(addedFileCount == 1 ? string.Empty : "s")}.");
		}
		catch (DirectoryNotFoundException exception)
		{
			return ItemsPage.Render(exception.Message, isError: true);
		}
		catch (Exception exception) when (
			exception is ArgumentException or
			IOException or
			InvalidDataException or
			InvalidOperationException or
			UnauthorizedAccessException)
		{
			return ItemPage.Render(payload.Name, exception.Message, isError: true);
		}
	}

	[ContentInvocation("file.remove")]
	public static IHtmlContent RemoveFile([ContentPayload] RemoveFilePayload payload)
	{
		if (string.IsNullOrWhiteSpace(payload.Name) ||
			string.IsNullOrWhiteSpace(payload.Path))
			return ItemsPage.Render("Choose a file to remove.", isError: true);

		try
		{
			var manifest = StorageManager.RemoveFiles(payload.Name, [payload.Path]);
			return ItemPage.Render(manifest.Name, $"Removed '{payload.Path}'.");
		}
		catch (DirectoryNotFoundException exception)
		{
			return ItemsPage.Render(exception.Message, isError: true);
		}
		catch (Exception exception) when (
			exception is ArgumentException or
			FileNotFoundException or
			IOException or
			InvalidDataException or
			InvalidOperationException or
			UnauthorizedAccessException)
		{
			return ItemPage.Render(payload.Name, exception.Message, isError: true);
		}
	}

	public sealed class ItemFilesPayload
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class RemoveFilePayload
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}
