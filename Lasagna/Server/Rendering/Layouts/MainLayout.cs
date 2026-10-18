using Heimdall.Server.Rendering;
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
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/namespace/rename")
                                                                        .Text("Rename")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .A(anchor => anchor
                                                                        .Class("nav-link")
                                                                        .Href("/docs")
                                                                        .Text("Docs")))
                                                                .Li(item => item.Class("nav-item")
                                                                    .Add(FluentHtml.Button(button =>
                                                                    {
                                                                        button.Class("btn", "btn-sm", "btn-outline-warning")
                                                                            .Type("button")
                                                                            .CommandFor("clear-confirmation")
                                                                            .Command(Html.CommandType.show_modal)
                                                                            .Text("Clear");
                                                                    })));
                                                        });
                                                });
                                        });
                                })
                                .Div(app => app.Id("app").Add(page))
                                .Tag("dialog", dialog =>
                                {
                                    dialog.Id("clear-confirmation")
                                        .Class("lasagna-confirmation-dialog")
                                        .Aria("labelledby", "clear-confirmation-title")
                                        .Div(panel =>
                                        {
                                            panel.Class("lasagna-confirmation-dialog__panel", "p-4")
                                                .H2(h2 => h2
                                                    .Id("clear-confirmation-title")
                                                    .Class("h4", "mb-3")
                                                    .Text("Clear the library?"))
                                                .P(p => p.Class("text-body-secondary", "mb-4")
                                                    .Text("This removes every stored item, bundle, and kit. This cannot be undone."))
                                                .Div(actions => actions.Class("d-flex", "justify-content-end", "gap-2")
                                                    .Button(cancel => cancel
                                                        .Type("button")
                                                        .Class("btn", "btn-outline-secondary")
                                                        .CommandFor("clear-confirmation")
                                                        .Command(Html.CommandType.close)
                                                        .Text("Cancel"))
                                                    .Button(confirm => confirm
                                                        .Type("button")
                                                        .Class("btn", "btn-warning")
                                                        .CommandFor("clear-confirmation")
                                                        .Command(Html.CommandType.close)
                                                        .Heimdall(heimdall => heimdall
                                                            .Click(CatalogPage.ActionIds.Clear)
                                                            .Target("#app")
                                                            .SwapInner()
                                                            .Disable())
                                                        .Text("Clear everything")));
                                        });
                                });
                        });
                });
        });
}
