using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Linux-specific speech synthesis using espeak-ng.
/// </summary>
public sealed class LinuxSpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<LinuxSpeechSynthesisService> _logger;

    public LinuxSpeechSynthesisService(ILogger<LinuxSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        if (IsCommandAvailable("espeak-ng"))
        {
            reason = "ok";
            return true;
        }

        reason = "Linux speech requires 'espeak-ng' (install: apt install espeak-ng).";
        return false;
    }

    public async Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!IsAvailable(out var reason))
        {
            _logger.LogWarning("Linux speech is unavailable: {Reason}", reason);
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");
        var voice = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en-us";
        var normalizedText = NormalizeLinuxSpeechText(text);

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "espeak-ng",
                ArgumentList =
                {
                    "-b", "1", // Force UTF-8 input decoding
                    "-v", voice,
                    "-s", "120", // Slightly faster than 115 but still readable
                    "-p", "28", // Lower pitch to avoid squeaky voice
                    "-g", "5", // Moderate word gap without robotic pauses
                    "-w", outputPath,
                    normalizedText
                },
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _logger.LogError("Failed to start 'espeak-ng' process");
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("'espeak-ng' process exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("'espeak-ng' output file not created: {OutputPath}", outputPath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return new SpeechSynthesisResult(bytes, "audio/wav");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech on Linux");
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

    private static string NormalizeLinuxSpeechText(string text)
    {
        var normalized = text.Trim();
        normalized = Regex.Replace(normalized, @"https?://\S+", " ссылка ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"\s+", " ");

        return string.IsNullOrWhiteSpace(normalized) ? text : normalized;
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
