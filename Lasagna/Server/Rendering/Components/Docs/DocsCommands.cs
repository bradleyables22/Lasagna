using Heimdall.Server.Rendering;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Components.Docs;

internal static class DocsCommands
{
    internal static IHtmlContent Render()
        => FluentHtml.Article(article =>
        {
            article.Class("card", "shadow-sm")
                .Div(body =>
                {
                    body.Class("card-body")
                        .H2(h2 => h2.Class("h3", "mb-4").Text("Command reference"))
                        .Add(CommandCard(
                            "Create and manage items",
                            "Items can contain one or more files. Use the file-management commands to change an existing item without recreating it.",
                            """
                            lasagna item create <name> <files>
                            lasagna item update <name> <files>
                            lasagna item list <name>
                            lasagna item add <name> <files>
                            lasagna item remove <name> <files>
                            """))
                        .Add(CommandCard(
                            "Create and manage bundles",
                            "Bundles reference stored items and can assign a destination or target namespace.",
                            """
                            lasagna bundle create <name> <items> [options]
                            lasagna bundle list [<name>]
                            lasagna bundle add <bundle> <item> [options]
                            lasagna bundle remove <bundle> <item> [options]
                            lasagna bundle delete <name>
                            """))
                        .Add(CommandCard(
                            "Use stored content",
                            "Load an item or bundle into the current project. Existing files are protected unless overwrite is explicit.",
                            """
                            lasagna load <name>
                            lasagna load <name> --destination Components
                            lasagna load <name> --namespace MyApp.Components
                            lasagna load <name> --overwrite
                            """))
                        .Add(CommandCard(
                            "Browse stored content",
                            "Use the web interface or open the files belonging to an item or bundle in browser tabs.",
                            """
                            lasagna browse web
                            lasagna browse item <name>
                            lasagna browse bundle <name>
                            lasagna list
                            """))
                        .Add(CommandCard(
                            "Remove and reset",
                            "Remove one stored item or bundle, or clear the complete local library.",
                            """
                            lasagna remove <name>
                            lasagna remove <name> --item
                            lasagna remove <name> --bundle
                            lasagna clear
                            """));
                });
        });

    private static IHtmlContent CommandCard(
        string heading,
        string description,
        string commands)
        => FluentHtml.Article(card =>
        {
            card.Class("border-top", "border-secondary-subtle", "pt-4", "mt-4")
                .H3(h3 => h3.Class("h5").Text(heading))
                .P(p => p.Class("text-body-secondary").Text(description))
                .Pre(pre => pre.Class("bg-body-tertiary", "rounded", "p-3", "mb-0")
                    .Code(code => code.Text(commands.Trim())));
        });
}
