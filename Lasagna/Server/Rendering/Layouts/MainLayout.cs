using Heimdall.Server.Rendering;
using Lasagna.Server.Rendering.Components.Shared;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;

namespace Lasagna.Server.Rendering.Layouts;

internal static class MainLayout
{
    internal static IHtmlContent Render(HttpContext ctx, IHtmlContent page, string title)
        => FluentHtml.Fragment(f =>
        {
            f.Raw("<!DOCTYPE html>")
                .HtmlTag(html =>
                {
                    html.Attr("lang", "en")
                        .Attr("data-bs-theme", "dark")
                        .Head(head =>
                        {
                            head.Meta(meta => meta.Attr("charset", "utf-8"))
                                .Meta(meta => meta.Name("viewport").ContentAttr("width=device-width, initial-scale=1"))
                                .Title(t => t.Text(title))
                                .Link(link =>
                                {
                                    link.Attr("rel", "icon")
                                        .Type("image/png")
                                        .Href("/images/lasagna-icon.png");
                                })
                                .Link(link =>
                                {
                                    link.Attr("rel", "stylesheet")
                                        .Href("/css/bootstrap.css");
                                })
                                .Link(link =>
                                {
                                    link.Attr("rel", "stylesheet")
                                        .Href("/css/lasagna.css");
                                })
                                .Script(script => script.Src("/js/bootstrap.bundle.min.js"))
                                .Script(script => script.Src("/_content/HeimdallFramework.Web/heimdall-bundle.min.js"))
                                .Script(script => script.Src("/js/vendor/cytoscape.min.js"))
                                .Script(script => script.Src("/js/lasagna.js"))
                                .Script(script => script.Src("/js/dependency-graph.js"))
                                .Script(script => script.Src("/js/heimdall.debug.js"));
                        })
                        .Body(body =>
                        {
                            body.Class("bg-body-tertiary")
                                .Header(header =>
                                {
                                    header.Class("border-bottom", "bg-body")
                                        .Div(container =>
                                        {
                                            container.Class("container")
                                                .Nav(nav =>
                                                {
                                                    nav.Class("navbar", "navbar-expand-lg", "py-3")
                                                        .A(anchor =>
                                                        {
                                                            anchor.Class("navbar-brand", "d-flex", "align-items-center", "gap-2")
                                                                .Href("/")
                                                                .Span(mark => mark.Class("lasagna-brand-mark"))
                                                                .Span(name => name.Text("Lasagna"));
                                                        })
                                                        .Ul(list =>
                                                        {
                                                            list.Class("navbar-nav", "ms-auto", "align-items-center", "gap-2", "flex-row", "flex-wrap", "justify-content-end")
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/items")
                                                                        .Text("Items")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/bundles")
                                                                        .Text("Bundles")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/kits")
                                                                        .Text("Kits")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/dependencies")
                                                                        .Text("Dependencies")))
                                                                .Li(item => item
                                                                    .Class("nav-item", "dropdown")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link", "dropdown-toggle")
                                                                        .Href("#")
                                                                        .Attr("role", "button")
                                                                        .Attr("data-bs-toggle", "dropdown")
                                                                        .Attr("aria-expanded", "false")
                                                                        .Text("Extras"))
                                                                    .Ul(menu => menu
                                                                        .Class("dropdown-menu", "dropdown-menu-end")
                                                                        .Li(entry => entry.Class("nav-item")
                                                                            .A(anchor => anchor
                                                                                .Class("dropdown-item")
                                                                                .Href("/namespace/rename")
                                                                                .Text("Rename")))
                                                                        .Li(entry => entry.Class("nav-item")
                                                                            .A(anchor => anchor
                                                                                .Class("dropdown-item")
                                                                                .Href("/shake")
                                                                                .Text("Shake")))))
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/docs")
                                                                        .Text("Docs")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .Add(ConfirmationDialog.Trigger(
                                                                        "clear-confirmation",
                                                                        "Clear",
                                                                        "btn", "btn-sm", "btn-outline-warning")));
                                                        });
                                                });
                                        });
                                })
                                .Div(app => app.Id("app").Add(page))
                                .Add(ConfirmationDialog.Render(
                                    "clear-confirmation",
                                    "Clear the library?",
                                    "This removes every stored item, bundle, and kit. This cannot be undone.",
                                    FluentHtml.Button(confirm => confirm
                                        .Type("button")
                                        .Class("btn", "btn-warning")
                                        .CommandFor("clear-confirmation")
                                        .Command(Html.CommandType.close)
                                        .Heimdall(heimdall => heimdall
                                            .Click(CatalogPage.ActionIds.Clear)
                                            .Target("#app")
                                            .SwapInner()
                                            .Disable())
                                        .Text("Clear everything"))));
                        });
                });
        });
}
