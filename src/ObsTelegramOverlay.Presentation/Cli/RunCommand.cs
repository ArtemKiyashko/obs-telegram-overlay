using ObsTelegramOverlay.Presentation.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Runtime.InteropServices;

namespace ObsTelegramOverlay.Presentation.Cli;

public sealed class RunCommand : AsyncCommand<RunSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, RunSettings settings, CancellationToken cancellationToken)
    {
        if (settings.HistoryLimit <= 0)
        {
            AnsiConsole.MarkupLine("[red]--history-limit must be greater than 0.[/]");
            return 2;
        }

        if (settings.MessageTtlSeconds <= 0)
        {
            AnsiConsole.MarkupLine("[red]--message-ttl-seconds must be greater than 0.[/]");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(settings.SpeechLang))
        {
            AnsiConsole.MarkupLine("[red]--speech-lang must not be empty.[/]");
            return 2;
        }

        if (!TryParseAllowedChatIds(settings.AllowedChatIds, out var allowedChatIds))
        {
            AnsiConsole.MarkupLine("[red]--allowed-chat-ids must be a comma-separated list of integer Telegram chat IDs.[/]");
            return 2;
        }

        var speechEngine = settings.SpeechEngine.Trim().ToLowerInvariant();

        if (speechEngine is not ("local" or "piper" or "edge-tts"))
        {
            AnsiConsole.MarkupLine("[red]--speech-engine must be one of: local, piper, edge-tts.[/]");
            return 2;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) is false && speechEngine is "piper" or "edge-tts")
        {
            AnsiConsole.MarkupLine($"[red]--speech-engine {speechEngine} is only supported on Linux.[/]");
            return 2;
        }

        var speechLangMode = settings.SpeechLangMode.Trim().ToLowerInvariant();
        if (speechLangMode is not ("fixed" or "auto"))
        {
            AnsiConsole.MarkupLine("[red]--speech-lang-mode must be one of: fixed, auto.[/]");
            return 2;
        }

        var botApiToken = settings.BotApiToken;
        if (string.IsNullOrWhiteSpace(botApiToken))
        {
            botApiToken = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Telegram bot token[/]:")
                    .PromptStyle("green")
                    .Secret());
        }

        var overlayTemplatePath = settings.OverlayTemplatePath;
        if (!string.IsNullOrWhiteSpace(overlayTemplatePath) && !File.Exists(overlayTemplatePath))
        {
            AnsiConsole.MarkupLine($"[red]Overlay template not found:[/] {overlayTemplatePath}");
            return 2;
        }

        var runtimeOptions = new RuntimeOptions(
            BotApiToken: botApiToken.Trim(),
            ListenUrl: settings.ListenUrl.Trim(),
            OverlayTemplatePath: overlayTemplatePath,
            HistoryLimit: settings.HistoryLimit,
            MessageTtlSeconds: settings.MessageTtlSeconds,
            AllowedChatIds: allowedChatIds,
            SpeechEnabled: settings.SpeechEnabled,
            SpeechEngine: speechEngine,
            SpeechLang: settings.SpeechLang.Trim(),
            SpeechLangMode: speechLangMode,
            SpeechVoice: string.IsNullOrWhiteSpace(settings.SpeechVoice) ? null : settings.SpeechVoice.Trim());

        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, cancellationToken);

        await OverlayHost.RunAsync(runtimeOptions, linked.Token);
        return 0;
    }

    private static bool TryParseAllowedChatIds(string? rawValue, out IReadOnlySet<long> allowedChatIds)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            allowedChatIds = new HashSet<long>();
            return true;
        }

        var ids = new HashSet<long>();
        foreach (var part in rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(part, out var chatId))
            {
                allowedChatIds = new HashSet<long>();
                return false;
            }

            ids.Add(chatId);
        }

        allowedChatIds = ids;
        return true;
    }
}
