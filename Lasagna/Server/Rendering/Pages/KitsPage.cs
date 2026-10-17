using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Kits;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class KitsPage
{
    internal static IHtmlContent Render(string? message = null, bool isError = false)
    {
        var kits = KitManager.List()
            .OrderBy(kit => kit.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "kits");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Kits", null));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Kits"))
                            .P(p => p.Class("text-body-secondary", "mb-0")
                                .Text("Saved directory contents that can be copied into another project."));
                    })
                    .A(back => back
                        .Href("/")
                        .Role("button")
                        .Class("btn", "btn-outline-secondary")
                        .Text("Back to library"));
            });

            if (!string.IsNullOrWhiteSpace(message))
                main.Add(PageFeedback.Render(message, isError));

            main.Add(KitCreationForm.Render());
            main.Add(KitList.Render(kits));
        });
    }
}
