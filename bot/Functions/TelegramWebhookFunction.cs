using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsTelegramOverlay.Bot.Configuration;
using ObsTelegramOverlay.Bot.Models;
using ObsTelegramOverlay.Bot.Services;

namespace ObsTelegramOverlay.Bot.Functions;

public sealed class TelegramWebhookFunction
{
    private readonly OverlayRegistryService _overlayRegistryService;
    private readonly SignalRService _signalRService;
    private readonly TelegramBotApiClient _telegramBotApiClient;
    private readonly BotOptions _options;
    private readonly ILogger<TelegramWebhookFunction> _logger;

    public TelegramWebhookFunction(
        OverlayRegistryService overlayRegistryService,
        SignalRService signalRService,
        TelegramBotApiClient telegramBotApiClient,
        IOptions<BotOptions> options,
        ILogger<TelegramWebhookFunction> logger)
    {
        _overlayRegistryService = overlayRegistryService;
        _signalRService = signalRService;
        _telegramBotApiClient = telegramBotApiClient;
        _options = options.Value;
        _logger = logger;
    }

    [Function(nameof(TelegramWebhookFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "telegram/webhook")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        if (!TryGetMessage(document.RootElement, out var message))
        {
            return request.CreateResponse(HttpStatusCode.OK);
        }

        if (IsManagementCommand(message.ChatId, message.Text))
        {
            await HandleManagementCommandAsync(message.ChatId, message.ChatTitle, message.Text, cancellationToken);
            return request.CreateResponse(HttpStatusCode.OK);
        }

        var overlays = await _overlayRegistryService.ListActiveOverlaysForChatAsync(message.ChatId, cancellationToken);
        if (overlays.Count == 0)
        {
            _logger.LogDebug("No active overlays for chat {ChatId}", message.ChatId);
            return request.CreateResponse(HttpStatusCode.OK);
        }

        var overlayMessage = new OverlayChatMessage
        {
            ChatId = message.ChatId,
            MessageId = message.MessageId,
            Sender = message.Sender,
            Text = message.Text,
            SentUtc = message.SentUtc
        };

        foreach (var overlay in overlays)
        {
            await _signalRService.PublishMessageAsync(overlay.OverlayId, overlayMessage, cancellationToken);
        }

        return request.CreateResponse(HttpStatusCode.OK);
    }

    private async Task HandleManagementCommandAsync(long managementChatId, string? managementChatTitle, string text, CancellationToken cancellationToken)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].ToLowerInvariant();
        switch (command)
        {
            case "/overlay-create":
            {
                if (parts.Length < 2 || !long.TryParse(parts[1], out var targetChatId))
                {
                    await _telegramBotApiClient.SendMessageAsync(managementChatId, "Usage: /overlay-create <chatId>", cancellationToken);
                    return;
                }

                var overlay = await _overlayRegistryService.CreateOverlayAsync(targetChatId, managementChatTitle, cancellationToken);
                await _telegramBotApiClient.SendMessageAsync(managementChatId, $"Overlay created:\n{overlay.PublicUrl}\nID: {overlay.OverlayId}", cancellationToken);
                return;
            }

            case "/overlay-revoke":
            {
                if (parts.Length < 2)
                {
                    await _telegramBotApiClient.SendMessageAsync(managementChatId, "Usage: /overlay-revoke <overlayId>", cancellationToken);
                    return;
                }

                var overlayId = parts[1];
                var overlay = await _overlayRegistryService.RevokeOverlayAsync(overlayId, "Revoked by Telegram command", cancellationToken);
                if (overlay is null)
                {
                    await _telegramBotApiClient.SendMessageAsync(managementChatId, $"Overlay not found: {overlayId}", cancellationToken);
                    return;
                }

                await _signalRService.CloseOverlayConnectionsAsync(overlayId, cancellationToken);
                await _telegramBotApiClient.SendMessageAsync(managementChatId, $"Overlay revoked: {overlayId}", cancellationToken);
                return;
            }

            case "/overlay-list":
            {
                if (parts.Length < 2 || !long.TryParse(parts[1], out var targetChatId))
                {
                    await _telegramBotApiClient.SendMessageAsync(managementChatId, "Usage: /overlay-list <chatId>", cancellationToken);
                    return;
                }

                var overlays = await _overlayRegistryService.ListOverlaysForChatAsync(targetChatId, cancellationToken);
                var lines = overlays.Count == 0
                    ? ["No overlays."]
                    : overlays.Select(overlay => $"{overlay.OverlayId} [{overlay.Status}] {overlay.PublicUrl}").ToArray();

                await _telegramBotApiClient.SendMessageAsync(managementChatId, string.Join("\n", lines), cancellationToken);
                return;
            }

            default:
                await _telegramBotApiClient.SendMessageAsync(
                    managementChatId,
                    "Commands:\n/overlay-create <chatId>\n/overlay-revoke <overlayId>\n/overlay-list <chatId>",
                    cancellationToken);
                return;
        }
    }

    private bool IsManagementCommand(long chatId, string text)
    {
        if (!_options.TelegramManagementChatId.HasValue)
        {
            return false;
        }

        return chatId == _options.TelegramManagementChatId.Value && text.StartsWith('/');
    }

    private static bool TryGetMessage(JsonElement update, out IncomingTelegramMessage message)
    {
        message = default;

        if (!update.TryGetProperty("message", out var messageElement))
        {
            return false;
        }

        if (!messageElement.TryGetProperty("chat", out var chatElement) ||
            !chatElement.TryGetProperty("id", out var chatIdElement) ||
            !chatIdElement.TryGetInt64(out var chatId))
        {
            return false;
        }

        if (!messageElement.TryGetProperty("message_id", out var messageIdElement) ||
            !messageIdElement.TryGetInt32(out var messageId))
        {
            return false;
        }

        if (!messageElement.TryGetProperty("date", out var dateElement) ||
            !dateElement.TryGetInt64(out var unixTime))
        {
            unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var text = TryGetString(messageElement, "text") ?? TryGetString(messageElement, "caption");
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var sender = "Unknown";
        if (messageElement.TryGetProperty("from", out var fromElement))
        {
            sender = TryGetString(fromElement, "username")
                ?? TryGetString(fromElement, "first_name")
                ?? sender;
        }

        message = new IncomingTelegramMessage(
            ChatId: chatId,
            ChatTitle: TryGetString(chatElement, "title") ?? TryGetString(chatElement, "username"),
            MessageId: messageId,
            Sender: sender,
            Text: text,
            SentUtc: DateTimeOffset.FromUnixTimeSeconds(unixTime));

        return true;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) ? property.GetString() : null;
    }

    private readonly record struct IncomingTelegramMessage(
        long ChatId,
        string? ChatTitle,
        int MessageId,
        string Sender,
        string Text,
        DateTimeOffset SentUtc);
}