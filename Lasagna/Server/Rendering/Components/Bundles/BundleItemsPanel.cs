using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Bundles;

internal static class BundleItemsPanel
{
    internal static IHtmlContent Render(BundleManifest bundle)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Items"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{bundle.Items.Count} item{(bundle.Items.Count == 1 ? string.Empty : "s")} in this bundle")));
                });

            if (bundle.Items.Count == 0)
            {
                article.Div(body => body.Class("card-body")
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("This bundle has no items.")));
                return;
            }

            article.Div(body =>
            {
                body.Class("card-body", "p-0")
                    .Ul(list =>
                    {
                        list.Class("list-group", "list-group-flush");

                        foreach (var reference in bundle.Items)
                        {
                            var item = StorageManager.Read(reference.ItemName);

                            list.Li(row =>
                            {
                                row.Class("list-group-item", "bg-body", "border-secondary-subtle", "d-flex", "justify-content-between", "align-items-center", "gap-3", "py-3", "flex-wrap")
                                    .Div(copy => copy
                                        .A(link => link
                                            .Href(PageLinks.Item(item.Name))
                                            .Class("link-light", "text-decoration-none", "fw-semibold")
                                            .Text(item.Name))
                                        .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-1")
                                            .Text($"{item.Files.Count} file{(item.Files.Count == 1 ? string.Empty : "s")}")))
                                    .Div(destination => destination.Class("text-end")
                                        .Add(Html.Tag(
                                            "small",
                                            Html.Attr("class", "d-block text-body-secondary"),
                                            Html.Text("Destination")))
                                        .Span(span => span.Class("font-monospace", "text-body-emphasis").Text(reference.Destination)));
                            });
                        }
                    });
            });
        });
}
