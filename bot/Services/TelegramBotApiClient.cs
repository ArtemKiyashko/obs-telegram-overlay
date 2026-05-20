using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsTelegramOverlay.Bot.Configuration;

namespace ObsTelegramOverlay.Bot.Services;

public sealed class TelegramBotApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BotOptions _options;
    private readonly ILogger<TelegramBotApiClient> _logger;

    public TelegramBotApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<BotOptions> options,
        ILogger<TelegramBotApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            _logger.LogWarning("TelegramBotToken is not configured. Skipping reply to chat {ChatId}.", chatId);
            return;
        }

        var httpClient = _httpClientFactory.CreateClient();
        var endpoint = $"https://api.telegram.org/bot{_options.TelegramBotToken}/sendMessage";
        using var response = await httpClient.PostAsJsonAsync(endpoint, new
        {
            chat_id = chatId,
            text
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}