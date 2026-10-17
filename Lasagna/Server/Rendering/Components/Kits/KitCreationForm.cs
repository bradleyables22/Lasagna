using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lasagna.Server.Rendering.Components.Kits;

[ContentInvocationPrefix("kits")]
internal static class KitCreationForm
{
    private const string AccordionId = "kit-create-accordion";
    private const string CollapseId = "kit-create-collapse";

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
                                .Id("kit-create-toggle")
                                .Class("accordion-button", "collapsed")
                                .Type("button")
                                .Attr("data-bs-toggle", "collapse")
                                .Attr("data-bs-target", $"#{CollapseId}")
                                .Attr("aria-expanded", "false")
                                .Attr("aria-controls", CollapseId)
                                .Text("Capture a kit")))
                        .Div(collapse =>
                        {
                            collapse.Id(CollapseId)
                                .Class("accordion-collapse", "collapse")
                                .Attr("data-bs-parent", $"#{AccordionId}")
                                .Div(body =>
                                {
                                    body.Class("accordion-body")
                                        .P(p => p.Class("small", "text-body-secondary")
                                            .Text("Capture a project or directory as reusable content. Generated folders are skipped automatically."))
                                        .Add(Form());
                                });
                        });
                });
        });

    private static IHtmlContent Form()
        => FluentHtml.Form(form =>
        {
            form.Id("kit-create")
                .MultipartFormData()
                .Attr("data-require-one", "[data-kit-folder]")
                .Attr("data-validation-message", "Choose a project or folder.")
                .Heimdall(heimdall => heimdall
                    .OnSubmit("kits.create")
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
                            .For("new-kit-name")
                            .Class("form-label")
                            .Text("Kit name"))
                        .Input(Html.InputType.text, input => input
                            .Id("new-kit-name")
                            .Name("Name")
                            .Class("form-control")
                            .Placeholder("orders-api")
                            .Attr("pattern", "[^<>:\"/\\\\|?*]+")
                            .Attr("title", "Use a single folder-safe name.")
                            .Required()))
                    .Div(column => column.Class("col-lg-6")
                        .Label(label => label
                            .For("new-kit-namespace")
                            .Class("form-label")
                            .Text("Source namespace (optional)"))
                        .Input(Html.InputType.text, input => input
                            .Id("new-kit-namespace")
                            .Name("SourceNamespace")
                            .Class("form-control")
                            .Placeholder("Auto-detect")));
            });

            form.Div(field =>
            {
                field.Class("mt-3")
                    .Label(label => label
                        .For("new-kit-folder")
                        .Class("form-label")
                        .Text("Project or folder"));

                field.Input(Html.InputType.file, input => input
                    .Id("new-kit-folder")
                    .Name("files")
                    .Attr("data-kit-folder", "true")
                    .Attr("webkitdirectory", "webkitdirectory")
                    .Attr("directory", "directory")
                    .Class("form-control")
                    .Required());

                field.Div(help => help.Class("form-text")
                                            .Text("The folder contents are captured; project and solution files plus bin, obj, .git, .vs, TestResults, and .idea are skipped."));
            });

            form.Button(button => button
                .Type("submit")
                .Class("btn", "btn-primary", "mt-3")
                .Text("Capture kit"));
        });

    [ContentInvocation("create")]
    public static IHtmlContent Create(
        [FromForm] CreateKitPayload payload,
        [FromForm(Name = "files")] IFormFileCollection files)
    {
        try
        {
            var manifest = KitManager.CreateUploadedFiles(
                payload.Name,
                files,
                payload.SourceNamespace);

            return KitsPage.Render($"Captured kit '{manifest.Name}'.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException)
        {
            return KitsPage.Render(exception.Message, isError: true);
        }
    }

    public sealed class CreateKitPayload
    {
        public string Name { get; set; } = string.Empty;

        public string? SourceNamespace { get; set; }
    }
}
