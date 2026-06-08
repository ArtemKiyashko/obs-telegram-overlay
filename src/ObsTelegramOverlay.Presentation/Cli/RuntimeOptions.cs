namespace ObsTelegramOverlay.Presentation.Cli;

public sealed record RuntimeOptions(
    string BotApiToken,
    string ListenUrl,
    string? OverlayTemplatePath,
    int HistoryLimit
);
