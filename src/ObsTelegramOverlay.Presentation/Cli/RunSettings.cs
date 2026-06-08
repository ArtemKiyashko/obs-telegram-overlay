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
}
