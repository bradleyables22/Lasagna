using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Bundles;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class BundlesPage
{
    internal static IHtmlContent Render(string? message = null,bool isError = false)
    {
        var bundles = BundleManager.List()
            .OrderBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var items = StorageManager.List()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "bundles");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Bundles", null));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Bundles"))
                            .P(p => p.Class("text-body-secondary", "mb-0")
                                .Text("Curated groups of items with their destinations."));
                    })
                    .A(back => back
                        .Href("/")
                        .Role("button")
                        .Class("btn", "btn-outline-secondary")
                        .Text("Back to library"));
            });

            if (!string.IsNullOrWhiteSpace(message))
                main.Add(PageFeedback.Render(message, isError));

            main.Add(BundleCreationForm.Render(items));
            main.Add(BundleList.Render(bundles));
        });
    }
}
