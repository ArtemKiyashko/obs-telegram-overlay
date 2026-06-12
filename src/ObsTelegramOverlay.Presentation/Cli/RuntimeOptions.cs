namespace ObsTelegramOverlay.Presentation.Cli;

public sealed record RuntimeOptions(
    string BotApiToken,
    string ListenUrl,
    string? OverlayTemplatePath,
    int HistoryLimit,
    int MessageTtlSeconds,
    IReadOnlySet<long> AllowedChatIds,
    bool SpeechEnabled,
    string SpeechEngine,
    string SpeechLang,
    string SpeechLangMode,
    string? SpeechVoice
);
