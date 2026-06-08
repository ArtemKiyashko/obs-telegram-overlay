using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ObsTelegramOverlay.Application.Abstractions;
using ObsTelegramOverlay.Application.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ObsTelegramOverlay.Infrastructure.Services;

public sealed class TelegramPollingBackgroundService : BackgroundService
{
    private static readonly UpdateType[] AllowedUpdates = [UpdateType.Message];

    private readonly ITelegramBotClient _botClient;
    private readonly IOverlayMessagePublisher _publisher;
    private readonly ILogger<TelegramPollingBackgroundService> _logger;

    public TelegramPollingBackgroundService(
        ITelegramBotClient botClient,
        IOverlayMessagePublisher publisher,
        ILogger<TelegramPollingBackgroundService> logger)
    {
        _botClient = botClient;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation("Telegram polling started as @{Username}", me.Username);

        var offset = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdates(
                    offset: offset,
                    timeout: 30,
                    allowedUpdates: AllowedUpdates,
                    cancellationToken: stoppingToken);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    await ProcessUpdateAsync(update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Telegram polling iteration failed");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }

        _logger.LogInformation("Telegram polling stopped");
    }

    private async Task ProcessUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null || message.Type != MessageType.Text || string.IsNullOrWhiteSpace(message.Text))
        {
            return;
        }

        var chatTitle = message.Chat.Title ?? message.Chat.Username ?? $"chat-{message.Chat.Id}";
        var sender = BuildSenderName(message.From);
        var payload = new OverlayMessage(
            ChatId: message.Chat.Id,
            ChatTitle: chatTitle,
            Sender: sender,
            Text: message.Text.Trim(),
            SentUtc: message.Date == default ? DateTimeOffset.UtcNow : message.Date);

        await _publisher.PublishAsync(payload, cancellationToken);
    }

    private static string BuildSenderName(User? from)
    {
        if (from is null)
        {
            return "Unknown";
        }

        if (!string.IsNullOrWhiteSpace(from.Username))
        {
            return $"@{from.Username}";
        }

        var fullName = string.Join(' ', new[] { from.FirstName, from.LastName }
            .Where(static value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(fullName) ? from.Id.ToString() : fullName;
    }
}
