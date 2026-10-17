using Heimdall.Server.Rendering;
using Lasagna.Models;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Kits;

internal static class KitList
{
    internal static IHtmlContent Render(IReadOnlyList<KitManifest> kits)
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Header(header =>
                {
                    header.Class("card-header", "bg-body-tertiary", "d-flex", "justify-content-between", "align-items-baseline")
                        .H2(h2 => h2.Class("h4", "mb-0").Text("Saved kits"))
                        .Add(Html.Tag(
                            "small",
                            Html.Attr("class", "text-body-secondary"),
                            Html.Text($"{kits.Count} kit{(kits.Count == 1 ? string.Empty : "s")}")));
                });

            if (kits.Count == 0)
            {
                article.Div(body => body.Class("card-body")
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("No kits saved yet.")));
                return;
            }

            article.Div(body =>
            {
                body.Class("card-body", "p-0")
                    .Ul(list =>
                    {
                        list.Class("list-group", "list-group-flush");

                        foreach (var kit in kits)
                        {
                            list.Li(row =>
                            {
                                row.Class("list-group-item", "bg-body", "border-secondary-subtle", "d-flex", "justify-content-between", "align-items-center", "gap-3", "py-3", "flex-wrap")
                                    .Div(copy => copy
                                        .A(link => link
                                            .Href(PageLinks.Kit(kit.Name))
                                            .Class("link-light", "text-decoration-none", "fw-semibold")
                                            .Text(kit.Name))
                                        .P(p => p.Class("small", "text-body-secondary", "mb-0", "mt-1")
                                            .Text($"{kit.Files.Count} file{(kit.Files.Count == 1 ? string.Empty : "s")} · {kit.SourceNamespace ?? "namespace not detected"}")))
                                    .Div(actions => actions.Class("d-flex", "align-items-center", "gap-2")
                                        .A(view => view
                                            .Href(PageLinks.Kit(kit.Name))
                                            .Role("button")
                                            .Class("btn", "btn-sm", "btn-outline-primary")
                                            .Text("Open"))
                                        .Add(CatalogPage.DeleteKitButton(kit.Name)));
                            });
                        }
                    });
            });
        });
}
