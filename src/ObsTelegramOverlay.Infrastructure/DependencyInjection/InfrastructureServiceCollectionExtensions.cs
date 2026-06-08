using Microsoft.Extensions.DependencyInjection;
using ObsTelegramOverlay.Infrastructure.Services;
using Telegram.Bot;

namespace ObsTelegramOverlay.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramPolling(this IServiceCollection services, string botApiToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botApiToken);

        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botApiToken));
        services.AddHostedService<TelegramPollingBackgroundService>();

        return services;
    }
}
