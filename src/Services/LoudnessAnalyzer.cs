using System.IO;
using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;
using System.Diagnostics;

namespace AudioAnalyzer.Services;

public class LoudnessAnalyzer : ILoudnessAnalyzerService
{
    public async Task<LoudnessResult> AnalyzeAsync(string filePath, IProgress<int>? progress = null)
    {
        return await Task.Run(() => Analyze(filePath, progress));
    }

    public LoudnessResult Analyze(string filePath, IProgress<int>? progress = null)
    {
        var result = new LoudnessResult();

        try
        {
            LoggerService.Log("LoudnessAnalyzer.Analyze - Iniciando para: " + filePath);
            progress?.Report(10);

            var ffmpegPath = FindFFmpeg();
            LoggerService.Log("LoudnessAnalyzer - Using FFmpeg: " + ffmpegPath);

            progress?.Report(30);

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-threads 0 -hide_banner -i \"{filePath}\" -af \"loudnorm=I=-23:print_format=json\" -f null -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("No se pudo iniciar FFmpeg");

            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(180000);

            LoggerService.Log("LoudnessAnalyzer - FFmpeg exit code: " + process.ExitCode);

            progress?.Report(70);

            result = ParseLoudnormOutput(output);

            progress?.Report(100);

            LoggerService.Log("LoudnessAnalyzer - Resultado: LUFS=" + result.IntegratedLufs + ", LRA=" + result.ShortTermLufs + ", TP=" + result.TruePeak);
        }
        catch (Exception ex)
        {
            LoggerService.Log("LoudnessAnalyzer.Analyze - Exception: " + ex.Message);
        }

        return result;
    }

    private string FindFFmpeg()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "publish", "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "publish", "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg.exe"),
            "ffmpeg"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                LoggerService.Log("LoudnessAnalyzer.FindFFmpeg - Found: " + path);
                return path;
            }
        }

        LoggerService.Log("LoudnessAnalyzer.FindFFmpeg - Using system ffmpeg");
        return "ffmpeg";
    }

    private double ExtractValue(string output, string key)
    {
        // Try JSON format first (key in quotes)
        var keyIndex = output.IndexOf("\"" + key + "\"");
        if (keyIndex >= 0)
        {
            var afterKey = output.Substring(keyIndex);
            var colonIndex = afterKey.IndexOf(':');
            if (colonIndex < 0)
                return 0;

            var valuePart = afterKey.Substring(colonIndex + 1).Trim();
            if (valuePart.StartsWith("\""))
            {
                var endQuote = valuePart.IndexOf('"', 1);
                if (endQuote > 0)
                    valuePart = valuePart.Substring(1, endQuote - 1);
            }

            if (double.TryParse(valuePart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;
        }

        // Try ebur128 format (I: -7.6 LUFS or Peak: 2.9 dBFS)
        var simpleKeyIndex = output.LastIndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (simpleKeyIndex >= 0)
        {
            var afterKey = output.Substring(simpleKeyIndex);
            var colonIndex = afterKey.IndexOf(':');
            if (colonIndex < 0)
                return 0;

            var valuePart = afterKey.Substring(colonIndex + 1).Trim();
            
            // Extract numeric part (e.g., "-7.6 LUFS" -> "-7.6", "2.9 dBFS" -> "2.9")
            var numericEnd = 0;
            for (int i = 0; i < valuePart.Length; i++)
            {
                char c = valuePart[i];
                if ((c == '-' || c == '.' || char.IsDigit(c)) && i > numericEnd)
                    numericEnd = i + 1;
                else if (numericEnd > 0 && !char.IsDigit(valuePart[i]) && valuePart[i] != '.' && valuePart[i] != '-')
                    break;
            }

            if (numericEnd > 0)
            {
                var numStr = valuePart.Substring(0, numericEnd).Trim();
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
            }
        }

        return 0;
    }

    private LoudnessResult ParseLoudnormOutput(string output)
    {
        var result = new LoudnessResult();

        if (string.IsNullOrWhiteSpace(output))
        {
            LoggerService.Log("LoudnessAnalyzer - Output vacio");
            return result;
        }

        try
        {
            // loudnorm format (JSON)
            result.IntegratedLufs = ExtractValue(output, "input_i");
            result.TruePeak = ExtractValue(output, "input_tp");
            result.ShortTermLufs = ExtractValue(output, "input_lra");

            LoggerService.Log("LoudnessAnalyzer - input_i (Integrated): " + result.IntegratedLufs + " LUFS");
            LoggerService.Log("LoudnessAnalyzer - input_tp (True Peak): " + result.TruePeak + " dBTP");
            LoggerService.Log("LoudnessAnalyzer - input_lra (LRA): " + result.ShortTermLufs + " LUFS");

            if (result.ShortTermLufs == 0)
            {
                result.ShortTermLufs = result.IntegratedLufs;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log("LoudnessAnalyzer.ParseLoudnormOutput - Error: " + ex.Message);
        }

        return result;
    }
}