using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ObsTelegramOverlay.Bot.Models;
using ObsTelegramOverlay.Bot.Services;

namespace ObsTelegramOverlay.Bot.Functions;

public sealed class OverlayNegotiationFunction
{
    private readonly OverlayRegistryService _overlayRegistryService;
    private readonly SignalRService _signalRService;
    private readonly ILogger<OverlayNegotiationFunction> _logger;

    public OverlayNegotiationFunction(
        OverlayRegistryService overlayRegistryService,
        SignalRService signalRService,
        ILogger<OverlayNegotiationFunction> logger)
    {
        _overlayRegistryService = overlayRegistryService;
        _signalRService = signalRService;
        _logger = logger;
    }

    [Function(nameof(OverlayNegotiationFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "overlay/{overlayId}/negotiate")] HttpRequestData request,
        string overlayId,
        CancellationToken cancellationToken)
    {
        var overlay = await _overlayRegistryService.GetOverlayAsync(overlayId, cancellationToken);
        if (overlay is null || overlay.Status != OverlayStatuses.Active)
        {
            _logger.LogWarning("Negotiate rejected for overlay {OverlayId}", overlayId);
            var notFound = request.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Overlay not found.", cancellationToken);
            return notFound;
        }

        var negotiationResult = await _signalRService.NegotiateAsync(overlayId, cancellationToken);
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(negotiationResult, cancellationToken);
        return response;
    }
}