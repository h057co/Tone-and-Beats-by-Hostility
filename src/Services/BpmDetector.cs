using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Services;
using BpmFinder;
using System;
using System.IO;

namespace AudioAnalyzer.Services;

public class BpmDetector : IBpmDetectorService
{
    private readonly WaveformAnalyzer _waveformAnalyzer = new();

    public async Task<double> DetectBpmAsync(string filePath, IProgress<int>? progress = null)
    {
        try
        {
            return await Task.Run(async () => await DetectBpmInternalAsync(filePath, progress));
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmAsync - Error: {ex.Message}");
            return 0;
        }
    }

    public double DetectBpm(string filePath, IProgress<int>? progress = null)
    {
        // Sync wrapper for backward compatibility - delegates to async implementation
        return Task.Run(async () => await DetectBpmInternalAsync(filePath, progress)).GetAwaiter().GetResult();
    }

    private async Task<double> DetectBpmInternalAsync(string filePath, IProgress<int>? progress = null)
    {
        try
        {
            LoggerService.Log($"BpmDetector.DetectBpm - Iniciando para: {filePath}");
            progress?.Report(10);

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            double bpmFinderBpm = 0;

            if (extension == ".mp3" || extension == ".wav")
            {
                var options = new BpmAnalysisOptions
                {
                    MinBpm = 50,
                    MaxBpm = 220,
                    TrimLeadingSilence = true,
                    PreferStableTempo = true
                };

                var bpmFinderResult = await BpmAnalyzer.AnalyzeFileAsync(filePath, options);
                bpmFinderBpm = bpmFinderResult.Bpm > 0 ? Math.Round(bpmFinderResult.Bpm, 1) : 0;
                LoggerService.Log($"BpmDetector.DetectBpm - BpmFinder result: {bpmFinderBpm}");
            }
            else
            {
                LoggerService.Log($"BpmDetector.DetectBpm - Formato no soportado por BpmFinder: {extension}, usando algoritmo avanzado");
            }

            progress?.Report(30);

            if (bpmFinderBpm <= 0)
            {
                LoggerService.Log($"BpmDetector.DetectBpm - Usando algoritmo avanzado para: {filePath}");
                var advancedResult = GetAdvancedBpm(filePath);
                bpmFinderBpm = advancedResult.bpm;
                LoggerService.Log($"BpmDetector.DetectBpm - Advanced BPM result: {bpmFinderBpm}");
            }

            progress?.Report(90);

            double finalBpm = bpmFinderBpm;
            LoggerService.Log($"BpmDetector.DetectBpm - Final BPM: {finalBpm}");

            progress?.Report(100);

            return finalBpm;
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpm - Exception: {ex.Message}");
            return 0;
        }
    }

    private (double bpm, double confidence) GetAdvancedBpm(string filePath)
    {
        try
        {
            var (sampleProvider, waveStream) = AudioReaderFactory.CreateReader(filePath);
            using (waveStream)
            {
                var sampleRate = waveStream.WaveFormat.SampleRate;
                var channels = waveStream.WaveFormat.Channels;
                var estimatedMonoSamples = (int)(waveStream.Length / sizeof(float) / channels);
                var samples = new List<float>(estimatedMonoSamples);
                var buffer = new float[waveStream.Length / sizeof(float)];
                int read = sampleProvider.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < read; i += waveStream.WaveFormat.Channels)
                {
                    float sum = 0;
                    for (int c = 0; c < waveStream.WaveFormat.Channels && i + c < read; c++)
                        sum += buffer[i + c];
                    samples.Add(sum / waveStream.WaveFormat.Channels);
                }

                return _waveformAnalyzer.DetectBpmWithConfidence(samples.ToArray(), sampleRate);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.GetAdvancedBpm - Error: {ex.Message}");
            return (0, 0);
        }
    }

    private double CombineBpmResults(double bpmFinderBpm, double advancedBpm, double advancedConfidence)
    {
        if (bpmFinderBpm <= 0 && advancedBpm <= 0)
            return 0;

        if (bpmFinderBpm <= 0)
            return advancedBpm;

        if (advancedBpm <= 0 || advancedConfidence < 0.3)
            return bpmFinderBpm;

        double bpmDiff = Math.Abs(bpmFinderBpm - advancedBpm);
        double harmonicDiff = Math.Min(
            Math.Abs(bpmFinderBpm - advancedBpm * 2),
            Math.Abs(bpmFinderBpm * 2 - advancedBpm)
        );
        harmonicDiff = Math.Min(harmonicDiff, Math.Abs(bpmFinderBpm - advancedBpm / 2));

        if (bpmDiff < 3)
        {
            return Math.Round((bpmFinderBpm + advancedBpm) / 2, 1);
        }

        if (harmonicDiff < 3 && advancedConfidence > 0.6)
        {
            return advancedBpm;
        }

        double bpmWeight = 0.6;
        double advancedWeight = 0.4 * advancedConfidence;
        double totalWeight = bpmWeight + advancedWeight;

        return Math.Round((bpmFinderBpm * bpmWeight + advancedBpm * advancedWeight) / totalWeight, 1);
    }
}
