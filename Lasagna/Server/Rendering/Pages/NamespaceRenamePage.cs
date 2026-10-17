using Heimdall.Server.Rendering;
using Lasagna.Server.Rendering.Components.Namespaces;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class NamespaceRenamePage
{
    internal static IHtmlContent Render(
        NamespaceRenamePanel.NamespaceRenamePayload? values = null,
        Lasagna.Models.NamespaceRenamePlan? plan = null,
        string? message = null,
        bool isError = false)
        => FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "namespace-rename");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Namespace rename", null));

            main.Header(header =>
            {
                header.Class("mb-5")
                    .H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Namespace rename"))
                    .P(p => p.Class("text-body-secondary", "mb-0")
                        .Text("Preview and deliberately apply a recursive namespace change across an existing project."));
            });

            main.Add(NamespaceRenamePanel.Render(values, plan, message, isError));
        });
}
