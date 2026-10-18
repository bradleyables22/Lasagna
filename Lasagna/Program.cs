using Lasagna.Commands;
using Lasagna.Helpers;
using Spectre.Console.Cli;

if (args.Length == 0)
    ConsoleUi.WriteLogo();

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("lasagna");
    config.CaseSensitivity(CaseSensitivity.None);
    config.SetExceptionHandler((exception, _) =>
    {
        ConsoleUi.WriteError(exception.Message);
        return 1;
    });

    config.AddCommand<LoadCommand>("load")
        .WithDescription("Pull an item, bundle, or kit into the current project with namespace alignment.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List stored items, bundles, and kits in tables.");
    config.AddCommand<RemoveCommand>("remove")
        .WithDescription("Remove a stored item, bundle, or kit.");
    config.AddCommand<ClearCommand>("clear")
        .WithDescription("Remove every stored item, bundle, and kit.");

    config.AddBranch("deps", dependencies =>
    {
        dependencies.SetDescription("Inspect projects, NuGet packages, project references, and package sources.");
        dependencies.AddCommand<DependencyListCommand>("list")
            .WithDescription("Print a nested dependency inventory for the current directory or a supplied path.");
    });

    config.AddBranch("namespace", namespaceCommands =>
    {
        namespaceCommands.SetDescription("Preview or apply namespace changes across source files.");
        namespaceCommands.AddCommand<NamespaceRenameCommand>("rename")
            .WithDescription("Rename a namespace and its related code references recursively.");
    });

    config.AddBranch("browse", browse =>
    {
        browse.SetDescription("Browse stored content or open the local web interface.");
        browse.AddCommand<BrowseWebCommand>("web")
            .WithDescription("Open the local Lasagna web interface.");
        browse.AddCommand<BrowseDependenciesCommand>("dependencies")
            .WithDescription("Open the dependency explorer for the current directory.");
        browse.AddCommand<BrowseItemCommand>("item")
            .WithDescription("Open every file in a stored item in browser tabs.");
        browse.AddCommand<BrowseBundleCommand>("bundle")
            .WithDescription("Open every file referenced by a bundle in browser tabs.");
        browse.AddCommand<BrowseKitCommand>("kit")
            .WithDescription("Open every file captured by a kit in browser tabs.");
    });

    config.AddBranch("item", item =>
    {
        item.SetDescription("Create and manage stored items.");
        item.AddCommand<ItemCreateCommand>("create")
            .WithDescription("Create a stored item from files or directories.");
        item.AddCommand<ItemUpdateCommand>("update")
            .WithDescription("Replace the contents of an existing item.");
        item.AddCommand<ItemListCommand>("list")
            .WithDescription("List the files inside a stored item.");
        item.AddCommand<ItemAddCommand>("add")
            .WithDescription("Add files or directories to an existing item.");
        item.AddCommand<ItemRemoveCommand>("remove")
            .WithDescription("Remove files or directories from an existing item.");
    });

    config.AddBranch("bundle", bundle =>
    {
        bundle.SetDescription("Create and maintain manifests of reusable items.");
        bundle.AddCommand<BundleCreateCommand>("create")
            .WithDescription("Create a bundle manifest from stored items.");
        bundle.AddCommand<BundleListCommand>("list")
            .WithDescription("List bundle manifests and their item destinations.");
        bundle.AddCommand<BundleAddCommand>("add")
            .WithDescription("Add a stored item reference to a bundle.");
        bundle.AddCommand<BundleRemoveCommand>("remove")
            .WithDescription("Remove a stored item reference from a bundle.");
        bundle.AddCommand<BundleDeleteCommand>("delete")
            .WithDescription("Delete a bundle manifest.");
    });

    config.AddBranch("kit", kit =>
    {
        kit.SetDescription("Capture and manage reusable directory contents.");
        kit.AddCommand<KitCreateCommand>("create")
            .WithDescription("Capture the contents of a directory as a kit.");
        kit.AddCommand<KitUpdateCommand>("update")
            .WithDescription("Replace a kit with the contents of a directory.");
        kit.AddCommand<KitListCommand>("list")
            .WithDescription("List kits or show the files inside one kit.");
        kit.AddCommand<KitDeleteCommand>("delete")
            .WithDescription("Delete a kit.");
    });
});

return app.Run(args);
