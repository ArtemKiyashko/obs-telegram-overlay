using System.Collections.Concurrent;
using ObsTelegramOverlay.Application.Models;

namespace ObsTelegramOverlay.Presentation.Realtime;

public sealed class OverlayMessageStore
{
    private readonly ConcurrentQueue<OverlayMessage> _messages = new();
    private readonly int _historyLimit;

    public OverlayMessageStore(int historyLimit)
    {
        _historyLimit = historyLimit;
    }

    public void Add(OverlayMessage message)
    {
        _messages.Enqueue(message);

        while (_messages.Count > _historyLimit && _messages.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<OverlayMessage> Snapshot()
    {
        return _messages.ToArray();
    }
}
