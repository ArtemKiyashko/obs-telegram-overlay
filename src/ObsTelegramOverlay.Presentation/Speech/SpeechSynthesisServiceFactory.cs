using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Factory for resolving the appropriate speech synthesis service based on engine type and platform.
/// </summary>
public sealed class SpeechSynthesisServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SpeechSynthesisServiceFactory> _logger;

    public SpeechSynthesisServiceFactory(IServiceProvider serviceProvider, ILogger<SpeechSynthesisServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a speech synthesis service based on the requested engine type.
    /// </summary>
    /// <param name="engineType">Engine type: local, piper, or edge-tts</param>
    /// <param name="errorMessage">Contains error message if resolution failed.</param>
    /// <returns>A resolved service or null if unavailable.</returns>
    public ISpeechSynthesisService? ResolveSpeechService(string engineType, out string? errorMessage)
    {
        errorMessage = null;

        if (engineType == "piper")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                errorMessage = "Piper engine is only supported on Linux.";
                return null;
            }

            var piperService = _serviceProvider.GetRequiredService<PiperSpeechSynthesisService>();
            if (piperService.IsAvailable(out var reason))
            {
                return piperService;
            }

            errorMessage = $"Piper requested but unavailable: {reason}";
            return null;
        }

        if (engineType == "edge-tts")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                errorMessage = "edge-tts engine is only supported on Linux.";
                return null;
            }

            var edgeTtsService = _serviceProvider.GetRequiredService<EdgeTtsSpeechSynthesisService>();
            if (edgeTtsService.IsAvailable(out var reason))
            {
                return edgeTtsService;
            }

            errorMessage = $"edge-tts requested but unavailable: {reason}";
            return null;
        }

        if (engineType == "local")
        {
            var localService = ResolveLocalService();
            if (localService.IsAvailable(out var reason))
            {
                return localService;
            }

            errorMessage = $"Local speech requested but unavailable: {reason}";
            return null;
        }

        errorMessage = $"Unknown speech engine: {engineType}";
        return null;
    }

    private ISpeechSynthesisService ResolveLocalService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return _serviceProvider.GetRequiredService<MacOsSpeechSynthesisService>();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return _serviceProvider.GetRequiredService<LinuxSpeechSynthesisService>();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return _serviceProvider.GetRequiredService<WindowsSpeechSynthesisService>();
        }

        throw new PlatformNotSupportedException("Speech synthesis is not supported on this platform.");
    }
}
