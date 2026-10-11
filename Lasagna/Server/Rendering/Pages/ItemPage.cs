using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Items;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class ItemPage
{
    internal static IHtmlContent Render(string name, string? message = null, bool isError = false)
    {
        var item = StorageManager.Read(name);

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "item");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Items", "/items"),
                (item.Name, null));

            if (!string.IsNullOrWhiteSpace(message))
                main.Add(PageFeedback.Render(message, isError));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text(item.Name));

                        if (!string.IsNullOrWhiteSpace(item.SourceNamespace))
                        {
                            copy.P(p => p.Class("text-body-secondary", "mb-0")
                                .Text($"Source namespace: {item.SourceNamespace}"));
                        }
                    })
                    .Div(actions => actions.Class("d-flex", "gap-2")
                        .Add(CatalogPage.DeleteItemButton(item.Name)));
            });

            main.Add(ItemFilesPanel.Render(item.Name));
        });
    }
}
