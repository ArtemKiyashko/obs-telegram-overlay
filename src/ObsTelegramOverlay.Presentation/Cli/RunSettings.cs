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

    [Description("Speech engine: local|piper|edge-tts. On Linux: local uses espeak-ng, piper/edge-tts use neural voices. On macOS/Windows: only local is supported (uses native TTS). Local requires: macOS=say, Linux=espeak-ng, Windows=PowerShell/System.Speech. Piper requires: pip install piper-tts. edge-tts requires: pip install edge-tts + internet.")]
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

    [Description("Speech voice override shared by supported TTS engines. Leave empty to use the engine default.")]
    [CommandOption("--speech-voice <VOICE>")]
    public string? SpeechVoice { get; init; }
}
