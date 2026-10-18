using Heimdall.Server;
using Lasagna.Helpers;
using Lasagna.Models;
using Lasagna.Server.Rendering.Components.Namespaces;
using Lasagna.Server.Rendering.Layouts;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lasagna.Server;

internal static class LocalWebHost
{
    public static void Run(string initialPath = "/")
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "Server", "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ApplicationName = typeof(LocalWebHost).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = webRoot
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.UseStaticWebAssets();
        builder.Logging.ClearProviders();

        builder.Services.AddHeimdall(options => 
        {
            options.EnableDetailedErrors = true;
            options.EnableAntiforgery = false;
		});

        using var app = builder.Build();
		var packagedWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
		var staticFileProviders = new List<IFileProvider>
		{
			new PhysicalFileProvider(webRoot)
		};

		if (Directory.Exists(packagedWebRoot))
			staticFileProviders.Add(new PhysicalFileProvider(packagedWebRoot));

		app.UseStaticFiles(new StaticFileOptions
		{
			FileProvider = new CompositeFileProvider(staticFileProviders)
		});
		app.MapStaticAssets();
		app.UseHeimdall();

		app.MapGet("/files/kit/{kit}/{**path}", (string kit, string? path) =>
		{
			if (!KitFilePage.TryResolve(kit, path, out var file) || file is null)
				return Results.NotFound();

			return Results.File(file.FullPath, "text/plain");
		});

		app.MapGet("/files/{item}/{**path}", (string item, string? path) =>
		{
			if (!FilePage.TryResolve(item, path, out var file) || file is null)
				return Results.NotFound();

			return Results.File(file.FullPath, "text/plain");
		});

		app.MapHeimdallPage("/", (_, ctx) =>
		{
			return MainLayout.Render(ctx, MainPage.Render(), "Home");
		});

		app.MapHeimdallPage("/items", (_, ctx) =>
		{
			return MainLayout.Render(ctx, ItemsPage.Render(), "Items");
		});

		app.MapHeimdallPage("/items/{name}", (_, ctx) =>
		{
			var name = ctx.Request.RouteValues["name"]?.ToString()
				?? throw new InvalidOperationException("An item name is required.");

			return MainLayout.Render(ctx, ItemPage.Render(name), $"Item: {name}");
		});

		app.MapHeimdallPage("/items/{item}/files/{**path}", (_, ctx) =>
		{
			var item = ctx.Request.RouteValues["item"]?.ToString() ?? string.Empty;
			var path = ctx.Request.RouteValues["path"]?.ToString();

			if (!FilePage.TryResolve(item, path, out var file) || file is null)
			{
				ctx.Response.StatusCode = StatusCodes.Status404NotFound;
				return MainLayout.Render(
					ctx,
					NotFoundPage.Render("That stored file could not be found."),
					"File not found");
			}

			return MainLayout.Render(
				ctx,
				FilePage.Render(file),
				$"{Path.GetFileName(file.RelativePath)} · {file.ItemName}");
		});

		app.MapHeimdallPage("/bundles", (_, ctx) =>
		{
			return MainLayout.Render(ctx, BundlesPage.Render(), "Bundles");
		});

		app.MapHeimdallPage("/docs", (_, ctx) =>
		{
			return MainLayout.Render(ctx, DocsPage.Render(), "Documentation");
		});

		app.MapHeimdallPage("/dependencies", (_, ctx) =>
		{
			var scope = string.IsNullOrWhiteSpace(ctx.Request.Query["Scope"].ToString())
				? "."
				: ctx.Request.Query["Scope"].ToString();

			try
			{
				return MainLayout.Render(
					ctx,
					DependenciesPage.Render(scope, DependencyAnalyzer.BuildReport(scope)),
					"Dependencies");
			}
			catch (Exception exception) when (exception is
				ArgumentException or
				DirectoryNotFoundException or
				FileNotFoundException or
				IOException or
				UnauthorizedAccessException or
				System.Xml.XmlException)
			{
				return MainLayout.Render(
					ctx,
					DependenciesPage.Render(scope, message: exception.Message, isError: true),
					"Dependencies");
			}
		});

		app.MapHeimdallPage("/namespace/rename", (_, ctx) =>
		{
			var payload = new NamespaceRenamePanel.NamespaceRenamePayload
			{
				From = ctx.Request.Query["From"].ToString(),
				To = ctx.Request.Query["To"].ToString(),
				Scope = string.IsNullOrWhiteSpace(ctx.Request.Query["Scope"].ToString())
					? "."
					: ctx.Request.Query["Scope"].ToString()
			};

			NamespaceRenamePlan? plan = null;
			string? message = null;
			var isError = false;

			if (!string.IsNullOrWhiteSpace(payload.From) &&
				!string.IsNullOrWhiteSpace(payload.To))
			{
				try
				{
					plan = NamespaceRenameManager.BuildPlan(
						payload.Scope,
						payload.From,
						payload.To);
				}
				catch (Exception exception) when (exception is
					ArgumentException or
					DirectoryNotFoundException or
					FileNotFoundException or
					IOException or
					InvalidDataException or
					UnauthorizedAccessException)
				{
					message = exception.Message;
					isError = true;
				}
			}

			return MainLayout.Render(
				ctx,
				NamespaceRenamePage.Render(payload, plan, message, isError),
				"Namespace rename");
		});

		app.MapHeimdallPage("/bundles/{name}", (_, ctx) =>
		{
			var name = ctx.Request.RouteValues["name"]?.ToString()
				?? throw new InvalidOperationException("A bundle name is required.");

			return MainLayout.Render(ctx, BundlePage.Render(name), $"Bundle: {name}");
		});

		app.MapHeimdallPage("/kits", (_, ctx) =>
		{
			return MainLayout.Render(ctx, KitsPage.Render(), "Kits");
		});

		app.MapHeimdallPage("/kits/{name}", (_, ctx) =>
		{
			var name = ctx.Request.RouteValues["name"]?.ToString()
				?? throw new InvalidOperationException("A kit name is required.");

			return MainLayout.Render(ctx, KitPage.Render(name), $"Kit: {name}");
		});

		app.MapHeimdallPage("/kits/{kit}/files/{**path}", (_, ctx) =>
		{
			var kit = ctx.Request.RouteValues["kit"]?.ToString() ?? string.Empty;
			var path = ctx.Request.RouteValues["path"]?.ToString();

			if (!KitFilePage.TryResolve(kit, path, out var file) || file is null)
			{
				ctx.Response.StatusCode = StatusCodes.Status404NotFound;
				return MainLayout.Render(
					ctx,
					NotFoundPage.Render("That captured kit file could not be found."),
					"Kit file not found");
			}

			return MainLayout.Render(
				ctx,
				KitFilePage.Render(file),
				$"{Path.GetFileName(file.RelativePath)} · {file.KitName}");
		});


		app.MapPost("/shutdown", () =>
        {
            app.Lifetime.StopApplication();
            return Results.NoContent();
        });

        app.Start();

        var url = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("The local web interface did not start.");

        var launchUrl = url.TrimEnd('/') +
            (string.IsNullOrWhiteSpace(initialPath)
                ? "/"
                : initialPath.StartsWith('/') ? initialPath : "/" + initialPath);

        ConsoleUi.WriteInfo($"Lasagna web interface: {launchUrl}");
        try
        {
            Process.Start(new ProcessStartInfo(launchUrl)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ConsoleUi.WriteWarning(
                $"The browser could not be opened automatically. Open {launchUrl} manually.");
        }

        ConsoleUi.WriteInfo("Use the Stop server button in the browser or the terminal stop control to end the session.");
        app.WaitForShutdown();
    }
}
