using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Bundles;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class BundlePage
{
    internal static IHtmlContent Render(string name)
    {
        var bundle = BundleManager.Read(name);

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "bundle");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Bundles", "/bundles"),
                (bundle.Name, null));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text(bundle.Name))
                            .P(p => p.Class("text-body-secondary", "mb-0")
                                .Text("A composed group of reusable items."));
                    })
                    .Div(actions => actions.Class("d-flex", "gap-2")
                        .Add(CatalogPage.DeleteBundleButton(bundle.Name)));
            });

            main.Add(BundleItemsPanel.Render(bundle));
        });
    }
}
