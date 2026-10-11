using Heimdall.Server;
using Lasagna.Helpers;
using Lasagna.Server.Rendering.Layouts;
using Lasagna.Server.Rendering.Pages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Lasagna.Server;

internal static class LocalWebHost
{
    public static void Run()
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
        builder.Logging.ClearProviders();

        builder.Services.AddHeimdall(options => 
        {
            options.EnableDetailedErrors = true;
            options.EnableAntiforgery = false;
		});

        using var app = builder.Build();
		app.UseStaticFiles();
		app.UseHeimdall();

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

		app.MapHeimdallPage("/bundles/{name}", (_, ctx) =>
		{
			var name = ctx.Request.RouteValues["name"]?.ToString()
				?? throw new InvalidOperationException("A bundle name is required.");

			return MainLayout.Render(ctx, BundlePage.Render(name), $"Bundle: {name}");
		});


		app.MapPost("/shutdown", () =>
        {
            app.Lifetime.StopApplication();
            return Results.NoContent();
        });

        app.Start();

        var url = app.Urls.FirstOrDefault()
            ?? throw new InvalidOperationException("The local web interface did not start.");

        ConsoleUi.WriteInfo($"Lasagna web interface: {url}");
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ConsoleUi.WriteWarning(
                $"The browser could not be opened automatically. Open {url} manually.");
        }

        ConsoleUi.WriteInfo("Use the Stop server button in the browser or the terminal stop control to end the session.");
        app.WaitForShutdown();
    }
}
