using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Models;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

internal static class KitFilePage
{
    internal sealed record ResolvedFile(
        string KitName,
        string RelativePath,
        string FullPath,
        long Length);

    internal static bool TryResolve(string kitName, string? relativePath, out ResolvedFile? file)
    {
        file = null;

        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        KitManifest manifest;
        try
        {
            manifest = KitManager.Read(kitName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or
            IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }

        var kitPath = KitManager.GetKitPath(manifest.Name);
        var normalizedPath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var requestedPath = Path.GetFullPath(Path.Combine(kitPath, normalizedPath));
        var kitRoot = Path.GetFullPath(kitPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!requestedPath.StartsWith(kitRoot, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(requestedPath))
            return false;

        var manifestPath = Path.GetRelativePath(kitPath, requestedPath).Replace('\\', '/');
        if (!manifest.Files.Contains(manifestPath, StringComparer.OrdinalIgnoreCase))
            return false;

        file = new ResolvedFile(
            manifest.Name,
            manifestPath,
            requestedPath,
            new FileInfo(requestedPath).Length);
        return true;
    }

    internal static IHtmlContent Render(ResolvedFile file)
    {
        var contents = File.ReadAllText(file.FullPath)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var lines = contents.Split('\n');

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "kit-file");

            main.Nav(nav =>
            {
                nav.Class("mb-4")
                    .Aria("label", "Breadcrumb")
                    .Tag("ol", list =>
                    {
                        list.Class("breadcrumb", "mb-0")
                            .Li(item => item.Class("breadcrumb-item")
                                .A(a => a.Href("/").Text("Library")))
                            .Li(item => item.Class("breadcrumb-item")
                                .A(a => a.Href(PageLinks.Kit(file.KitName)).Text(file.KitName)))
                            .Li(item => item.Class("breadcrumb-item", "active")
                                .Attr("aria-current", "page")
                                .Text(file.RelativePath));
                    });
            });

            main.Header(header =>
            {
                header.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "flex-wrap", "mb-4")
                    .Div(copy =>
                    {
                        copy.H1(h1 => h1.Class("display-6", "fw-semibold", "mb-2")
                                .Span(span => span.Class("font-monospace").Text(Path.GetFileName(file.RelativePath))))
                            .P(p => p.Class("text-body-secondary", "mb-1")
                                .Text($"{file.KitName} / {file.RelativePath}"))
                            .P(p => p.Class("small", "text-body-secondary", "mb-0")
                                .Text($"{lines.Length:N0} line{(lines.Length == 1 ? string.Empty : "s")} · {FormatBytes(file.Length)} · read-only preview"));
                    })
                    .Div(actions =>
                    {
                        actions.Class("d-flex", "gap-2")
                            .A(back => back
                                .Href(PageLinks.Kit(file.KitName))
                                .Role("button")
                                .Class("btn", "btn-outline-secondary")
                                .Text("Back to kit"))
                            .A(raw => raw
                                .Href(PageLinks.RawKitFile(file.KitName, file.RelativePath))
                                .Target("_blank")
                                .Rel("noreferrer")
                                .Role("button")
                                .Class("btn", "btn-outline-primary")
                                .Text("Open raw"));
                    });
            });

            main.Article(article =>
            {
                article.Class("card", "shadow-sm", "overflow-hidden")
                    .Header(header =>
                    {
                        header.Class("card-header", "bg-body-tertiary")
                            .H2(h2 => h2.Class("h4", "mb-1").Text("Contents"))
                            .P(p => p.Class("small", "text-body-secondary", "mb-0")
                                .Text("Plain text with line numbers for orientation."));
                    });

                if (contents.Length == 0)
                {
                    article.Div(body => body.Class("card-body")
                        .P(p => p.Class("text-body-secondary", "mb-0")
                            .Text("This file is empty.")));
                }
                else
                {
                    article.Pre(pre =>
                    {
                        pre.Class("lasagna-file-content", "p-3", "mb-0")
                            .Code(code =>
                            {
                                foreach (var line in lines)
                                {
                                    code.Add(Html.Tag(
                                        "span",
                                        Html.Attr("data-line", "true"),
                                        Html.Text(line)));
                                }
                            });
                    });
                }
            });
        });
    }

    private static string FormatBytes(long bytes)
        => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
            _ => $"{bytes / (1024d * 1024d):0.#} MB"
        };
}
