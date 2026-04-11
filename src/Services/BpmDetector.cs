using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Services;
using SoundTouch;
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
            var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
            return await Task.Run(() => DetectBpmFromSamples(monoSamples, sampleRate, progress));
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmAsync - Error: {ex.Message}");
            return 0;
        }
    }

    public async Task<double> DetectBpmAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        try
        {
            return await Task.Run(() => DetectBpmFromSamples(monoSamples, sampleRate, progress));
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmAsync(samples) - Error: {ex.Message}");
            return 0;
        }
    }

    public double DetectBpm(string filePath, IProgress<int>? progress = null)
    {
        var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
        return DetectBpmFromSamples(monoSamples, sampleRate, progress);
    }

    /// <summary>
    /// Core BPM detection logic operating on pre-loaded mono samples.
    /// No file I/O occurs in this method.
    /// </summary>
    private double DetectBpmFromSamples(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        try
        {
            LoggerService.Log($"BpmDetector.DetectBpmFromSamples - {monoSamples.Length} samples @ {sampleRate}Hz");
            progress?.Report(5);

            if (monoSamples.Length < sampleRate * 5)
            {
                LoggerService.Log("BpmDetector - Audio too short for analysis");
                return 0;
            }

            progress?.Report(15);

            // === Step 1: SoundTouch quick BPM from mono samples in memory ===
            double soundTouchBpm = DetectWithSoundTouchFromSamples(monoSamples, sampleRate);
            LoggerService.Log($"BpmDetector - SoundTouch quick estimate: {soundTouchBpm}");

            progress?.Report(35);

            // === Step 2: Select analysis segment (post-intro, up to 60s / ~32 bars) ===
            double initialBpm = soundTouchBpm > 0 ? soundTouchBpm : 120;
            var segment = SelectAnalysisSegment(monoSamples, sampleRate, initialBpm);
            LoggerService.Log($"BpmDetector - Analysis segment: {segment.Length} samples ({segment.Length / (double)sampleRate:F1}s)");

            progress?.Report(45);

            // === Step 3: Transient-based BPM with Beat Grid Fitting ===
            var (gridBpm, gridConfidence) = _waveformAnalyzer.DetectBpmByTransientGrid(segment, sampleRate);
            LoggerService.Log($"BpmDetector - TransientGrid result: {gridBpm:F1} BPM (conf: {gridConfidence:F2})");

            progress?.Report(80);

            // === Step 4: Select final BPM ===
            double finalBpm;
            if (gridBpm > 0 && gridConfidence > 0.15)
            {
                if (soundTouchBpm > 0)
                {
                    double ratio = gridBpm / soundTouchBpm;
                    bool harmonic = IsHarmonicRatio(ratio);

                    if (harmonic || Math.Abs(gridBpm - soundTouchBpm) < 5)
                    {
                        // Manejo inteligente de tresillos (Dembow/Trap)
                        if (Math.Abs(ratio - 1.5) < 0.08 || Math.Abs(ratio - 0.667) < 0.08)
                        {
                            finalBpm = (gridBpm > 140 && soundTouchBpm <= 140) ? soundTouchBpm : gridBpm;
                            LoggerService.Log($"BpmDetector - Ratio de tresillo detectado (1.5x). Prefiriendo el tempo base: {finalBpm:F1}");
                        }
                        else
                        {
                            finalBpm = gridBpm;
                            LoggerService.Log($"BpmDetector - Usando TransientGrid {gridBpm:F1} (coincidencia armónica con SoundTouch {soundTouchBpm})");
                        }
                    }
                    else
                    {
                        finalBpm = gridConfidence > 0.4 ? gridBpm : soundTouchBpm;
                        LoggerService.Log($"BpmDetector - Desacuerdo: Grid={gridBpm:F1}(conf={gridConfidence:F2}), ST={soundTouchBpm} -> usando {finalBpm:F1}");
                    }
                }
                else
                {
                    finalBpm = gridBpm;
                }
            }
            else if (soundTouchBpm > 0)
            {
                finalBpm = soundTouchBpm;
                LoggerService.Log($"BpmDetector - TransientGrid falló, usando SoundTouch: {soundTouchBpm}");
            }
            else
            {
                LoggerService.Log("BpmDetector - Ambos métodos fallaron");
                return 0;
            }

            // === Step 5: Normalize tempo range ===
            finalBpm = NormalizeTempoRange(finalBpm);

            // === Step 6: Snap to integer if within 0.3 BPM ===
            finalBpm = SnapToInteger(finalBpm);
            LoggerService.Log($"BpmDetector - Final BPM: {finalBpm}");

            progress?.Report(100);
            return finalBpm;
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmFromSamples - Exception: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// SoundTouch BPMDetect operating on pre-loaded mono samples.
    /// Feeds chunks from memory — zero file I/O.
    /// </summary>
    private double DetectWithSoundTouchFromSamples(float[] monoSamples, int sampleRate)
    {
        const int ChunkSize = 4096;

        try
        {
            var bpmDetect = new BpmDetect(1, sampleRate);
            int offset = 0;

            while (offset < monoSamples.Length)
            {
                int remaining = monoSamples.Length - offset;
                int count = Math.Min(ChunkSize, remaining);
                bpmDetect.InputSamples(monoSamples.AsSpan(offset, count), count);
                offset += count;
            }

            float bpm = bpmDetect.GetBpm();
            return bpm > 0 ? Math.Round(bpm, 1) : 0;
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.SoundTouchFromSamples - Error: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Selects the optimal segment for analysis:
    /// - Skips the intro by finding where sustained energy begins
    /// - Takes up to 60 seconds (enough for 32 bars at any BPM > 80)
    /// </summary>
    private float[] SelectAnalysisSegment(float[] monoSamples, int sampleRate, double initialBpm)
    {
        double barDuration = 4.0 * (60.0 / initialBpm);
        double targetDuration = Math.Min(60.0, 32 * barDuration);
        int targetSamples = (int)(targetDuration * sampleRate);

        if (monoSamples.Length <= targetSamples)
            return monoSamples;

        int windowSamples = sampleRate / 2;
        int numWindows = monoSamples.Length / windowSamples;
        if (numWindows < 4) return monoSamples;

        var rmsValues = new double[numWindows];
        for (int w = 0; w < numWindows; w++)
        {
            double sum = 0;
            int start = w * windowSamples;
            for (int i = start; i < start + windowSamples && i < monoSamples.Length; i++)
                sum += monoSamples[i] * (double)monoSamples[i];
            rmsValues[w] = Math.Sqrt(sum / windowSamples);
        }

        var sorted = rmsValues.OrderBy(x => x).ToArray();
        double p75 = sorted[(int)(sorted.Length * 0.75)];
        double threshold = p75 * 0.6;

        int startWindow = 0;
        for (int w = 0; w < numWindows - 2; w++)
        {
            if (rmsValues[w] > threshold && rmsValues[w + 1] > threshold && rmsValues[w + 2] > threshold)
            {
                startWindow = w;
                break;
            }
        }

        if (startWindow == 0 && numWindows > 10)
            startWindow = numWindows / 10;

        int startSample = startWindow * windowSamples;

        if (startSample + targetSamples > monoSamples.Length)
            startSample = Math.Max(0, monoSamples.Length - targetSamples);

        LoggerService.Log($"BpmDetector.SelectSegment - Start: {startSample / (double)sampleRate:F1}s, Duration: {targetDuration:F1}s (32 bars @ {initialBpm:F0} BPM)");

        return monoSamples.AsSpan(startSample, Math.Min(targetSamples, monoSamples.Length - startSample)).ToArray();
    }

    private double NormalizeTempoRange(double bpm)
    {
        if (bpm >= 170 && bpm <= 200)
        {
            double half = bpm / 2.0;
            if (half >= 85 && half <= 100)
            {
                LoggerService.Log($"BpmDetector.NormalizeTempo - {bpm} -> {half} (half-time, urban/reggaetón convention)");
                return half;
            }
        }
        return bpm;
    }

    private double SnapToInteger(double bpm)
    {
        double rounded = Math.Round(bpm);
        if (Math.Abs(bpm - rounded) < 0.3)
        {
            LoggerService.Log($"BpmDetector.SnapToInteger - {bpm:F1} -> {rounded}");
            return rounded;
        }
        return Math.Round(bpm, 1);
    }

    private bool IsHarmonicRatio(double ratio)
    {
        double[] harmonics = { 0.5, 0.667, 0.75, 1.0, 1.333, 1.5, 2.0, 3.0 };
        foreach (var h in harmonics)
        {
            if (Math.Abs(ratio - h) < 0.08) return true;
        }
        return false;
    }
}
