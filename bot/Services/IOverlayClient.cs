using ObsTelegramOverlay.Bot.Models;

namespace ObsTelegramOverlay.Bot.Services;

public interface IOverlayClient
{
    Task NewMessage(OverlayChatMessage message);
}