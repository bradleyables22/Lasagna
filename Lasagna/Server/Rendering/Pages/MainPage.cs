using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class MainPage
{
    internal static IHtmlContent Render() => CatalogPage.Render();
}
