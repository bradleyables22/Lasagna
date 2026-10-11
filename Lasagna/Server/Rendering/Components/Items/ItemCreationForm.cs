using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Items;

[ContentInvocationPrefix("items")]
internal static class ItemCreationForm
{
    private const string AccordionId = "item-create-accordion";
    private const string CollapseId = "item-create-collapse";

    internal static IHtmlContent Render()
        => FluentHtml.Div(wrapper =>
        {
            wrapper.Class("accordion", "lasagna-creation-accordion", "mb-4")
                .Id(AccordionId)
                .Div(item =>
                {
                    item.Class("accordion-item", "shadow-sm")
                        .H2(header => header
                            .Class("accordion-header")
                            .Button(button => button
                                .Id("item-create-toggle")
                                .Class("accordion-button", "collapsed")
                                .Type("button")
                                .Attr("data-bs-toggle", "collapse")
                                .Attr("data-bs-target", $"#{CollapseId}")
                                .Attr("aria-expanded", "false")
                                .Attr("aria-controls", CollapseId)
                                .Text("Create an item")))
                        .Div(collapse =>
                        {
                            collapse.Id(CollapseId)
                                .Class("accordion-collapse", "collapse")
                                .Attr("data-bs-parent", $"#{AccordionId}")
                                .Div(body =>
                                {
                                    body.Class("accordion-body")
                                        .P(p => p.Class("small", "text-body-secondary")
                                            .Text("Start a reusable item from files or a folder on this computer."))
                                        .Add(Form());
                                });
                        });
                });
        });

    private static IHtmlContent Form()
        => FluentHtml.Form(form =>
        {
            form.Id("item-create")
                .MultipartFormData()
                .Attr("data-require-one", "[data-upload-input]")
                .Attr("data-validation-message", "Choose at least one file or folder.")
                .Heimdall(heimdall => heimdall
                    .Submit("items.create")
                    .PayloadFromClosestForm()
                    .Target("#app")
                    .SwapInner()
                    .PreventDefault()
                    .Disable());

            form.Div(row =>
            {
                row.Class("row", "g-3")
                    .Div(column => column.Class("col-lg-6")
                        .Label(label => label
                            .For("new-item-name")
                            .Class("form-label")
                            .Text("Item name"))
                        .Input(Html.InputType.text, input => input
                            .Id("new-item-name")
                            .Name("Name")
                            .Class("form-control")
                            .Placeholder("component-library")
                            .Attr("pattern", "[^<>:\"/\\\\|?*]+")
                            .Attr("title", "Use a single folder-safe name.")
                            .Required()))
                    .Div(column => column.Class("col-lg-6")
                        .Label(label => label
                            .For("new-item-namespace")
                            .Class("form-label")
                            .Text("Source namespace (optional)"))
                        .Input(Html.InputType.text, input => input
                            .Id("new-item-namespace")
                            .Name("SourceNamespace")
                            .Class("form-control")
                            .Placeholder("MyApp.Components")));
            });

            form.Div(field =>
            {
                field.Class("mt-3")
                    .Label(label => label
                        .For("new-item-files")
                        .Class("form-label")
                        .Text("Files or folder (at least one required)"));

                field.Input(Html.InputType.file, input => input
                    .Id("new-item-files")
                    .Name("files")
                    .Attr("data-upload-input", "true")
                    .Attr("multiple", "multiple")
                    .Class("form-control"));

                field.Div(help => help.Class("form-text")
                    .Text("Choose multiple files with Ctrl or Command."));
            });

            form.Div(field =>
            {
                field.Class("mt-3")
                    .Label(label => label
                        .For("new-item-folder")
                        .Class("form-label")
                        .Text("Folder selection (optional)"));

                field.Input(Html.InputType.file, input => input
                    .Id("new-item-folder")
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
                .Class("btn", "btn-primary", "mt-3")
                .Text("Create item"));
        });

	[ContentInvocation("create")]
	public static IHtmlContent Create([FromForm] CreateItemPayload payload, [FromForm(Name = "files")] IFormFileCollection files)
	{
		try
		{
			var manifest = StorageManager.CreateUploadedFiles(
				payload.Name,
				files,
				payload.SourceNamespace);

			return ItemsPage.Render($"Created item '{manifest.Name}'.");
		}
		catch (Exception exception) 
		{
			return ItemsPage.Render(exception.Message, isError: true);
		}
	}

    public sealed class CreateItemPayload
    {
        public string Name { get; set; } = string.Empty;
        public string? SourceNamespace { get; set; }
    }
}
