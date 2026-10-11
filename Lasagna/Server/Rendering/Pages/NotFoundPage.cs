using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class NotFoundPage
{
    internal static IHtmlContent Render(string message)
        => FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "not-found");
            main.Header(header =>
            {
                header.Class("mb-4")
                    .H1(h1 => h1.Class("display-6", "fw-semibold").Text("Not found"))
                    .P(p => p.Class("lead", "text-body-secondary").Text(message));
            });
            main.A(link => link
                .Href("/")
                .Role("button")
                .Class("btn", "btn-outline-primary")
                .Text("Back to library"));
        });
}
