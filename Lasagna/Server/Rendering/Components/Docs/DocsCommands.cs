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
                            "Capture and manage kits",
                            "Kits capture a directory's source contents as one reusable source set. The browser can add or remove individual files later. Project and solution files plus generated folders such as bin, obj, .git, and .vs are skipped automatically.",
                            """
                            lasagna kit create <name> <directory>
                            lasagna kit update <name> <directory>
                            lasagna kit list [<name>]
                            lasagna kit delete <name>
                            """))
                        .Add(CommandCard(
                            "Use stored content",
                            "Load an item, bundle, or kit into the current project. Existing files are protected unless overwrite is explicit; namespace alignment is automatic by default. Use --no-namespace-rewrite to preserve stored namespaces.",
                            """
                            lasagna load <name>
                            lasagna load <name> --destination Components
                            lasagna load <name> --namespace MyApp.Components
                            lasagna load <name> --overwrite
                            lasagna load <name> --no-namespace-rewrite
                            """))
                        .Add(CommandCard(
                            "Rename namespaces",
                            "Preview or apply a recursive namespace rename across an existing project. C#, Razor, using directives, descendant namespaces, and fully qualified references are updated; generated files, project files, comments, strings, and package references are left alone.",
                            """
                            lasagna namespace rename <from> <to>
                            lasagna namespace rename <from> <to> --dry-run
                            lasagna namespace rename <from> <to> --apply
                            lasagna namespace rename <from> <to> --path src/Orders
                            """))
                        .Add(CommandCard(
                            "Inspect dependencies",
                            "Scan the current directory or a solution scope without changing files. The inventory includes nested projects, direct and transitive package relationships, central package files, NuGet.Config sources, local folder feeds, and an interactive browser graph. Use deps replace separately when you are ready to replace a direct package with a source kit.",
                            """
                            lasagna deps list .
                            lasagna deps list path/to/solution
                            lasagna browse dependencies
                            lasagna browse dependencies path/to/solution
                            lasagna deps replace Thing.Api --with-kit thing-api --dry-run
                            """))
                        .Add(CommandCard(
                            "Browse stored content",
                            "Use the web interface or open the files belonging to an item, bundle, or kit in browser tabs.",
                            """
                            lasagna browse web
                            lasagna browse item <name>
                            lasagna browse bundle <name>
                            lasagna browse kit <name>
                            lasagna list
                            """))
                        .Add(CommandCard(
                            "Remove and reset",
                            "Remove one stored item, bundle, or kit, or clear the complete local library.",
                            """
                            lasagna remove <name>
                            lasagna remove <name> --item
                            lasagna remove <name> --bundle
                            lasagna remove <name> --kit
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
