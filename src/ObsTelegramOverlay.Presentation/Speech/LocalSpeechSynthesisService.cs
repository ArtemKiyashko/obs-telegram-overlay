using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ObsTelegramOverlay.Presentation.Speech;

public sealed class LocalSpeechSynthesisService
{
    private readonly ILogger<LocalSpeechSynthesisService> _logger;

    public LocalSpeechSynthesisService(ILogger<LocalSpeechSynthesisService> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable(out string reason)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (IsCommandAvailable("say"))
            {
                reason = "ok";
                return true;
            }

            reason = "macOS local speech requires 'say' utility.";
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (IsCommandAvailable("espeak-ng"))
            {
                reason = "ok";
                return true;
            }

            reason = "Linux local speech requires 'espeak-ng' utility.";
            return false;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            reason = "ok";
            return true;
        }

        reason = "Local speech is not supported on this OS.";
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
            _logger.LogWarning("Local speech is unavailable: {Reason}", reason);
            return null;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await SynthesizeOnMacOsAsync(text, lang, cancellationToken);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await SynthesizeOnLinuxAsync(text, lang, cancellationToken);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await SynthesizeOnWindowsAsync(text, lang, cancellationToken);
        }

        return null;
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

    private async Task<SpeechSynthesisResult?> SynthesizeOnMacOsAsync(string text, string lang, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");
        var voice = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "Milena" : "Samantha";

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "say",
                ArgumentList = { "-v", voice, "-o", outputPath, "--data-format=LEI16@44100", text },
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

    private async Task<SpeechSynthesisResult?> SynthesizeOnLinuxAsync(string text, string lang, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"obstelegramoverlay-{Guid.NewGuid():N}.wav");
        var voice = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "espeak-ng",
                ArgumentList = { "-v", voice, "-w", outputPath, text },
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

    private async Task<SpeechSynthesisResult?> SynthesizeOnWindowsAsync(string text, string lang, CancellationToken cancellationToken)
    {
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
