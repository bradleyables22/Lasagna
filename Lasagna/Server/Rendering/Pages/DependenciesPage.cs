using Heimdall.Server.Rendering;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Dependencies;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class DependenciesPage
{
    internal static IHtmlContent Render(
        string scope,
        DependencyReport? report = null,
        string? message = null,
        bool isError = false)
        => FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "dependencies");

            Breadcrumbs.Render(
                main,
                ("Library", "/"),
                ("Dependencies", null));

            main.Header(header => header.Class("mb-5")
                .H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2").Text("Dependencies"))
                .P(p => p.Class("text-body-secondary", "mb-0")
                    .Text("Read-only project, solution, package, framework, and NuGet source discovery.")));

            main.Add(DependencyPanel.Render(scope, report, message, isError));
        });
}
