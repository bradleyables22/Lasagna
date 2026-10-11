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
        .WithDescription("Pull an item or bundle into the current project.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List stored items and bundles in a table.");
    config.AddCommand<RemoveCommand>("remove")
        .WithDescription("Remove a stored item or bundle.");
    config.AddCommand<ClearCommand>("clear")
        .WithDescription("Remove every stored item and bundle.");

    config.AddBranch("browse", browse =>
    {
        browse.SetDescription("Browse stored content or open the local web interface.");
        browse.AddCommand<BrowseWebCommand>("web")
            .WithDescription("Open the local Lasagna web interface.");
        browse.AddCommand<BrowseItemCommand>("item")
            .WithDescription("Open every file in a stored item in browser tabs.");
        browse.AddCommand<BrowseBundleCommand>("bundle")
            .WithDescription("Open every file referenced by a bundle in browser tabs.");
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
});

return app.Run(args);
