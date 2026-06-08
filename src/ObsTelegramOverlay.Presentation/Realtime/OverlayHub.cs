using Microsoft.AspNetCore.SignalR;

namespace ObsTelegramOverlay.Presentation.Realtime;

public sealed class OverlayHub : Hub
{
    private readonly OverlayMessageStore _store;

    public OverlayHub(OverlayMessageStore store)
    {
        _store = store;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("overlay-snapshot", _store.Snapshot(), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
