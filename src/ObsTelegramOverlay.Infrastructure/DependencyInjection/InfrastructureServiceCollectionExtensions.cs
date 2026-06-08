using Microsoft.Extensions.DependencyInjection;
using ObsTelegramOverlay.Application.Abstractions;
using ObsTelegramOverlay.Infrastructure.Services;
using Telegram.Bot;

namespace ObsTelegramOverlay.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTelegramPolling(this IServiceCollection services, string botApiToken, IReadOnlySet<long> allowedChatIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(botApiToken);
        ArgumentNullException.ThrowIfNull(allowedChatIds);

        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient(botApiToken));
        services.AddHostedService(serviceProvider => new TelegramPollingBackgroundService(
            serviceProvider.GetRequiredService<ITelegramBotClient>(),
            serviceProvider.GetRequiredService<IOverlayMessagePublisher>(),
            allowedChatIds,
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TelegramPollingBackgroundService>>()));

        return services;
    }
}
