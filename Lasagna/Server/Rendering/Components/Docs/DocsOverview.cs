using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Docs;

internal static class DocsOverview
{
    internal static IHtmlContent Render()
        => FluentHtml.Div(row =>
        {
            row.Class("row", "g-4", "mb-4")
                .Div(column => column.Class("col-lg-4")
                    .Add(InfoCard(
                        "Items",
                        "Reusable collections of files and folders. Create them from the browser or console, then update or manage their files later.")))
                .Div(column => column.Class("col-lg-4")
                    .Add(InfoCard(
                        "Bundles",
                        "Named manifests composed of stored items. Each reference can be loaded into its own destination.")))
                .Div(column => column.Class("col-lg-4")
                    .Add(InfoCard(
                        "Loading",
                        "Load an item or bundle into the current project. Namespace rewriting can be automatic, explicit, or disabled.")));
        });

    private static IHtmlContent InfoCard(string heading, string description)
        => FluentHtml.Article(card =>
        {
            card.Class("card", "h-100", "shadow-sm")
                .Div(body => body.Class("card-body")
                    .H2(h2 => h2.Class("h4").Text(heading))
                    .P(p => p.Class("text-body-secondary", "mb-0").Text(description)));
        });
}
