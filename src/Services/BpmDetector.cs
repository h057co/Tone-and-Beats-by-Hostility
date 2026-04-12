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

            // ── Step 3: TransientGrid (sin cambios) ─────────────────────────
            LoggerService.Log("BpmDetector - Aplicando filtro High-Pass al segmento para el analisis de transientes...");
            var transientSegment = ApplyTransientEnhancementFilter(segment);
            
            var (gridBpm, gridConfidence, allGridCandidates) = _waveformAnalyzer.DetectBpmByTransientGrid(transientSegment, sampleRate);
            LoggerService.Log($"[TransientGrid] {gridBpm:F1} BPM (conf: {gridConfidence:F2})");

            // ── Step 3.5: SpectralFlux — NUEVO v1.0.7 ───────────────────────
            // Sin nuevas dependencias: usa NAudio.Dsp que ya está en el proyecto
            // Reutiliza MergeTransients + AutocorrelateTransients + ScoreBeatGrid
            var (sfBpm, sfConfidence, allSfCandidates) = 
                _waveformAnalyzer.DetectBpmBySpectralFlux(segment, sampleRate);
            LoggerService.Log($"[SpectralFlux] {sfBpm:F1} BPM (conf: {sfConfidence:F2})");

            // ── Step 3.6: Half-Time Hypothesis ──────────────────────────────
            // Solo activa si AMBOS detectores tienen baja confianza y ST es half-time
            var cachedTransients = _waveformAnalyzer.GetLastTransients();
            double segDuration   = transientSegment.Length / (double)sampleRate;

            bool gridWeak = gridBpm <= 0 || gridConfidence < 0.5;
            bool sfWeak   = sfBpm   <= 0 || sfConfidence   < 0.5;
            bool stIsHalfTimeZone = soundTouchBpm >= 85 && soundTouchBpm <= 115;

            if (gridWeak && sfWeak && stIsHalfTimeZone)
            {
                var (htBpm, htConf) = EvaluateSoundTouchHalfTimeHypothesis(
                    soundTouchBpm, allGridCandidates, cachedTransients, segDuration);

                if (htBpm > 0)
                {
                    LoggerService.Log($"[HalfTime] Override: {gridBpm:F1} → {htBpm:F1} BPM " +
                        $"(conf: {htConf:F2})");
                    gridBpm        = htBpm;
                    gridConfidence = htConf;
                }
            }

            progress?.Report(80);

            // ── Step 4: Voto de tres fuentes → BPM final ────────────────────
            double finalBpm = VoteThreeSources(
                soundTouchBpm,
                gridBpm,    gridConfidence,
                sfBpm,      sfConfidence);

            LoggerService.Log($"[Decision] Final antes de post-processing: {finalBpm:F1} BPM " +
                $"(ST:{soundTouchBpm:F1}, Grid:{gridBpm:F1} [{gridConfidence:F2}], " +
                $"SF:{sfBpm:F1} [{sfConfidence:F2}])");

            // === HEURISTICA DE TRAP MASTERIZADO (Correccion del agujero 101.4 BPM) ===
            // Si el motor base decidió un tempo entre 98 y 105 (firma tipica del tresillo de 150-155 BPM),
            // pero el TransientGrid estuvo detectando velocidades altisimas (> 160) en el fondo,
            // asumimos con seguridad que es un Trap/Drill masterizado y lo forzamos a Half-time.
            if (finalBpm >= BpmConstants.TRAP_MIN_BPM &&
                finalBpm <= BpmConstants.TRAP_MAX_BPM &&
                gridBpm   > BpmConstants.TRAP_GRID_BPM_THRESHOLD)
            {
                // Pasar AMBAS listas de candidatos al guard para más contexto
                var combinedCandidates = allGridCandidates
                    .Concat(allSfCandidates)
                    .ToList();

                if (ShouldApplyTrapHeuristic(finalBpm, combinedCandidates))
                {
                    double pre = finalBpm;
                    finalBpm   = finalBpm * BpmConstants.TRAP_CORRECTION_MULTIPLIER;
                    LoggerService.Log($"[Trap] {pre:F1} → {finalBpm:F1} BPM");
                }
                else
                {
                    LoggerService.Log($"[Trap] Cancelada — evidencia de half-time en candidatos.");
                }
            }

            // === Step 5: Normalize tempo range ===
            finalBpm = NormalizeTempoRange(finalBpm, profile);

            // Resolucion de ambigüedad 2:1
            finalBpm = ResolveDoubleTimeAmbiguity(finalBpm, soundTouchBpm, gridBpm);

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

        // NUEVO: Siempre buscar el mejor candidato en el rango, incluso si el input ya está en rango.
        // Esto maneja el caso donde 142.5 está en rango pero 95 también está y es "más limpio" (no es tresillo)
        double[] candidates = new double[]
        {
            bpm, bpm * 0.5, bpm * 2.0,
            bpm / 1.5, bpm * 1.5,
            bpm / 3.0, bpm * 3.0,
            bpm / 4.0, bpm * 4.0
        };
        
        double bestCandidate = bpm;
        bool foundBetter = false;
        
        foreach (var candidate in candidates)
        {
            // Solo considerar si está en el rango Y es más bajo que el actual (preferir tempo base sobre tresillo)
            if (candidate >= minBpm && candidate <= maxBpm && candidate < bestCandidate)
            {
                // Verificar que no sea un tresillo obvio de un valor ya en rango
                // Si el candidato es < actual Y el actual está muy cerca de ser candidato*1.5, probablemente es un tresillo
                double ratio = bpm / candidate;
                if (Math.Abs(ratio - 1.5) < 0.1 || Math.Abs(ratio - 3.0) < 0.1 || Math.Abs(ratio - 0.667) < 0.05)
                {
                    LoggerService.Log($"BpmDetector.Normalize - Descartado candidato {candidate:F1} (ratio {ratio:F3} indica tresillo/armónico del original)");
                    continue;
                }
                
                bestCandidate = candidate;
                foundBetter = true;
            }
        }
        
        if (foundBetter)
        {
            LoggerService.Log($"BpmDetector.Normalize - Mejor candidato encontrado: {bpm:F1} -> {bestCandidate:F1} (perfil {profile})");
            return bestCandidate;
        }

        // Si ya está en el rango, lo retornamos tal cual
        if (bpm >= minBpm && bpm <= maxBpm) return bpm;

        // Si ningúnmultiplicador directo funciona, usar el ajuste tradicional con más multipliers
        double[] multipliers = { 2.0, 0.5, 1.5, 0.667, 3.0, 0.333, 1.25, 0.8 };
        
        foreach (var mult in multipliers)
        {
            double adjusted = bpm * mult;
            if (adjusted >= minBpm && adjusted <= maxBpm)
            {
                LoggerService.Log($"BpmDetector.Normalize - Ajustado por perfil ({minBpm}-{maxBpm}): {bpm:F1} -> {adjusted:F1} (Multiplicador: x{mult:F3})");
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

    /// <summary>
    /// Cuando SoundTouch cae en zona de half-time (85-115 BPM), evalúa si
    /// soundTouch * 1.5 tiene soporte en los candidatos del grid.
    /// Resuelve el fallo de 'master bpm 152.mp3' donde la compresión destruye
    /// la confianza del grid para 152 BPM pero SoundTouch detectó 101.4 (su mitad).
    /// </summary>
    private (double bpm, double confidence) EvaluateSoundTouchHalfTimeHypothesis(
        double soundTouchBpm,
        List<(double bpm, double score)> allGridCandidates,
        List<(double position, double amplitude)> allTransients,
        double segmentDuration)
    {
        if (soundTouchBpm < 85 || soundTouchBpm > 115)
            return (0, 0);

        double halfTimeCandidate = soundTouchBpm * BpmConstants.TRESILLO_RATIO;

        bool candidateFound = allGridCandidates.Any(
            c => Math.Abs(c.bpm - halfTimeCandidate) < 4.0);

        if (!candidateFound)
        {
            LoggerService.Log($"[HalfTime Hyp] {halfTimeCandidate:F1} BPM no encontrado en candidatos grid. Hipotesis descartada.");
            return (0, 0);
        }

        var (stdDev, hitRate, _) = _waveformAnalyzer.ScoreBeatGrid(
            allTransients, halfTimeCandidate, segmentDuration);

        double composite = (hitRate * 1.3) - (stdDev * 1.5);

        LoggerService.Log($"[HalfTime Hyp] BPM {halfTimeCandidate:F1}: stdDev={stdDev:F4}, hitRate={hitRate:F2}, composite={composite:F3}");

        if (composite > -0.05 && hitRate > 0.04)
        {
            double conf = Math.Min(1.0, hitRate * 1.5);
            LoggerService.Log($"[HalfTime Hyp] Hipotesis CONFIRMADA: {halfTimeCandidate:F1} BPM (conf: {conf:F2})");
            return (halfTimeCandidate, conf);
        }

        LoggerService.Log($"[HalfTime Hyp] Hipotesis descartada: composite={composite:F3} insuficiente.");
        return (0, 0);
    }

    /// <summary>
    /// Sistema de voto entre tres detectores independientes:
    ///   - SoundTouch:     autocorrelación temporal (rápido, estable)
    ///   - TransientGrid:  peaks de amplitud + grid fitting
    ///   - SpectralFlux:   cambios espectrales + grid fitting (robusto a mastering)
    ///
    /// Lógica de decisión:
    ///   1. Si dos fuentes coinciden dentro de ±5 BPM → consenso, usar ese valor
    ///   2. Si ningún par coincide → gana el de mayor confianza
    ///      con prioridad SpectralFlux > TransientGrid > SoundTouch
    ///      (SpectralFlux es más robusto en audio producido/masterizado)
    /// </summary>
    private double VoteThreeSources(
        double stBpm,
        double gridBpm,  double gridConf,
        double sfBpm,    double sfConf)
    {
        const double AGREEMENT_TOL = 5.0; // BPM — margen para considerar acuerdo

        bool gridVsSf = gridBpm > 0 && sfBpm > 0 && Math.Abs(gridBpm - sfBpm) < AGREEMENT_TOL;
        bool stVsSf   = stBpm > 0   && sfBpm > 0 && Math.Abs(stBpm - sfBpm) < AGREEMENT_TOL;
        bool stVsGrid = stBpm > 0   && gridBpm > 0 && Math.Abs(stBpm - gridBpm) < AGREEMENT_TOL;

        // ── Caso 1: Consenso directo entre dos fuentes ───────────────────────
        if (gridVsSf)
        {
            double winner = gridConf >= sfConf ? gridBpm : sfBpm;
            LoggerService.Log($"[Vote] CONSENSO Grid+SF → {winner:F1} BPM (grid conf:{gridConf:F2}, sf conf:{sfConf:F2})");
            return winner;
        }
        if (stVsSf)
        {
            LoggerService.Log($"[Vote] CONSENSO ST+SF → {sfBpm:F1} BPM (sf conf:{sfConf:F2})");
            return sfBpm;
        }
        if (stVsGrid)
        {
            LoggerService.Log($"[Vote] CONSENSO ST+Grid → {gridBpm:F1} BPM (grid conf:{gridConf:F2})");
            return gridBpm;
        }

        // ── Caso 2: GUARDIA DE PULSO (SoundTouch vs SpectralFlux) ────────────
        // Previene que SF gane con un tresillo falso en FLAC/M4A limpios.
        if (stBpm > 0 && sfBpm > 0)
        {
            double ratio = sfBpm / stBpm;
            if (Math.Abs(ratio - 1.5) < 0.08 || Math.Abs(ratio - 0.667) < 0.08)
            {
                // Si hay relación de tresillo, confiamos en SoundTouch como la base del pulso,
                // a menos que SF tenga una confianza absolutamente abrumadora (> 0.85).
                if (sfConf < 0.85)
                {
                    LoggerService.Log($"[Vote] GUARDIA DE PULSO: Relación {ratio:F2} detectada. Gana SoundTouch base → {stBpm:F1} BPM");
                    return stBpm;
                }
            }
        }

        // ── Caso 3: Verificar acuerdo entre harmónicos (SF vs Grid) ──────────
        if (sfBpm > 0 && gridBpm > 0)
        {
            double ratio = sfBpm / gridBpm;
            if (Math.Abs(ratio - 1.5)  < 0.08 || Math.Abs(ratio - 0.667) < 0.08 ||
                Math.Abs(ratio - 2.0)  < 0.10 || Math.Abs(ratio - 0.5)   < 0.10)
            {
                double winner = sfConf >= gridConf ? sfBpm : gridBpm;
                LoggerService.Log($"[Vote] HARMÓNICO SF/Grid ratio={ratio:F3} → {winner:F1} BPM");
                return winner;
            }
        }

        // ── Caso 4: Sin consenso — prioridad por confianza ───────────────────
        if (sfBpm > 0 && sfConf > 0.25)
        {
            LoggerService.Log($"[Vote] SF gana por confianza ({sfConf:F2}) → {sfBpm:F1} BPM");
            return sfBpm;
        }
        if (gridBpm > 0 && gridConf > BpmConstants.MIN_CONFIDENCE_THRESHOLD)
        {
            LoggerService.Log($"[Vote] Grid gana por confianza ({gridConf:F2}) → {gridBpm:F1} BPM");
            return gridBpm;
        }

        // ── Fallback: SoundTouch ──────────────────────────────────────────────
        LoggerService.Log($"[Vote] Fallback SoundTouch → {stBpm:F1} BPM");
        return stBpm;
    }

    /// <summary>
    /// Determina si la heuristica Trap debe aplicarse.
    /// Se cancela si algun candidato del grid sugiere que soundTouch es half-time de algo real.
    /// </summary>
    private bool ShouldApplyTrapHeuristic(
        double soundTouchBpm,
        List<(double bpm, double score)> allGridCandidates)
    {
        if (allGridCandidates == null || allGridCandidates.Count == 0)
            return true;

        double halfTimeTarget = soundTouchBpm * BpmConstants.TRESILLO_RATIO;
        foreach (var candidate in allGridCandidates)
        {
            if (Math.Abs(candidate.bpm - halfTimeTarget) < 5.0)
            {
                LoggerService.Log($"[Trap Guard] Candidato {candidate.bpm:F1} sugiere que {soundTouchBpm:F1} es half-time. Trap cancelada.");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resuelve ambigüedad 2:1 cuando el algoritmo prefirió el doble-tiempo.
    /// Aplica cuando: BPM > 145 Y su mitad está en zona de tempo natural (60-105).
    /// Conservador: solo actúa si SoundTouch no apoya activamente el valor alto.
    /// </summary>
    private double ResolveDoubleTimeAmbiguity(
        double detectedBpm,
        double soundTouchBpm,
        double gridBpm)
    {
        double halfTime = detectedBpm / 2.0;

        if (detectedBpm <= 145 || halfTime < 60 || halfTime > 105)
            return detectedBpm;

        bool stSupportsFullTime = soundTouchBpm > 0 &&
                                  Math.Abs(soundTouchBpm - detectedBpm) < 8;
        bool stSupportsHalfTime = soundTouchBpm > 0 &&
                                  Math.Abs(soundTouchBpm - halfTime) < 8;

        if (stSupportsFullTime && !stSupportsHalfTime)
            return detectedBpm;

        if (stSupportsHalfTime || halfTime >= 70)
        {
            LoggerService.Log($"[DoubleTime] {detectedBpm:F1} → {halfTime:F1} BPM " +
                $"(stSupportsHalf:{stSupportsHalfTime})");
            return halfTime;
        }

        return detectedBpm;
    }
}
