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
	config.AddExample([
		"--bot-api-token", "<token>",
		"--allowed-chat-ids", "-1001234567890,-1009876543210"
	]);
});

return await app.RunAsync(args);
