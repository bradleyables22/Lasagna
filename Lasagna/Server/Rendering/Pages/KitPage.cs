using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Kits;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class KitPage
{
    internal static IHtmlContent Render(string name, string? message = null, bool isError = false)
    {
        var kit = KitManager.Read(name);

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "kit");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Kits", "/kits"),
                (kit.Name, null));

            if (!string.IsNullOrWhiteSpace(message))
                main.Add(PageFeedback.Render(message, isError));

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-5")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text(kit.Name))
                            .P(p => p.Class("text-body-secondary", "mb-0")
                                .Text(kit.SourceNamespace is null
                                    ? "Source namespace was not detected."
                                    : $"Source namespace: {kit.SourceNamespace}"));
                    })
                    .Div(actions => actions.Class("d-flex", "gap-2")
                        .Add(CatalogPage.DeleteKitButton(kit.Name)));
            });

            main.Add(KitFilesPanel.Render(kit.Name));
        });
    }
}
