using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Speech synthesis via Piper TTS (local neural voices, Linux-only).
/// Requires: pip install piper-tts
/// </summary>
public sealed class PiperSpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<PiperSpeechSynthesisService> _logger;

    public PiperSpeechSynthesisService(ILogger<PiperSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        if (!IsCommandAvailable("piper"))
        {
            reason = "Piper CLI not found. Install with: pip install piper-tts";
            return false;
        }

        reason = "ok";
        return true;
    }

    public async Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, string? voice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsAvailable(out var reason))
        {
            _logger.LogWarning("Piper TTS is unavailable: {Reason}", reason);
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");

        try
        {
            // Use explicit override when provided; otherwise fall back to a sensible per-language default.
            var voiceModel = ResolveVoiceModel(lang, voice);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "piper",
                ArgumentList =
                {
                    "--model", voiceModel,
                    "--output-file", outputPath,
                    "--quiet"
                },
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _logger.LogError("Failed to start 'piper' process");
                return null;
            }

            // Write text to stdin
            await process.StandardInput.WriteLineAsync(text);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'piper' process exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("'piper' output file not created: {OutputPath}", outputPath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return new SpeechSynthesisResult(bytes, "audio/wav");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech via Piper");
            return null;
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    private static string ResolveVoiceModel(string lang, string? voice)
    {
        if (!string.IsNullOrWhiteSpace(voice))
        {
            return voice.Trim();
        }

        if (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return "ru_RU-dmitri_bozhinskiy-medium";
        }

        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en_GB-alan-medium";
        }

        // Fallback
        return "en_GB-alan-medium";
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
