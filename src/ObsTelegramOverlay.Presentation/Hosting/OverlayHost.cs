using Microsoft.AspNetCore.SignalR;
using ObsTelegramOverlay.Application.Abstractions;
using ObsTelegramOverlay.Infrastructure.DependencyInjection;
using ObsTelegramOverlay.Presentation.Cli;
using ObsTelegramOverlay.Presentation.Realtime;
using ObsTelegramOverlay.Presentation.Templating;
using Spectre.Console;

namespace ObsTelegramOverlay.Presentation.Hosting;

public static class OverlayHost
{
    public static async Task RunAsync(RuntimeOptions options, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = [],
            ContentRootPath = Directory.GetCurrentDirectory()
        });

        builder.WebHost.UseUrls(options.ListenUrl);
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(new OverlayMessageStore(options.HistoryLimit));
        builder.Services.AddSingleton<OverlayTemplateProvider>();
        builder.Services.AddSingleton<IOverlayMessagePublisher, SignalrOverlayMessagePublisher>();
        builder.Services.AddTelegramPolling(options.BotApiToken);

        var app = builder.Build();

        app.MapGet("/", async (OverlayTemplateProvider templateProvider, CancellationToken ct) =>
        {
            var html = await templateProvider.GetOverlayHtmlAsync(options.OverlayTemplatePath, ct);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapHub<OverlayHub>("/hubs/overlay");

        AnsiConsole.MarkupLine($"[green]Overlay server started:[/] {options.ListenUrl}");
        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop.[/]");

        await app.RunAsync(cancellationToken);
    }
}
