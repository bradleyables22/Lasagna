using Heimdall.Server.Rendering;
using Lasagna.Server.Rendering.Components.Docs;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class DocsPage
{
    internal static IHtmlContent Render()=> FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "docs");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Docs", null));

            main.Header(header =>
            {
                header.Class("mb-5")
                    .H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Documentation"))
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("A quick guide to the browser and command-line interfaces."));
            });

            main.Add(DocsOverview.Render());

            main.Article(article =>
            {
                article.Class("card", "shadow-sm", "mb-4")
                    .Div(body => body.Class("card-body")
                        .H2(h2 => h2.Class("h3").Text("Browser workflow"))
                        .P(p => p.Class("text-body-secondary", "mb-0")
                            .Text("Use Items to create and manage reusable file collections. Use Bundles to group those items for repeatable loads. The Clear action in the top bar removes the entire local library after confirmation.")));
            });

            main.Add(DocsCommands.Render());
        });
}
