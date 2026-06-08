using ObsTelegramOverlay.Presentation.Cli;
using Spectre.Console.Cli;

var app = new CommandApp<RunCommand>();

app.Configure(config =>
{
	config.SetApplicationName("obs-telegram-overlay");
	config.ValidateExamples();
	config.AddExample([
		"--bot-api-token", "<token>",
		"--listen-url", "http://127.0.0.1:5180"
	]);
});

return await app.RunAsync(args);
