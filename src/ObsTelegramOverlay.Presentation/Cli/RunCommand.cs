using ObsTelegramOverlay.Presentation.Hosting;
using Spectre.Console;
using Spectre.Console.Cli;

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
            MessageTtlSeconds: settings.MessageTtlSeconds);

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
}
