using Heimdall.Server.Rendering;

namespace Lasagna.Server.Rendering.Components.Shared;

internal static class Breadcrumbs
{
    internal static void Render(FluentHtml.ElementBuilder main,params (string Label, string? Href)[] items)
    {
        main.Nav(nav =>
        {
            nav.Class("mb-4")
                .Aria("label", "Breadcrumb")
                .Tag("ol", list =>
                {
                    list.Class("breadcrumb", "mb-0");

                    foreach (var (label, href) in items)
                    {
                        list.Li(item =>
                        {
                            item.Class("breadcrumb-item");

                            if (href is null)
                            {
                                item.Class("active")
                                    .Attr("aria-current", "page")
                                    .Text(label);
                            }
                            else
                            {
                                item.A(a => a.Href(href).Text(label));
                            }
                        });
                    }
                });
        });
    }
}
