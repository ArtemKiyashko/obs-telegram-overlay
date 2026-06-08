using System.ComponentModel;
using Spectre.Console.Cli;

namespace ObsTelegramOverlay.Presentation.Cli;

public sealed class RunSettings : CommandSettings
{
    [CommandOption("--bot-api-token <TOKEN>")]
    public string? BotApiToken { get; init; }

    [CommandOption("--listen-url <URL>")]
    [DefaultValue("http://127.0.0.1:5180")]
    public string ListenUrl { get; init; } = "http://127.0.0.1:5180";

    [CommandOption("--overlay-template <PATH>")]
    public string? OverlayTemplatePath { get; init; }

    [CommandOption("--history-limit <COUNT>")]
    [DefaultValue(100)]
    public int HistoryLimit { get; init; } = 100;

    [CommandOption("--message-ttl-seconds <SECONDS>")]
    [DefaultValue(10)]
    public int MessageTtlSeconds { get; init; } = 10;

    [Description("Comma-separated list of allowed Telegram chat IDs. If omitted, messages from all chats are accepted.")]
    [CommandOption("--allowed-chat-ids <IDS>")]
    public string? AllowedChatIds { get; init; }

    [Description("Legacy compatibility switch. Speech stays enabled, engine selection still follows --speech-engine.")]
    [CommandOption("--speech-enabled")]
    [DefaultValue(false)]
    public bool SpeechEnabled { get; init; }

    [Description("Speech engine: browser|local. local requires utilities by OS (macOS: say, Linux: espeak-ng, Windows: PowerShell/System.Speech). If local is requested but unavailable, fallback to browser.")]
    [CommandOption("--speech-engine <ENGINE>")]
    [DefaultValue("local")]
    public string SpeechEngine { get; init; } = "local";

    [CommandOption("--speech-lang <LANG>")]
    [DefaultValue("ru-RU")]
    public string SpeechLang { get; init; } = "ru-RU";

    [Description("Speech language mode: fixed|auto. auto selects ru-RU for Cyrillic text and en-US otherwise.")]
    [CommandOption("--speech-lang-mode <MODE>")]
    [DefaultValue("auto")]
    public string SpeechLangMode { get; init; } = "auto";
}
