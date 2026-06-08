using Microsoft.AspNetCore.SignalR;
using ObsTelegramOverlay.Application.Abstractions;
using ObsTelegramOverlay.Infrastructure.DependencyInjection;
using ObsTelegramOverlay.Presentation.Cli;
using ObsTelegramOverlay.Presentation.Realtime;
using ObsTelegramOverlay.Presentation.Speech;
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
        builder.Services.AddSingleton<LocalSpeechSynthesisService>();
        builder.Services.AddSingleton<IOverlayMessagePublisher, SignalrOverlayMessagePublisher>();
        builder.Services.AddTelegramPolling(options.BotApiToken, options.AllowedChatIds);

        var app = builder.Build();
        var localSpeech = app.Services.GetRequiredService<LocalSpeechSynthesisService>();
        var effectiveSpeechEngine = ResolveSpeechEngine(options, localSpeech, out var speechNotice);

        if (!string.IsNullOrWhiteSpace(speechNotice))
        {
            AnsiConsole.MarkupLine($"[yellow]{speechNotice}[/]");
        }

        AnsiConsole.MarkupLine($"[green]Speech engine:[/] {effectiveSpeechEngine} (lang mode: {options.SpeechLangMode}, default lang: {options.SpeechLang})");

        app.MapGet("/", async (OverlayTemplateProvider templateProvider, CancellationToken ct) =>
        {
            var html = await templateProvider.GetOverlayHtmlAsync(options.OverlayTemplatePath, ct);
            html = InjectOverlaySettings(html, options.MessageTtlSeconds, effectiveSpeechEngine, options.SpeechLang, options.SpeechLangMode);
            return Results.Content(html, "text/html; charset=utf-8");
        });

        app.MapPost("/api/speech", async (SpeechRequest request, CancellationToken ct) =>
        {
            if (effectiveSpeechEngine != "local")
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return Results.BadRequest(new { error = "Text is required." });
            }

            var result = await localSpeech.SynthesizeAsync(request.Text.Trim(), request.Lang.Trim(), ct);
            if (result is null)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            return Results.File(result.AudioBytes, result.ContentType);
        });

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapHub<OverlayHub>("/hubs/overlay");

        AnsiConsole.MarkupLine($"[green]Overlay server started:[/] {options.ListenUrl}");
        AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop.[/]");

        await app.RunAsync(cancellationToken);
    }

    private static string InjectOverlaySettings(string html, int messageTtlSeconds, string speechEngine, string speechLang, string speechLangMode)
    {
        var speechLangEscaped = speechLang
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        var speechLangModeEscaped = speechLangMode
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        var settingsScript =
            $"<script>window.__overlaySettings = {{ messageTtlSeconds: {messageTtlSeconds}, speechEngine: \"{speechEngine}\", speechLang: \"{speechLangEscaped}\", speechLangMode: \"{speechLangModeEscaped}\" }};</script>";

        var headCloseIndex = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headCloseIndex >= 0)
        {
            return html.Insert(headCloseIndex, settingsScript);
        }

        return settingsScript + html;
    }

    private static string ResolveSpeechEngine(RuntimeOptions options, LocalSpeechSynthesisService localSpeech, out string? notice)
    {
        notice = null;

        if (options.SpeechEngine == "browser")
        {
            return "browser";
        }

        if (options.SpeechEngine == "local")
        {
            if (localSpeech.IsAvailable(out var reason))
            {
                return "local";
            }

            notice = $"Local speech requested but unavailable ({reason}). Falling back to browser engine.";
            return "browser";
        }

        return "browser";
    }
}
