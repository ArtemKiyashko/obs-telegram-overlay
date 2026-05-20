using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObsTelegramOverlay.Bot.Configuration;
using ObsTelegramOverlay.Bot.Models;

namespace ObsTelegramOverlay.Bot.Services;

public sealed class SignalRService : IAsyncDisposable
{
    private readonly BotOptions _options;
    private readonly ILogger<SignalRService> _logger;
    private readonly Lazy<ServiceManager> _serviceManagerFactory;
    private readonly Lazy<Task<ServiceHubContext<IOverlayClient>>> _hubContextFactory;

    public SignalRService(IOptions<BotOptions> options, ILogger<SignalRService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _serviceManagerFactory = new Lazy<ServiceManager>(CreateServiceManager);
        _hubContextFactory = new Lazy<Task<ServiceHubContext<IOverlayClient>>>(CreateHubContextAsync);
    }

    public async Task<SignalRNegotiationResult> NegotiateAsync(string overlayId, CancellationToken cancellationToken)
    {
        var hubContext = await _hubContextFactory.Value;
        var response = await hubContext.NegotiateAsync(new NegotiationOptions
        {
            UserId = overlayId,
            CloseOnAuthenticationExpiration = true,
            TokenLifetime = TimeSpan.FromMinutes(_options.SignalRTokenLifetimeMinutes)
        }, cancellationToken);

        return new SignalRNegotiationResult
        {
            Url = response.Url ?? string.Empty,
            AccessToken = response.AccessToken ?? string.Empty
        };
    }

    public async Task PublishMessageAsync(string overlayId, OverlayChatMessage message, CancellationToken cancellationToken)
    {
        var hubContext = await _hubContextFactory.Value;
        await hubContext.Clients.User(overlayId).NewMessage(message);
        await Task.CompletedTask;
    }

    public async Task CloseOverlayConnectionsAsync(string overlayId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Closing SignalR connections for overlay {OverlayId}", overlayId);
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_hubContextFactory.IsValueCreated)
        {
            return;
        }

        var hubContext = await _hubContextFactory.Value;
        await hubContext.DisposeAsync();
    }

    private Task<ServiceHubContext<IOverlayClient>> CreateHubContextAsync()
    {
        return _serviceManagerFactory.Value.CreateHubContextAsync<IOverlayClient>(_options.SignalRHubName, CancellationToken.None);
    }

    private ServiceManager CreateServiceManager()
    {
        if (string.IsNullOrWhiteSpace(_options.SignalRConnectionString))
        {
            throw new InvalidOperationException("Bot:SignalRConnectionString is not configured.");
        }

        return new ServiceManagerBuilder()
            .WithOptions(options =>
            {
                options.ConnectionString = _options.SignalRConnectionString;
            })
            .BuildServiceManager();
    }
}