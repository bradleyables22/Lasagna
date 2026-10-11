using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Items;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class ItemsPage
{
    internal static IHtmlContent Render(string? message = null,bool isError = false)
    {
        var items = StorageManager.List()
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "items");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Items", null));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Items"))
                            .P(p => p.Class("text-body-secondary", "mb-0")
                                .Text("Reusable files and folders saved for later."));
                    })
                    .A(back => back
                        .Href("/")
                        .Role("button")
                        .Class("btn", "btn-outline-secondary")
                        .Text("Back to library"));
            });

            if (!string.IsNullOrWhiteSpace(message))
                main.Add(PageFeedback.Render(message, isError));

            main.Add(ItemCreationForm.Render());
            main.Add(ItemList.Render(items));
        });
    }
}
