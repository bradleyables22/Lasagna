using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Shared;

internal static class PageFeedback
{
    internal static IHtmlContent Render(string message, bool isError) => FluentHtml.Article(article =>
        {
            article.Class(
                    "alert",
                    isError ? "alert-warning" : "alert-success",
                    "mb-4")
                .Attr("aria-live", "polite")
                .P(p => p.Class("mb-0").Text(message));
        });
}
