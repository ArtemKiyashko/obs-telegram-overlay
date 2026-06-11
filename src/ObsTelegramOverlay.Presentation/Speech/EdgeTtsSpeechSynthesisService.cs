using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Speech synthesis via edge-tts (Microsoft neural voices via Edge).
/// Requires: pip install edge-tts
/// Requires internet connection.
/// </summary>
public sealed class EdgeTtsSpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<EdgeTtsSpeechSynthesisService> _logger;

    public EdgeTtsSpeechSynthesisService(ILogger<EdgeTtsSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        if (!IsCommandAvailable("edge-tts"))
        {
            reason = "edge-tts CLI not found. Install with: pip install edge-tts";
            return false;
        }

        reason = "ok";
        return true;
    }

    public async Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsAvailable(out var reason))
        {
            _logger.LogWarning("edge-tts is unavailable: {Reason}", reason);
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.mp3");

        try
        {
            var voice = ResolveVoice(lang);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "edge-tts",
                ArgumentList =
                {
                    "--voice", voice,
                    "--write-media", outputPath,
                    text
                },
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _logger.LogError("Failed to start 'edge-tts' process");
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'edge-tts' process exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("'edge-tts' output file not created: {OutputPath}", outputPath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return new SpeechSynthesisResult(bytes, "audio/mpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech via edge-tts");
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

    private static string ResolveVoice(string lang)
    {
        if (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return "ru-RU-DariyaNeural";
        }

        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return "en-US-AriaNeural";
        }

        return "en-US-AriaNeural";
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
