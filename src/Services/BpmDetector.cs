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

            double advancedBpm = 0;
            double advancedConfidence = 0;
            
            var advancedResult = GetAdvancedBpm(filePath);
            advancedBpm = advancedResult.bpm;
            advancedConfidence = advancedResult.confidence;
            LoggerService.Log($"BpmDetector.DetectBpm - Advanced BPM result: {advancedBpm} (conf: {advancedConfidence})");
            
            if (bpmFinderBpm > 0)
            {
                bpmFinderBpm = SelectBestBpm(bpmFinderBpm, advancedBpm, advancedConfidence);
                LoggerService.Log($"BpmDetector.DetectBpm - BPM after comparison: {bpmFinderBpm}");
            }
            else
            {
                bpmFinderBpm = advancedBpm;
            }

            progress?.Report(90);

            double finalBpm = ApplyHarmonicCorrection(bpmFinderBpm);
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

    private double ApplyHarmonicCorrection(double bpm)
    {
        if (bpm <= 0) return bpm;

        if (bpm > 150)
        {
            double[] divisors = { 2.0, 3.0, 1.5 };
            foreach (var div in divisors)
            {
                double corrected = bpm / div;
                if (corrected >= 60 && corrected <= 140)
                {
                    LoggerService.Log($"BpmDetector.ApplyHarmonicCorrection - {bpm} -> {corrected} (divided by {div})");
                    return Math.Round(corrected * 2) / 2;
                }
            }
        }

        if (bpm < 55)
        {
            double[] multipliers = { 2.0, 3.0 };
            foreach (var mult in multipliers)
            {
                double corrected = bpm * mult;
                if (corrected >= 60 && corrected <= 180)
                {
                    LoggerService.Log($"BpmDetector.ApplyHarmonicCorrection - {bpm} -> {corrected} (multiplied by {mult})");
                    return Math.Round(corrected * 2) / 2;
                }
            }
        }

        return bpm;
    }

    private double SelectBestBpm(double bpmFinderBpm, double advancedBpm, double advancedConfidence)
    {
        if (bpmFinderBpm <= 0) return advancedBpm;
        if (advancedBpm <= 0) return bpmFinderBpm;

        bool bfInDjRange = bpmFinderBpm >= 60 && bpmFinderBpm <= 180;
        bool advInDjRange = advancedBpm >= 60 && advancedBpm <= 180;

        double ratio = bpmFinderBpm / advancedBpm;
        bool isHarmonic = IsHarmonicRatio(ratio);

        if (isHarmonic)
        {
            if (bfInDjRange && !advInDjRange)
            {
                LoggerService.Log($"BpmDetector.SelectBestBpm - Using BpmFinder {bpmFinderBpm} (advanced {advancedBpm} out of DJ range)");
                return bpmFinderBpm;
            }
            if (advInDjRange && !bfInDjRange)
            {
                LoggerService.Log($"BpmDetector.SelectBestBpm - Using advanced {advancedBpm} (BpmFinder {bpmFinderBpm} out of DJ range)");
                return advancedBpm;
            }
            if (advInDjRange && bfInDjRange && advancedConfidence > 0.5)
            {
                LoggerService.Log($"BpmDetector.SelectBestBpm - Using advanced {advancedBpm} (harmonic match, higher confidence)");
                return advancedBpm;
            }
        }

        double diff = Math.Abs(bpmFinderBpm - advancedBpm);
        if (diff < 3) return Math.Round((bpmFinderBpm + advancedBpm) / 2, 1);

        return bpmFinderBpm;
    }

    private bool IsHarmonicRatio(double ratio)
    {
        double[] harmonicMultipliers = { 0.5, 0.67, 0.75, 1.0, 1.33, 1.5, 2.0, 3.0 };
        foreach (var mult in harmonicMultipliers)
        {
            if (Math.Abs(ratio - mult) < 0.1) return true;
        }
        return false;
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
