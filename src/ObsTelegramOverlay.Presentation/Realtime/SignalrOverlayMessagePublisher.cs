using Microsoft.AspNetCore.SignalR;
using ObsTelegramOverlay.Application.Abstractions;
using ObsTelegramOverlay.Application.Models;

namespace ObsTelegramOverlay.Presentation.Realtime;

public sealed class SignalrOverlayMessagePublisher : IOverlayMessagePublisher
{
    private readonly IHubContext<OverlayHub> _hubContext;
    private readonly OverlayMessageStore _store;

    public SignalrOverlayMessagePublisher(IHubContext<OverlayHub> hubContext, OverlayMessageStore store)
    {
        _hubContext = hubContext;
        _store = store;
    }

    public async Task PublishAsync(OverlayMessage message, CancellationToken cancellationToken)
    {
        _store.Add(message);
        await _hubContext.Clients.All.SendAsync("overlay-message", message, cancellationToken);
    }
}
