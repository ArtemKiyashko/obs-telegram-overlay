namespace ObsTelegramOverlay.Application.Models;

public sealed record OverlayMessage(
    long ChatId,
    string ChatTitle,
    string Sender,
    string Text,
    DateTimeOffset SentUtc
);
