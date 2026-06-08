using ObsTelegramOverlay.Application.Models;

namespace ObsTelegramOverlay.Application.Abstractions;

public interface IOverlayMessagePublisher
{
    Task PublishAsync(OverlayMessage message, CancellationToken cancellationToken);
}
