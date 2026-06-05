namespace ObsTelegramOverlay.Bot.Configuration;

public sealed class BotOptions
{
    public const string SectionName = "Bot";

    public string? TelegramBotToken { get; init; }

    public long? TelegramManagementChatId { get; init; }

    public string OverlayBaseUrl { get; init; } = string.Empty;

    public string OverlayApiBaseUrl { get; init; } = string.Empty;

    public string OverlayStaticContainer { get; init; } = "$web";

    public string OverlayControlContainer { get; init; } = "control";

    public string SignalRConnectionString { get; init; } = string.Empty;

    public string SignalRHubName { get; init; } = "overlay";

    public int SignalRTokenLifetimeMinutes { get; init; } = 10;

    public string SiteTitle { get; init; } = "Telegram Stream Overlay";
}