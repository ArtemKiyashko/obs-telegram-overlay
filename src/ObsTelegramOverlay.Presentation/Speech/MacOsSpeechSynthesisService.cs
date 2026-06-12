using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// macOS-specific speech synthesis using the native 'say' command.
/// </summary>
public sealed class MacOsSpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<MacOsSpeechSynthesisService> _logger;

    public MacOsSpeechSynthesisService(ILogger<MacOsSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        if (IsCommandAvailable("say"))
        {
            reason = "ok";
            return true;
        }

        reason = "macOS speech requires 'say' utility (should be built-in).";
        return false;
    }

    public async Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, string? voice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsAvailable(out var reason))
        {
            _logger.LogWarning("macOS speech is unavailable: {Reason}", reason);
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");
        var selectedVoice = string.IsNullOrWhiteSpace(voice)
            ? (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "Milena" : "Samantha")
            : voice.Trim();

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "say",
                ArgumentList = { "-v", selectedVoice, "-o", outputPath, "--data-format=LEI16@44100", text },
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _logger.LogError("Failed to start 'say' process");
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'say' process exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("'say' output file not created: {OutputPath}", outputPath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return new SpeechSynthesisResult(bytes, "audio/wav");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech on macOS");
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
