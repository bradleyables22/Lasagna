using Heimdall.Server;
using Heimdall.Server.Rendering;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Components.Shared;
using Microsoft.AspNetCore.Html;

namespace Lasagna.Server.Rendering.Pages;

[ContentInvocationPrefix("catalog")]
public static class CatalogPage
{
    public static class ActionIds
    {
        public const string DeleteItem = "catalog.item.delete";
        public const string DeleteBundle = "catalog.bundle.delete";
        public const string DeleteKit = "catalog.kit.delete";
        public const string Clear = "catalog.clear";
    }

    internal static IHtmlContent Render(string? message = null)
    {
        var itemCount = StorageManager.List().Count;
        var bundleCount = BundleManager.List().Count;
        var kitCount = KitManager.List().Count;

        return FluentHtml.Main(main =>
        {
            main.Class("container", "py-5", "lasagna-page")
                .Attr("data-view", "catalog");

            main.Header(header =>
            {
                header.Class("mb-5")
                    .H1(h1 => h1.Class("display-4", "fw-semibold", "mb-2").Text("Library"))
                    .P(p => p.Class("lead", "text-body-secondary", "mb-0")
                        .Text("A calm home for reusable items, bundles, and kits."));
            });

            if (!string.IsNullOrWhiteSpace(message))
            {
                main.Article(article =>
                {
                    article.Class("alert", "alert-warning", "mb-4")
                        .Attr("aria-live", "polite")
                        .P(p => p.Class("mb-0").Text(message));
                });
            }

            main.Div(row =>
            {
                row.Class("row", "g-4")
                    .Div(column => column.Class("col-lg-4")
                        .Add(OverviewCard(
                            "Items",
                            itemCount,
                            "Reusable files and folders saved for later.",
                            "/items",
                            "Browse items")))
                    .Div(column => column.Class("col-lg-4")
                        .Add(OverviewCard(
                            "Bundles",
                            bundleCount,
                            "Curated groups of items with their destinations.",
                            "/bundles",
                            "Browse bundles")))
                    .Div(column => column.Class("col-lg-4")
                        .Add(OverviewCard(
                            "Kits",
                            kitCount,
                            "Saved directory contents ready to copy into another project.",
                            "/kits",
                            "Browse kits")));
            });
        });
    }

    private static IHtmlContent OverviewCard(string heading,int count,string description,string href,string action)=> FluentHtml.Article(card =>
        {
            card.Class("card", "h-100", "shadow-sm")
                .Div(body =>
                {
                    body.Class("card-body", "d-flex", "flex-column", "p-4")
                        .Div(top =>
                        {
                            top.Class("d-flex", "justify-content-between", "align-items-start", "gap-3", "mb-3")
                                .H2(h2 => h2.Class("h3", "mb-0").Text(heading))
                                .Add(Html.Tag(
                                    "span",
                                    Html.Attr("class", "badge text-bg-secondary"),
                                    Html.Text(count.ToString())));
                        })
                        .P(p => p.Class("text-body-secondary", "mb-4").Text(description))
                        .A(link => link
                            .Href(href)
                            .Role("button")
                            .Class("btn", "btn-outline-primary", "mt-auto", "align-self-start")
                            .Text(action));
                });
        });

    internal static IHtmlContent DeleteItemButton(string name)
        => DeleteStorageButton(
            "item",
            name,
            ActionIds.DeleteItem,
            "Delete item?",
            $"Delete '{name}'? This cannot be undone.");

    internal static IHtmlContent DeleteBundleButton(string name)
        => DeleteStorageButton(
            "bundle",
            name,
            ActionIds.DeleteBundle,
            "Delete bundle?",
            $"Delete '{name}'? This cannot be undone.");

    internal static IHtmlContent DeleteKitButton(string name)
        => DeleteStorageButton(
            "kit",
            name,
            ActionIds.DeleteKit,
            "Delete kit?",
            $"Delete '{name}'? This cannot be undone.");

    private static IHtmlContent DeleteStorageButton(
        string kind,
        string name,
        string actionId,
        string title,
        string message)
    {
        var dialogId = ConfirmationDialog.CreateId($"delete-{kind}", name);

        return FluentHtml.Fragment(fragment =>
        {
            fragment.Add(ConfirmationDialog.Trigger(
                dialogId,
                "Delete",
                "btn", "btn-sm", "btn-outline-danger"));
            fragment.Add(ConfirmationDialog.Render(
                dialogId,
                title,
                message,
                FluentHtml.Button(confirm => confirm
                    .Type("button")
                    .Class("btn", "btn-danger")
                    .CommandFor(dialogId)
                    .Command(Html.CommandType.close)
                    .Heimdall(heimdall => heimdall
                        .Click(actionId)
                        .Payload(new DeleteStoragePayload { Name = name })
                        .Target("#app")
                        .SwapInner()
                        .Disable())
                    .Text("Delete"))));
        });
    }

	[ContentInvocation("clear")]
	public static IHtmlContent Clear()
	{
		var bundles = BundleManager.List();
		var kits = KitManager.List();
		var items = StorageManager.List();
		var total = bundles.Count + kits.Count + items.Count;

		foreach (var bundle in bundles)
			BundleManager.Delete(bundle.Name);

		foreach (var item in items)
			StorageManager.Delete(item.Name);

		foreach (var kit in kits)
			KitManager.Delete(kit.Name);

		return Render(total == 0
			? "The library is already empty."
			: $"Removed {total} stored entr{(total == 1 ? "y" : "ies")}.");
	}

	[ContentInvocation("item.delete")]
	public static IHtmlContent DeleteItem([ContentPayload] DeleteStoragePayload payload)
	{
		if (string.IsNullOrWhiteSpace(payload.Name))
			return ItemsPage.Render("Choose an item to delete.");

		var item = StorageManager.Read(payload.Name);
		var referencingBundles = BundleManager.FindReferencingBundles(item.Name);

		if (referencingBundles.Count > 0)
		{
			return ItemsPage.Render(
				$"Item '{item.Name}' is still used by: {string.Join(", ", referencingBundles)}.");
		}

		StorageManager.Delete(item.Name);
		return ItemsPage.Render();
	}

	[ContentInvocation("bundle.delete")]
	public static IHtmlContent DeleteBundle([ContentPayload] DeleteStoragePayload payload)
	{
		if (string.IsNullOrWhiteSpace(payload.Name))
			return BundlesPage.Render("Choose a bundle to delete.");

		var bundle = BundleManager.Read(payload.Name);
		BundleManager.Delete(bundle.Name);
		return BundlesPage.Render();
	}

	[ContentInvocation("kit.delete")]
	public static IHtmlContent DeleteKit([ContentPayload] DeleteStoragePayload payload)
	{
		if (string.IsNullOrWhiteSpace(payload.Name))
			return KitsPage.Render("Choose a kit to delete.", isError: true);

		var kit = KitManager.Read(payload.Name);
		KitManager.Delete(kit.Name);
		return KitsPage.Render();
	}

	public sealed class DeleteStoragePayload
    {
        public string Name { get; set; } = string.Empty;
    }
}
