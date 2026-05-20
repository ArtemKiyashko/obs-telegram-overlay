namespace ObsTelegramOverlay.Bot.Models;

public sealed class OverlayChatMessage
{
    public long ChatId { get; init; }

    public int MessageId { get; init; }

    public string Sender { get; init; } = "unknown";

    public string Text { get; init; } = string.Empty;

    public DateTimeOffset SentUtc { get; init; }
}