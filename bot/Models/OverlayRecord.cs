namespace ObsTelegramOverlay.Bot.Models;

public sealed class OverlayRecord
{
    public string OverlayId { get; init; } = string.Empty;

    public long ChatId { get; init; }

    public string? ChatTitle { get; init; }

    public string PublicUrl { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public string Status { get; init; } = OverlayStatuses.Active;

    public string? RevokedReason { get; init; }
}

public static class OverlayStatuses
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}