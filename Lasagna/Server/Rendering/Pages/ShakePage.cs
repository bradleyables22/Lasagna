using Heimdall.Server.Rendering;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Shared;
using Lasagna.Server.Rendering.Components.Shake;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class ShakePage
{
    internal static IHtmlContent Render(
        string scope = ".",
        ShakePlan? plan = null,
        string? message = null,
        bool isError = false)
        => FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "shake");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Extras", null),
                ("Shake", null));

            main.Header(header => header.Class("mb-5")
                .H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Leaf shake"))
                .P(p => p.Class("text-body-secondary", "mb-0")
                    .Text("Find unused local C# source files recursively, inspect their reference evidence, and clean up only after confirmation.")));

            main.Add(ShakePanel.Render(scope, plan, message, isError));
        });
}
