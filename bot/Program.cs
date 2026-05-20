using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ObsTelegramOverlay.Bot.Configuration;
using ObsTelegramOverlay.Bot.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(BotOptions.SectionName));
builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["OverlayStorageConnectionString"] ?? configuration["AzureWebJobsStorage"];

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Storage connection string is not configured. Set OverlayStorageConnectionString or AzureWebJobsStorage.");
    }

    return new Azure.Storage.Blobs.BlobServiceClient(connectionString);
});

builder.Services
    .AddHttpClient()
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<OverlayRegistryService>();
builder.Services.AddSingleton<SignalRService>();
builder.Services.AddSingleton<TelegramBotApiClient>();

builder.Build().Run();
