using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Bundles;

internal static class BundleCreationForm
{
    private const string AccordionId = "bundle-create-accordion";
    private const string CollapseId = "bundle-create-collapse";

    internal static IHtmlContent Render(IReadOnlyList<ItemManifest> items)
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
                                .Id("bundle-create-toggle")
                                .Class("accordion-button", "collapsed")
                                .Type("button")
                                .Attr("data-bs-toggle", "collapse")
                                .Attr("data-bs-target", $"#{CollapseId}")
                                .Attr("aria-expanded", "false")
                                .Attr("aria-controls", CollapseId)
                                .Text("Create a bundle")))
                        .Div(collapse =>
                        {
                            collapse.Id(CollapseId)
                                .Class("accordion-collapse", "collapse")
                                .Attr("data-bs-parent", $"#{AccordionId}")
                                .Div(body =>
                                {
                                    body.Class("accordion-body")
                                        .P(p => p.Class("small", "text-body-secondary")
                                            .Text("Choose stored items and decide where they should land when loaded."))
                                        .Add(Form(items));
                                });
                        });
                });
        });

    private static IHtmlContent Form(IReadOnlyList<ItemManifest> items)
        => FluentHtml.Form(form =>
        {
            form.Id("bundle-create")
                .Attr("data-require-one", "[data-bundle-item]")
                .Attr("data-validation-message", "Choose at least one stored item.")
                .Heimdall(heimdall => heimdall
                    .Submit("bundles.create")
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
                            .For("new-bundle-name")
                            .Class("form-label")
                            .Text("Bundle name"))
                        .Input(Html.InputType.text, input => input
                            .Id("new-bundle-name")
                            .Name("Name")
                            .Class("form-control")
                            .Placeholder("admin-ui")
                            .Attr("pattern", "[^<>:\"/\\\\|?*]+")
                            .Attr("title", "Use a single folder-safe name.")
                            .Required()))
                    .Div(column => column.Class("col-lg-6")
                        .Label(label => label
                            .For("bundle-destination")
                            .Class("form-label")
                            .Text("Destination (optional)"))
                        .Input(Html.InputType.text, input => input
                            .Id("bundle-destination")
                            .Name("Destination")
                            .Value(".")
                            .Class("form-control")
                            .Placeholder("Components")));
            });

            form.Div(field =>
            {
                field.Class("mt-3")
                    .Label(label => label
                        .For("bundle-items")
                        .Class("form-label")
                        .Text("Items (at least one required)"));

                field.Div(options =>
                {
                    options.Class("border", "rounded", "p-3", "bg-body-tertiary");

                    if (items.Count == 0)
                    {
                        options.P(p => p.Class("text-body-secondary", "mb-0")
                            .Text("Create an item first before creating a bundle."));
                    }
                    else
                    {
                        foreach (var item in items.Select((value, index) => (value, index)))
                        {
                            options.Div(option => option.Class("form-check")
                                .Input(Html.InputType.checkbox, input => input
                                    .Id($"bundle-item-{item.index}")
                                    .Name("Items")
                                    .Value(item.value.Name)
                                    .Attr("data-bundle-item", "true")
                                    .Class("form-check-input"))
                                .Label(label => label
                                    .For($"bundle-item-{item.index}")
                                    .Class("form-check-label")
                                    .Text($"{item.value.Name} · {item.value.Files.Count} file{(item.value.Files.Count == 1 ? string.Empty : "s")}")));
                        }
                    }
                });
            });

            form.Div(field =>
            {
                field.Class("mt-3")
                    .Label(label => label
                        .For("bundle-namespace")
                        .Class("form-label")
                        .Text("Target namespace (optional)"));

                field.Input(Html.InputType.text, input => input
                    .Id("bundle-namespace")
                    .Name("TargetNamespace")
                    .Class("form-control")
                    .Placeholder("MyApp.Components"));
            });

            form.Div(field => field.Class("form-check", "mt-3")
                .Input(Html.InputType.checkbox, input => input
                    .Id("bundle-no-namespace-rewrite")
                    .Name("NoNamespaceRewrite")
                    .Value("true")
                    .Class("form-check-input"))
                .Label(label => label
                    .For("bundle-no-namespace-rewrite")
                    .Class("form-check-label")
                    .Text("Keep stored namespaces unchanged (optional)")));

            form.Button(button => button
                .Type("submit")
                .Class("btn", "btn-primary", "mt-3")
                .Disabled(items.Count == 0)
                .Text("Create bundle"));
        });

    [ContentInvocationPrefix("bundles")]
    public sealed class BundleCreationFormActions
    {
        [ContentInvocation("create")]
        public static IHtmlContent Create([ContentPayload] CreateBundlePayload payload)
        {
            try
            {
                var itemNames = payload.Items ?? [];
                if (itemNames.Length == 0)
                    throw new InvalidOperationException("Choose at least one stored item for the bundle.");

                var manifest = BundleManager.Create(
                    payload.Name,
                    itemNames.Select(item => new BundleItemReference
                    {
                        ItemName = item,
                        Destination = payload.Destination ?? ".",
                        RewriteNamespace = !payload.NoNamespaceRewrite,
                        TargetNamespace = payload.TargetNamespace
                    }));

                return BundlesPage.Render($"Created bundle '{manifest.Name}'.");
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                DirectoryNotFoundException or
                IOException or
                InvalidDataException or
                InvalidOperationException or
                UnauthorizedAccessException)
            {
                return BundlesPage.Render(exception.Message, isError: true);
            }
        }
    }

    public sealed class CreateBundlePayload
    {
        public string Name { get; set; } = string.Empty;
        public string[] Items { get; set; } = [];
        public string? Destination { get; set; }
        public string? TargetNamespace { get; set; }
        public bool NoNamespaceRewrite { get; set; }
    }
}
