using Heimdall.Server.Rendering;
using Lasagna.Models;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Items;

internal static class ItemList
{
    internal static IHtmlContent Render(IReadOnlyList<ItemManifest> items) => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Saved items"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{items.Count} item{(items.Count == 1 ? string.Empty : "s")}")));
                });

            if (items.Count == 0)
            {
                article.Div(body => body.Class("card-body")
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("No items saved yet.")));
                return;
            }

            article.Div(body =>
            {
                body.Class("card-body", "p-0")
                    .Ul(list =>
                    {
                        list.Class("list-group", "list-group-flush");

                        foreach (var item in items)
                        {
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
                                    .Div(actions => actions.Class("d-flex", "align-items-center", "gap-2")
                                        .A(view => view
                                            .Href(PageLinks.Item(item.Name))
                                            .Role("button")
                                            .Class("btn", "btn-sm", "btn-outline-primary")
                                            .Text("Open"))
                                        .Add(CatalogPage.DeleteItemButton(item.Name)));
                            });
                        }
                    });
            });
        });
}
