namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Abstracts speech synthesis across different engines and platforms.
/// </summary>
public interface ISpeechSynthesisService
{
    /// <summary>
    /// Checks if the service is available and can synthesize speech.
    /// </summary>
    /// <param name="reason">Human-readable reason if unavailable.</param>
    /// <returns>True if available, false otherwise.</returns>
    bool IsAvailable(out string reason);

    /// <summary>
    /// Synthesizes text to speech.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="lang">Language code (e.g., "ru-RU", "en-US").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Synthesized audio or null if synthesis failed.</returns>
    Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, CancellationToken cancellationToken);
}
