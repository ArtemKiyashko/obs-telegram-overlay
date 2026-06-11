using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

/// <summary>
/// Windows-specific speech synthesis using System.Speech via PowerShell.
/// </summary>
public sealed class WindowsSpeechSynthesisService : ISpeechSynthesisService
{
    private readonly ILogger<WindowsSpeechSynthesisService> _logger;

    public WindowsSpeechSynthesisService(ILogger<WindowsSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        reason = "ok";
        return true;
    }

    public async Task<SpeechSynthesisResult?> SynthesizeAsync(string text, string lang, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");

        try
        {
            var escapedText = text.Replace("'", "''", StringComparison.Ordinal);
            var escapedPath = outputPath.Replace("'", "''", StringComparison.Ordinal);
            var escapedCulture = (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru-RU" : "en-US")
                .Replace("'", "''", StringComparison.Ordinal);

            var script = string.Join(';',
                "Add-Type -AssemblyName System.Speech",
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer",
                $"$s.SelectVoiceByHints([System.Speech.Synthesis.VoiceGender]::NotSet,[System.Speech.Synthesis.VoiceAge]::NotSet,0,'{escapedCulture}')",
                $"$s.SetOutputToWaveFile('{escapedPath}')",
                $"$s.Speak('{escapedText}')",
                "$s.Dispose()"
            );

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                ArgumentList = { "-NoProfile", "-Command", script },
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is null)
            {
                _logger.LogError("Failed to start PowerShell process");
                return null;
            }

            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogError("PowerShell process exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogError("PowerShell output file not created: {OutputPath}", outputPath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            return new SpeechSynthesisResult(bytes, "audio/wav");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech on Windows");
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
}
