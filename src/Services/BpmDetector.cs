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
        return DetectBpmFromSamples(monoSamples, sampleRate, progress, profile);
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
                sfBpm,      sfConfidence,
                allGridCandidates);

            LoggerService.Log($"[Decision] Final antes de post-processing: {finalBpm:F1} BPM " +
                $"(ST:{soundTouchBpm:F1}, Grid:{gridBpm:F1} [{gridConfidence:F2}], " +
                $"SF:{sfBpm:F1} [{sfConfidence:F2}])");

            // ── Rescate mitad-SF: cuando Grid=0, ST=0 y SF reporta armónico alto ──
            // Si SF fue la única fuente y reportó un BPM > 150 con baja confianza,
            // buscar entre sus candidatos uno cercano a SF/2 (el fundamental probable).
            // Caso: audio11 → SF=170/0.20, candidato 83.5 ≈ 170/2=85.
            if (finalBpm > 150 && gridBpm <= 0 && soundTouchBpm <= 0 && sfConfidence < 0.30)
            {
                double halfSf = finalBpm / 2.0;
                var halfCandidate = allSfCandidates
                    .FirstOrDefault(c => Math.Abs(c.bpm - halfSf) < 3.0);

                if (halfCandidate.bpm > 0)
                {
                    LoggerService.Log($"[Rescue] SF={finalBpm:F1} baja conf, Grid=0, ST=0. " +
                        $"Candidato mitad encontrado: {halfCandidate.bpm:F1} BPM (≈{halfSf:F1}/2)");
                    finalBpm = halfCandidate.bpm;
                }
            }

            // ── Fallback: DetectBpmAdvanced cuando todas las fuentes fallan ──
            // Usa 3 métodos independientes: spectral flux, energy flux, complex domain onset.
            // El complex domain es especialmente bueno para baladas (detecta cambios de fase
            // incluso cuando la amplitud es suave). Aplica low-pass 200Hz (opuesto al high-pass
            // del pipeline principal) preservando el bajo que lleva el pulso en baladas.
            // Solo se ejecuta cuando finalBpm <= 0 para no afectar archivos que ya funcionan.
            if (finalBpm <= 0)
            {
                LoggerService.Log("[Fallback] Todas las fuentes fallaron. Intentando DetectBpmAdvanced...");
                var (advBpm, advConf) = _waveformAnalyzer.DetectBpmWithConfidence(segment, sampleRate);
                if (advBpm > 0 && advConf > 0.15)
                {
                    finalBpm = advBpm;
                    LoggerService.Log($"[Fallback] DetectBpmAdvanced rescató: {finalBpm:F1} BPM (conf: {advConf:F2})");
                    
                    // Cross-validation con candidatos SF del pipeline principal:
                    // Si Advanced retorna un BPM < 70 (sub-armónico probable de una balada),
                    // buscar en candidatos SF del pipeline principal alguno en rango 70-100
                    // que pueda ser el tempo fundamental real.
                    // Caso: audio11 → Advanced=64.5, SF candidato 83.5 ≈ 82 BPM real.
                    if (finalBpm < 70 && allSfCandidates != null && allSfCandidates.Count > 0)
                    {
                        var sfCandidateInRange = allSfCandidates
                            .Where(c => c.bpm >= 70 && c.bpm <= 100)
                            .OrderByDescending(c => c.score)
                            .FirstOrDefault();
                        
                        if (sfCandidateInRange.bpm > 0)
                        {
                            LoggerService.Log($"[Fallback] Cross-validation: Advanced={finalBpm:F1} < 70, " +
                                $"SF candidato en rango 70-100: {sfCandidateInRange.bpm:F1} (score={sfCandidateInRange.score:F3}). " +
                                $"Prefiriendo SF candidato.");
                            finalBpm = sfCandidateInRange.bpm;
                        }
                    }
                }
                else
                {
                    LoggerService.Log($"[Fallback] DetectBpmAdvanced no pudo detectar (bpm={advBpm:F1}, conf={advConf:F2})");
                }
            }

            // === HEURISTICA DE TRAP MASTERIZADO (Correccion del agujero 101.4 BPM) ===
            // Si el motor base decidió un tempo entre 98 y 105 (firma tipica del tresillo de 150-155 BPM),
            // pero el TransientGrid estuvo detectando velocidades altisimas (> 160) en el fondo,
            // asumimos con seguridad que es un Trap/Drill masterizado y lo forzamos a Half-time.
            bool trapCorrectionApplied = false;
            if (finalBpm >= BpmConstants.TRAP_MIN_BPM &&
                finalBpm <= BpmConstants.TRAP_MAX_BPM)
            {
                // Solo rescatamos si el Grid confirma que hay energía en el rango superior (> 140)
                // O si el Grid coincide directamente con el objetivo (finalBpm * 1.5)
                // Pasar AMBAS listas de candidatos al guard para más contexto
                var combinedCandidates = allGridCandidates
                    .Concat(allSfCandidates)
                    .ToList();

                if (sfConfidence < BpmConstants.HIGH_CONFIDENCE_THRESHOLD && ShouldApplyTrapHeuristic(finalBpm, combinedCandidates))
                {
                    double pre = finalBpm;
                    finalBpm   = finalBpm * BpmConstants.TRAP_CORRECTION_MULTIPLIER;
                    trapCorrectionApplied = true;
                    LoggerService.Log($"[Trap] {pre:F1} → {finalBpm:F1} BPM");
                }
                else
                {
                    LoggerService.Log($"[Trap] Cancelada — evidencia de half-time en candidatos.");
                }
            }

            // === Step 5: Post-processing heuristics ===
            // Resolucion de ambigüedad 2:1
            // Solo aplicamos esto en modo AUTO, porque en otros perfiles el usuario ya decidió el rango.
            if (!trapCorrectionApplied && profile == BpmRangeProfile.Auto)
                finalBpm = ResolveDoubleTimeAmbiguity(finalBpm, soundTouchBpm, gridBpm);

            // === Step 6: Normalize tempo range — FINAL GATE ===
            // Este es el filtro final que garantiza el cumplimiento del perfil elegido.
            finalBpm = SelectBestCandidateForProfile(
                finalBpm, 
                soundTouchBpm, 
                allGridCandidates, 
                allSfCandidates ?? new List<(double bpm, double score)>(), 
                profile);

            // === Step 7: Snap to integer if within 0.3 BPM ===
            finalBpm = SnapToInteger(finalBpm);
            LoggerService.Log($"BpmDetector - Final BPM: {finalBpm}");

            // === Step 8: Calculate Alternative BPM ===
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

    private (double min, double max) GetProfileRange(BpmRangeProfile profile)
    {
        return profile switch
        {
            BpmRangeProfile.Low_50_100 => (50, 100),
            BpmRangeProfile.Mid_75_150 => (75, 150),
            BpmRangeProfile.High_100_200 => (100, 200),
            BpmRangeProfile.VeryHigh_150_300 => (150, 300),
            _ => (0, 0)
        };
    }

    private double NormalizeTempoRangeAuto(double bpm)
    {
        if (bpm <= 0) return 0;
        if (bpm > 175) return bpm / 2.0;
        if (bpm < 55) return bpm * 2.0;
        return bpm;
    }

    /// <summary>
    /// Selecciona el mejor BPM del pool de candidatos reales que encaje en el perfil.
    /// NO inventa valores nuevos arbitrarios — prioriza candidatos detectados por los motores.
    /// </summary>
    private double SelectBestCandidateForProfile(
        double currentBpm,
        double soundTouchBpm,
        List<(double bpm, double score)> allGridCandidates,
        List<(double bpm, double score)> allSfCandidates,
        BpmRangeProfile profile)
    {
        if (currentBpm <= 0) return 0;

        // Caso Auto: Usar la regla de normalización permisiva estándar
        if (profile == BpmRangeProfile.Auto)
        {
            return NormalizeTempoRangeAuto(currentBpm);
        }

        var (minBpm, maxBpm) = GetProfileRange(profile);

        // 1. Construir pool de todos los candidatos reales detectados
        var pool = new List<(double bpm, double score, string source)>();
        
        // El ganador actual es el candidato más fuerte inicialmente
        pool.Add((currentBpm, 1.0, "Winner"));

        if (soundTouchBpm > 0)
            pool.Add((soundTouchBpm, 0.5, "ST"));
        
        if (allGridCandidates != null)
            foreach (var c in allGridCandidates)
                pool.Add((c.bpm, c.score, "Grid"));
        
        if (allSfCandidates != null)
            foreach (var c in allSfCandidates)
                pool.Add((c.bpm, c.score, "SF"));

        // 2. Filtrar candidatos que están dentro del rango del perfil
        var inRangeCandidates = pool
            .Where(c => c.bpm >= minBpm && c.bpm <= maxBpm)
            .OrderByDescending(c => c.score)
            .ToList();

        if (inRangeCandidates.Count > 0)
        {
            var best = inRangeCandidates[0];
            LoggerService.Log($"BpmDetector.RangeFilter [{profile}] - Pool: {pool.Count} candidatos, {inRangeCandidates.Count} en rango [{minBpm}-{maxBpm}]. " +
                $"Seleccionado: {best.bpm:F1} BPM ({best.source}, score={best.score:F3})");
            return best.bpm;
        }

        // 3. Fallback: Si no hay NINGÚN candidato real en el rango deseado, 
        // intentamos el ajuste matemático tradicional (pero limitado a multiplicadores armónicos comunes)
        LoggerService.Log($"BpmDetector.RangeFilter [{profile}] - Advertencia: Sin candidatos reales en [{minBpm}-{maxBpm}]. Fallback desde {currentBpm:F1}");
        
        double[] commonMultipliers = { 2.0, 0.5, 1.5, 0.667, 3.0 };
        foreach (var mult in commonMultipliers)
        {
            double adjusted = currentBpm * mult;
            if (adjusted >= minBpm && adjusted <= maxBpm)
            {
                LoggerService.Log($"BpmDetector.RangeFilter [{profile}] - Fallback exitoso: {currentBpm:F1} -> {adjusted:F1} (Multiplicador x{mult:F3})");
                return adjusted;
            }
        }

        // Si nada funcionó, devolver original pero loguear que falló el rango
        LoggerService.Log($"BpmDetector.RangeFilter [{profile}] - CRÍTICO: No se encontró forma de llevar {currentBpm:F1} al rango [{minBpm}-{maxBpm}].");
        return currentBpm;
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
        double sfBpm,    double sfConf,
        List<(double bpm, double score)> allGridCandidates = null)
    {
        const double AGREEMENT_TOL = 5.0; // BPM — margen para considerar acuerdo

        // Guardia de Grid ruido: si los top-5 candidatos de Grid están TODOS en rango 185-200
        // Y SF es débil (< 0.25), Grid probablemente es ruido saturado. Descartar.
        // Casos: audio6 (SF=0.04), audio11 (SF=0.20), audio5 (SF=0.56 - no aplica)
        if (gridBpm > 0 && allGridCandidates != null && allGridCandidates.Count >= 5 && sfConf < 0.25)
        {
            bool allAtCeiling = allGridCandidates.Take(5).All(c => c.bpm >= 185 && c.bpm <= 200);
            if (allAtCeiling)
            {
                LoggerService.Log($"[Vote] GRID NOISE GUARD: Candidatos {allGridCandidates.Take(5).Min(c => c.bpm):F0}-" +
                    $"{allGridCandidates.Take(5).Max(c => c.bpm):F0} en rango techo, SF conf {sfConf:F2} < 0.25. " +
                    $"Descartando Grid.");
                gridBpm = 0;
                gridConf = 0;
            }
        }

        bool gridVsSf = gridBpm > 0 && sfBpm > 0 && Math.Abs(gridBpm - sfBpm) < AGREEMENT_TOL;
        bool stVsSf   = stBpm > 0   && sfBpm > 0 && Math.Abs(stBpm - sfBpm) < AGREEMENT_TOL;
        bool stVsGrid = stBpm > 0   && gridBpm > 0 && Math.Abs(stBpm - gridBpm) < AGREEMENT_TOL;

        // ── Caso 1: Consenso directo entre dos fuentes ───────────────────────
        if (gridVsSf)
        {
            double winner = gridConf >= sfConf ? gridBpm : sfBpm;
            
            // Validar contra SoundTouch: si ST reportó un valor diferente y válido,
            // verificar si el consenso Grid+SF es un armónico de ST.
            // En 5 de 7 FAILs del baseline, Grid+SF consensuaban en un armónico
            // incorrecto mientras ST tenía el BPM correcto.
            if (stBpm > 0 && Math.Abs(stBpm - winner) > AGREEMENT_TOL)
            {
                double ratio = winner / stBpm;
                bool isHarmonic = Math.Abs(ratio - 0.5) < 0.06 || Math.Abs(ratio - 2.0) < 0.06 ||
                                  Math.Abs(ratio - 1.5) < 0.06 || Math.Abs(ratio - 0.667) < 0.06 ||
                                  Math.Abs(ratio - 1.333) < 0.06 || Math.Abs(ratio - 0.75) < 0.06;
                
                if (isHarmonic)
                {
                    LoggerService.Log($"[Vote] CONSENSO Grid+SF={winner:F1} es armónico de ST={stBpm:F1} (ratio={ratio:F3}). Prefiriendo ST.");
                    return stBpm;
                }
                
                // Si no es armónico pero Grid+SF tienen confianza no abrumadora, preferir ST
                // Umbral 0.8: protege contra sub-armónicos falsos (ej: audio12: Grid+SF=65, ST=98)
                double maxConf = Math.Max(gridConf, sfConf);
                if (maxConf < 0.8)
                {
                    LoggerService.Log($"[Vote] CONSENSO Grid+SF={winner:F1} confianza insuficiente ({maxConf:F2} < 0.8), ST={stBpm:F1} diferente. Prefiriendo ST.");
                    return stBpm;
                }
            }
            
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
                // a menos que SF tenga una confianza absoluta muy alta (> 0.75).
                if (sfConf < BpmConstants.HIGH_CONFIDENCE_THRESHOLD)
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

        // ── Caso 4: Detección de half-time falso (Grid/SF reportan ~0.5x de ST) ──
        // Ej: ST=110, Grid=55, SF=55 → Significa que Grid/SF están detectando 
        //     un pulso más lento que el real. Usar ST como base.
        if (stBpm > 0 && gridBpm > 0 && Math.Abs(gridBpm - stBpm * 0.5) < 3.0)
        {
            LoggerService.Log($"[Vote] HALF-TIME GUARD: Grid detectó {gridBpm:F1} (~0.5x ST). Usando ST base → {stBpm:F1} BPM");
            return stBpm;
        }
        if (stBpm > 0 && sfBpm > 0 && Math.Abs(sfBpm - stBpm * 0.5) < 3.0 && sfConf < 0.7)
        {
            LoggerService.Log($"[Vote] HALF-TIME GUARD: SF detectó {sfBpm:F1} (~0.5x ST, conf={sfConf:F2}). Usando ST base → {stBpm:F1} BPM");
            return stBpm;
        }

        // ── Caso 4.5: Guardia ST/2 — SoundTouch detectó double-time ─────────
        // Caso típico: ST=153.4 (double del real 76.7), SF=57.5 (sub-armónico).
        // Ninguna fuente reporta el fundamental, pero ST/2 lo es.
        if (stBpm > 140 && sfBpm > 0 && sfBpm < 90)
        {
            double stHalf = stBpm / 2.0;
            if (Math.Abs(sfBpm - stHalf) > AGREEMENT_TOL && stHalf >= 60 && stHalf <= 120)
            {
                double sfToStHalf = sfBpm / stHalf;
                if (sfToStHalf < 0.85) // SF es sub-armónico de ST/2
                {
                    LoggerService.Log($"[Vote] ST/2 GUARD: SF={sfBpm:F1} es sub-armónico de ST/2={stHalf:F1} " +
                        $"(ratio={sfToStHalf:F3}). Prefiriendo ST/2.");
                    return stHalf;
                }
            }
        }

        // ── Caso 5: Sin consenso — prioridad por confianza ───────────────────
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
            return false;

        foreach (var candidate in allGridCandidates)
        {
            double ratio = candidate.bpm / soundTouchBpm;
            
            // Umbral dinámico: si el BPM base ya es alto (> 100), exigimos más confianza para el 1.5x
            // para evitar falsos positivos en temas que ya están en su tempo real (ej. 108 BPM).
            double minScore = (soundTouchBpm > 100) ? -1.0 : -1.8;

            if ((Math.Abs(ratio - 1.5) < 0.05 || Math.Abs(ratio - 3.0) < 0.05) && candidate.score > minScore)
            {
                LoggerService.Log($"[Trap Guard] Candidato {candidate.bpm:F1} confirma relación {ratio:F2}x con score {candidate.score:F3} (min={minScore}). Rescatando.");
                return true; 
            }
        }

        return false;
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
