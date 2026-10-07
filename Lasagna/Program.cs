using Lasagna.Commands;
using Lasagna.Helpers;
using Spectre.Console.Cli;

ConsoleUi.WriteLogo();

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("lasagna");
    config.SetExceptionHandler((exception, _) =>
    {
        ConsoleUi.WriteError(exception.Message);
        return 1;
    });

    config.AddCommand<SaveCommand>("save")
        .WithDescription("Save files or directories as a reusable item.");
    config.AddCommand<LoadCommand>("load")
        .WithDescription("Pull an item or bundle into the current project.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List stored items and bundles in a table.");
    config.AddCommand<RemoveCommand>("remove")
        .WithDescription("Remove a stored item or bundle.");
    config.AddCommand<ClearCommand>("clear")
        .WithDescription("Remove every stored item and bundle.");

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
