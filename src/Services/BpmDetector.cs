using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Services;
using SoundTouch;
using System;
using System.IO;

namespace AudioAnalyzer.Services;

public class BpmDetector : IBpmDetectorService
{
    private readonly WaveformAnalyzer _waveformAnalyzer = new();

    public async Task<(double PrimaryBpm, double AlternativeBpm)> DetectBpmAsync(string filePath, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto)
    {
        try
        {
            var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
            return await Task.Run(() => DetectBpmFromSamples(monoSamples, sampleRate, progress, profile));
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmAsync - Error: {ex.Message}");
            return (0, 0);
        }
    }

    public async Task<(double PrimaryBpm, double AlternativeBpm)> DetectBpmAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto)
    {
        try
        {
            return await Task.Run(() => DetectBpmFromSamples(monoSamples, sampleRate, progress, profile));
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmAsync(samples) - Error: {ex.Message}");
            return (0, 0);
        }
    }

    public (double PrimaryBpm, double AlternativeBpm) DetectBpm(string filePath, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto)
    {
        var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
        return DetectBpmFromSamples(monoSamples, sampleRate, progress);
    }

    /// <summary>
    /// Core BPM detection logic operating on pre-loaded mono samples.
    /// No file I/O occurs in this method.
    /// </summary>
    private (double PrimaryBpm, double AlternativeBpm) DetectBpmFromSamples(float[] monoSamples, int sampleRate, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto)
    {
        try
        {
            LoggerService.Log($"BpmDetector.DetectBpmFromSamples - {monoSamples.Length} samples @ {sampleRate}Hz");
            progress?.Report(5);

            if (monoSamples.Length < sampleRate * 5)
            {
                LoggerService.Log("BpmDetector - Audio too short for analysis");
                return (0, 0);
            }

            progress?.Report(15);

            // === Step 1: SoundTouch quick BPM (USAR AUDIO ORIGINAL) ===
            double soundTouchBpm = DetectWithSoundTouchFromSamples(monoSamples, sampleRate);
            LoggerService.Log($"BpmDetector - SoundTouch quick estimate: {soundTouchBpm}");

            progress?.Report(35);

            // === Step 2: Select analysis segment (USAR AUDIO ORIGINAL) ===
            double initialBpm = soundTouchBpm > 0 ? soundTouchBpm : 120;
            var segment = SelectAnalysisSegment(monoSamples, sampleRate, initialBpm);
            LoggerService.Log($"BpmDetector - Analysis segment: {segment.Length} samples ({segment.Length / (double)sampleRate:F1}s)");

            progress?.Report(45);

            // === Step 3: Transient-based BPM with Beat Grid Fitting ===
            // AQUÍ ES DONDE APLICAMOS EL FILTRO ÚNICAMENTE PARA EL ANÁLISIS DE TRANSIENTES
            LoggerService.Log("BpmDetector - Aplicando filtro High-Pass al segmento para el análisis de transientes...");
            var transientSegment = ApplyTransientEnhancementFilter(segment);
            
            var (gridBpm, gridConfidence) = _waveformAnalyzer.DetectBpmByTransientGrid(transientSegment, sampleRate);
            LoggerService.Log($"BpmDetector - TransientGrid result: {gridBpm:F1} BPM (conf: {gridConfidence:F2})");

            progress?.Report(80);

            // === Step 4: Select final BPM ===
            double finalBpm;
            if (gridBpm > 0 && gridConfidence > BpmConstants.MIN_CONFIDENCE_THRESHOLD)
            {
                if (soundTouchBpm > 0)
                {
                    double ratio = gridBpm / soundTouchBpm;
                    bool harmonic = IsHarmonicRatio(ratio);

                    if (harmonic || Math.Abs(gridBpm - soundTouchBpm) < 5)
                    {
                        // Manejo inteligente de tresillos (Dembow/Trap)
                        if (Math.Abs(ratio - BpmConstants.TRESILLO_RATIO) < BpmConstants.TRESILLO_TOLERANCE || Math.Abs(ratio - BpmConstants.HALF_TIME_RATIO) < BpmConstants.TRESILLO_TOLERANCE)
                        {
                            // NUEVA REGLA: Override por altísima confianza del TransientGrid
                            if (gridConfidence >= BpmConstants.HIGH_CONFIDENCE_THRESHOLD)
                            {
                                finalBpm = gridBpm;
                                LoggerService.Log($"BpmDetector - Ratio de tresillo detectado. TransientGrid tiene confianza muy alta ({gridConfidence:F2}). Forzando TransientGrid: {finalBpm:F1}");
                            }
                            else
                            {
                                double maxBpm = Math.Max(gridBpm, soundTouchBpm);
                                double minBpm = Math.Min(gridBpm, soundTouchBpm);

                                // Umbral inteligente
                                if (maxBpm > BpmConstants.SMART_THRESHOLD_BPM)
                                {
                                    finalBpm = minBpm;
                                    LoggerService.Log($"BpmDetector - Ratio de tresillo. Max BPM ({maxBpm:F1}) > 155 (Rango Pop/Reggaeton). Prefiriendo base: {finalBpm:F1}");
                                }
                                else
                                {
                                    finalBpm = maxBpm;
                                    LoggerService.Log($"BpmDetector - Ratio de tresillo. Max BPM ({maxBpm:F1}) <= 155 (Rango Trap/Drill). Prefiriendo tempo alto: {finalBpm:F1}");
                                }
                            }
                        }
                        else
                        {
                            // Para el resto de armónicos (ej. el doble o la mitad), verificamos que la confianza sea decente
                            if (gridConfidence >= 0.40)
                            {
                                finalBpm = gridBpm;
                                LoggerService.Log($"BpmDetector - Usando TransientGrid {gridBpm:F1} (coincidencia armónica con SoundTouch {soundTouchBpm:F1})");
                            }
                            else
                            {
                                // Si la confianza es muy mala, el masterizado engañó al Grid. Nos quedamos con SoundTouch.
                                finalBpm = soundTouchBpm;
                                LoggerService.Log($"BpmDetector - Coincidencia armónica pero confianza de Grid muy baja ({gridConfidence:F2}). Rechazando Grid y usando SoundTouch: {soundTouchBpm:F1}");
                            }
                        }
                    }
                    else
                    {
                        // Desacuerdo total: Solo confiamos en el Grid si está MUY seguro de sí mismo.
                        // De lo contrario, SoundTouch es mucho más estable para audio comprimido (MP3).
                        if (gridConfidence >= BpmConstants.MEDIUM_CONFIDENCE_THRESHOLD)
                        {
                            finalBpm = gridBpm;
                            LoggerService.Log($"BpmDetector - Desacuerdo superado por alta confianza: Grid={gridBpm:F1} (conf={gridConfidence:F2}) vetó a ST={soundTouchBpm:F1}");
                        }
                        else
                        {
                            finalBpm = soundTouchBpm;
                            LoggerService.Log($"BpmDetector - Desacuerdo con confianza baja/media: Grid={gridBpm:F1} (conf={gridConfidence:F2}). Rechazando Grid, usando ST={soundTouchBpm:F1}");
                        }
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
                return (0, 0);
            }

            // === HEURÍSTICA DE TRAP MASTERIZADO (Corrección del agujero 101.4 BPM) ===
            // Si el motor base decidió un tempo entre 98 y 105 (firma típica del tresillo de 150-155 BPM),
            // pero el TransientGrid estuvo detectando velocidades altísimas (> 160) en el fondo,
            // asumimos con seguridad que es un Trap/Drill masterizado y lo forzamos a Half-time.
            if (finalBpm >= BpmConstants.TRAP_MIN_BPM && finalBpm <= BpmConstants.TRAP_MAX_BPM && gridBpm >= BpmConstants.TRAP_GRID_BPM_THRESHOLD)
            {
                double correctedBpm = finalBpm * BpmConstants.TRAP_CORRECTION_MULTIPLIER;
                LoggerService.Log($"BpmDetector - Heurística Trap Masterizado: Falso positivo {finalBpm:F1} corregido a {correctedBpm:F1} BPM (Grid sugería {gridBpm:F1})");
                finalBpm = correctedBpm;
            }

            // === Step 5: Normalize tempo range ===
            finalBpm = NormalizeTempoRange(finalBpm, profile);

            // === Step 6: Snap to integer if within 0.3 BPM ===
            finalBpm = SnapToInteger(finalBpm);
            LoggerService.Log($"BpmDetector - Final BPM: {finalBpm}");

            // === Step 7: Calculate Alternative BPM ===
            double altBpm = CalculateAlternativeBpm(finalBpm);
            LoggerService.Log($"BpmDetector - Resultado Final: {finalBpm} BPM (Alternativo: {altBpm} BPM)");

            progress?.Report(100);
            return (finalBpm, altBpm);
        }
        catch (Exception ex)
        {
            LoggerService.Log($"BpmDetector.DetectBpmFromSamples - Exception: {ex.Message}");
            return (0, 0);
        }
    }

    /// <summary>
    /// Calculate alternative BPM based on DJ conventions (Double-time, Half-time, or Tresillo).
    /// </summary>
    private double CalculateAlternativeBpm(double primaryBpm)
    {
        if (primaryBpm <= 0) return 0;
        
        double altBpm;
        if (primaryBpm < 90) 
        {
            // Tempos bajos: sugerimos el Double-Time (ej. 76 -> 152)
            altBpm = primaryBpm * 2.0; 
        }
        else if (primaryBpm >= 90 && primaryBpm <= 135) 
        {
            // Tempos medios: sugerimos el ratio de tresillo (ej. 101.4 -> 152.1)
            altBpm = primaryBpm * 1.5; 
        }
        else 
        {
            // Tempos altos: sugerimos el Half-Time (ej. 152 -> 76)
            altBpm = primaryBpm / 2.0; 
        }
        
        return SnapToInteger(altBpm);
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

    private double NormalizeTempoRange(double bpm, BpmRangeProfile profile)
    {
        if (bpm <= 0) return 0;

        // Si es Auto, usamos una regla general permisiva (60-170) 
        // pero preferimos dejarlo como lo detectó el motor base
        if (profile == BpmRangeProfile.Auto)
        {
            if (bpm > 175) return bpm / 2.0;
            if (bpm < 55) return bpm * 2.0;
            return bpm;
        }

        double minBpm, maxBpm;
        switch (profile)
        {
            case BpmRangeProfile.Low_50_100: minBpm = 50; maxBpm = 100; break;
            case BpmRangeProfile.Mid_75_150: minBpm = 75; maxBpm = 150; break;
            case BpmRangeProfile.High_100_200: minBpm = 100; maxBpm = 200; break;
            case BpmRangeProfile.VeryHigh_150_300: minBpm = 150; maxBpm = 300; break;
            default: return bpm;
        }

        // Si ya está en el rango, lo retornamos tal cual
        if (bpm >= minBpm && bpm <= maxBpm) return bpm;

        // Si no está en el rango, probamos multiplicadores para encajarlo
        double[] multipliers = { 2.0, 0.5, 1.5, 0.667 };
        
        foreach (var mult in multipliers)
        {
            double adjusted = bpm * mult;
            if (adjusted >= minBpm && adjusted <= maxBpm)
            {
                LoggerService.Log($"BpmDetector.Normalize - Ajustado por perfil FL Studio ({minBpm}-{maxBpm}): {bpm:F1} -> {adjusted:F1} (Multiplicador: x{mult:F3})");
                return adjusted;
            }
        }

        LoggerService.Log($"BpmDetector.Normalize - Advertencia: No se pudo encajar {bpm:F1} en el rango {minBpm}-{maxBpm}. Se devuelve original.");
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

    /// <summary>
    /// Aplica un filtro pasa-altos de primer orden (pre-emphasis) para remover sub-bajos 
    /// y resaltar los transientes agudos, crucial para archivos masterizados y MP3.
    /// </summary>
    private float[] ApplyTransientEnhancementFilter(float[] samples)
    {
        if (samples == null || samples.Length == 0) return samples;

        float[] filtered = new float[samples.Length];
        filtered[0] = samples[0];
        
        // Coeficiente 0.95 elimina eficazmente frecuencias por debajo de ~150Hz
        for (int i = 1; i < samples.Length; i++)
        {
            filtered[i] = samples[i] - 0.95f * samples[i - 1];
        }
        
        return filtered;
    }
}
