using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;

namespace AudioAnalyzer.Services;

public class WaveformAnalyzer : IWaveformAnalyzerService
{
    private const int WindowSize = 2048;
    private const int HopSize = 512;
    private const double TempoChangeThreshold = 2.0;
    private const int BarsForSection = 8;
    private const int TopKCandidates = 5;

    public async Task<WaveformData> AnalyzeAsync(string filePath, double? globalBpm = null)
    {
        return await Task.Run(() => Analyze(filePath, globalBpm));
    }

    public async Task<WaveformData> AnalyzeAsync(float[] monoSamples, int sampleRate, double? globalBpm = null, IProgress<int>? progress = null)
    {
        return await Task.Run(() => AnalyzeFromSamples(monoSamples, sampleRate, globalBpm));
    }

    public WaveformData Analyze(string filePath, double? globalBpm = null)
    {
        LoggerService.Log($"WaveformAnalyzer.Analyze - Iniciando para: {filePath}");
        var (samples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
        return AnalyzeFromSamples(samples, sampleRate, globalBpm);
    }

    /// <summary>
    /// Core waveform analysis operating on pre-loaded mono samples.
    /// No file I/O occurs in this method.
    /// </summary>
    private WaveformData AnalyzeFromSamples(float[] samples, int sampleRate, double? globalBpm = null)
    {
        LoggerService.Log($"WaveformAnalyzer.AnalyzeFromSamples - {samples.Length} samples @ {sampleRate}Hz");

        var waveformData = GetWaveformData(samples, 1000);
        LoggerService.Log($"WaveformAnalyzer.AnalyzeFromSamples - Waveform data completado");

        var beatPositions = new List<double>();
        LoggerService.Log($"WaveformAnalyzer.AnalyzeFromSamples - Beat positions simplificado");

        var energyData = GetEnergySections(samples, sampleRate);
        LoggerService.Log($"WaveformAnalyzer.AnalyzeFromSamples - Energy sections completado");

        var tempoChanges = new List<TempoChange>();
        LoggerService.Log($"WaveformAnalyzer.AnalyzeFromSamples - Tempo changes simplificado");

        return new WaveformData
        {
            WaveformPoints = waveformData,
            BeatPositions = beatPositions,
            EnergySections = energyData,
            TempoChanges = tempoChanges,
            Duration = samples.Length / (double)sampleRate,
            SampleRate = sampleRate
        };
    }

    public List<double[]> GetWaveformData(float[] samples, int numPoints)
    {
        // Pre-allocate exact capacity to avoid resizing
        var result = new List<double[]>(numPoints);
        var samplesPerPoint = samples.Length / numPoints;
        if (samplesPerPoint < 1) samplesPerPoint = 1;

        for (int i = 0; i < numPoints; i++)
        {
            var start = i * samplesPerPoint;
            var end = Math.Min(start + samplesPerPoint, samples.Length);
            
            if (start >= samples.Length) break;

            float min = float.MaxValue;
            float max = float.MinValue;

            for (int j = start; j < end; j++)
            {
                if (samples[j] < min) min = samples[j];
                if (samples[j] > max) max = samples[j];
            }

            result.Add(new double[] { min, max });
        }

        return result;
    }

    public List<double> GetBeatGrid(float[] samples, int sampleRate, double bpm)
    {
        var secondsPerBeat = 60.0 / bpm;
        var samplesPerBeat = secondsPerBeat * sampleRate;
        var estimatedBeats = (int)(samples.Length / samplesPerBeat) + 1;
        
        // Pre-allocate capacity to avoid resizing
        var beatPositions = new List<double>(Math.Max(estimatedBeats, 16));

        for (double pos = 0; pos < samples.Length; pos += samplesPerBeat)
        {
            beatPositions.Add(pos / sampleRate);
        }

        return beatPositions;
    }

    public List<EnergySection> GetEnergySections(float[] samples, int sampleRate, int windowSeconds = 10)
    {
        var windowSamples = windowSeconds * sampleRate;
        var numWindows = samples.Length / windowSamples;
        
        // Pre-allocate capacity to avoid resizing
        var sections = new List<EnergySection>(numWindows + 1);

        double previousEnergy = 0;

        for (int i = 0; i < numWindows; i++)
        {
            var start = i * windowSamples;
            var end = Math.Min(start + windowSamples, samples.Length);
            
            double energy = 0;
            for (int j = start; j < end; j++)
            {
                energy += samples[j] * samples[j];
            }
            energy = Math.Sqrt(energy / (end - start));

            var startTime = start / (double)sampleRate;
            var endTime = end / (double)sampleRate;

            var sectionEnergy = energy > 0.5 ? "high" : (energy > 0.2 ? "medium" : "low");
            double energyChange = 0;
            if (previousEnergy > 0)
            {
                energyChange = Math.Abs(energy - previousEnergy) / previousEnergy * 100;
            }

            sections.Add(new EnergySection
            {
                StartTime = startTime,
                EndTime = endTime,
                Energy = energy,
                EnergyLevel = sectionEnergy,
                EnergyChangePercent = energyChange
            });

            previousEnergy = energy;
        }

        return sections;
    }

    public List<TempoChange> DetectTempoChanges(float[] samples, int sampleRate, double? globalBpm, int? barsPerWindow = null)
    {
        var tempoChanges = new List<TempoChange>();
        
        var (bpm, _) = DetectBpmAdvanced(samples, sampleRate);
        
        if (bpm <= 0) return tempoChanges;

        var beatsPerBar = 4;
        var barsCount = (samples.Length / sampleRate) * bpm / 60 / beatsPerBar;
        
        var numSections = (int)Math.Ceiling(barsCount / BarsForSection);
        if (numSections < 2) return tempoChanges;

        var samplesPerSection = samples.Length / numSections;
        var currentBpm = bpm;

        for (int i = 1; i < numSections; i++)
        {
            var sectionStart = i * samplesPerSection;
            var sectionSamples = samples.Skip(sectionStart).Take(samplesPerSection).ToArray();
            
            var (localBpm, _) = DetectBpmAdvanced(sectionSamples, sampleRate);
            
            if (localBpm > 0 && Math.Abs(localBpm - currentBpm) > TempoChangeThreshold)
            {
                var changeTime = sectionStart / (double)sampleRate;
                
                tempoChanges.Add(new TempoChange
                {
                    Time = changeTime,
                    PreviousBpm = currentBpm,
                    NewBpm = localBpm,
                    ChangeAmount = Math.Abs(localBpm - currentBpm)
                });

                currentBpm = localBpm;
            }
        }

        return tempoChanges;
    }

    public (double bpm, double confidence) DetectBpmWithConfidence(float[] samples, int sampleRate)
    {
        return DetectBpmAdvanced(samples, sampleRate);
    }

    private (double bpm, double confidence) DetectBpmAdvanced(float[] samples, int sampleRate)
    {
        if (samples.Length < sampleRate * 10) return (0, 0);

        // Downsample to ~11025 Hz for BPM detection (rhythm info is in low frequencies)
        // This reduces data 4x for 44.1kHz and ~4.4x for 48kHz audio
        const int TargetSampleRate = 11025;
        if (sampleRate > TargetSampleRate)
        {
            int factor = sampleRate / TargetSampleRate;
            var downsampled = new float[samples.Length / factor];
            for (int i = 0; i < downsampled.Length; i++)
                downsampled[i] = samples[i * factor];
            LoggerService.Log($"WaveformAnalyzer.DetectBpmAdvanced - Downsampled {sampleRate}Hz -> {sampleRate / factor}Hz ({samples.Length} -> {downsampled.Length} samples)");
            samples = downsampled;
            sampleRate = sampleRate / factor;
        }

        var (minBpm, maxBpm) = DetectAdaptiveRange(samples, sampleRate);

        var preprocessed = PreprocessForBeatDetection(samples, sampleRate);

        // Run all 3 onset detection methods in parallel (they are independent)
        double[] spectralFlux = Array.Empty<double>();
        double[] energyFlux = Array.Empty<double>();
        double[] complexOnset = Array.Empty<double>();

        // Capture local copies for thread safety
        var prepData = preprocessed;
        var sr = sampleRate;
        var minB = minBpm;
        var maxB = maxBpm;

        Parallel.Invoke(
            () => { (spectralFlux, _) = ComputeOnsetStrength(prepData, sr, minB, maxB); },
            () => { (energyFlux, _) = ComputeEnergyFluxOnset(prepData, sr, minB, maxB); },
            () => { (complexOnset, _) = ComputeComplexDomainOnset(prepData, sr, minB, maxB); }
        );

        var candidates = new List<(double bpm, double weight, string method)>();

        var sfBpm = EstimateBpmWithCandidates(spectralFlux, sampleRate, minBpm, maxBpm, TopKCandidates);
        foreach (var c in sfBpm) candidates.Add((c.bpm, c.confidence * 1.2, "spectral_flux"));

        var efBpm = EstimateBpmWithCandidates(energyFlux, sampleRate, minBpm, maxBpm, TopKCandidates);
        foreach (var c in efBpm) candidates.Add((c.bpm, c.confidence * 1.0, "energy_flux"));

        var coBpm = EstimateBpmWithCandidates(complexOnset, sampleRate, minBpm, maxBpm, TopKCandidates);
        foreach (var c in coBpm) candidates.Add((c.bpm, c.confidence * 1.1, "complex_domain"));

        var (bestBpm, confidence) = WeightedVotingAndHarmonicCheck(candidates, spectralFlux, sampleRate);

        return (bestBpm, confidence);
    }

    private float[] PreprocessForBeatDetection(float[] samples, int sampleRate)
    {
        var lowFreqEmphasis = LowFrequencyEmphasis(samples, sampleRate);
        var normalized = NormalizeAudio(lowFreqEmphasis);
        return normalized;
    }

    private float[] LowFrequencyEmphasis(float[] samples, int sampleRate)
    {
        const double cutoffFreq = 200.0;
        var result = new float[samples.Length];
        var (b0, b1, b2, a1, a2) = DesignButterworthFilter(cutoffFreq, sampleRate, isHighPass: false);

        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double x0 = samples[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            result[i] = (float)y0;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
        }

        return result;
    }

    private float[] NormalizeAudio(float[] samples)
    {
        float maxAbs = 0;
        foreach (var s in samples)
            if (Math.Abs(s) > maxAbs) maxAbs = Math.Abs(s);
        
        if (maxAbs < 1e-6f) return samples;
        
        var result = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            result[i] = samples[i] / maxAbs;
        
        return result;
    }

    private (double[] onsetStrength, double[] phases) ComputeOnsetStrength(float[] samples, int sampleRate, double minBpm, double maxBpm)
    {
        int numFrames = (samples.Length - WindowSize) / HopSize + 1;
        if (numFrames <= 0) return (Array.Empty<double>(), Array.Empty<double>());
        
        var onsetStrength = new double[numFrames];
        var phases = new double[numFrames];
        var previousSpectrum = new double[WindowSize / 2];
        var previousPhase = new double[WindowSize / 2];

        for (int frame = 0; frame < numFrames; frame++)
        {
            var frameStart = frame * HopSize;
            if (frameStart + WindowSize > samples.Length) break;

            var window = samples.AsSpan(frameStart, WindowSize);
            var (magnitude, phase) = ComputeMagnitudeSpectrumWithPhase(window);
            
            var flux = ComputeSpectralFlux(magnitude, previousSpectrum);
            onsetStrength[frame] = flux;
            phases[frame] = phase.Length > 0 ? phase[0] : 0;
            
            Array.Copy(magnitude, previousSpectrum, Math.Min(magnitude.Length, previousSpectrum.Length));
            Array.Copy(phase, previousPhase, Math.Min(phase.Length, previousPhase.Length));
        }

        onsetStrength = ApplyOnsetSmoothing(onsetStrength);
        return (onsetStrength, phases);
    }

    private (double[] onsetStrength, double[] phases) ComputeEnergyFluxOnset(float[] samples, int sampleRate, double minBpm, double maxBpm)
    {
        int numFrames = (samples.Length - WindowSize) / HopSize + 1;
        if (numFrames <= 0) return (Array.Empty<double>(), Array.Empty<double>());
        
        var onsetStrength = new double[numFrames];
        var previousEnergy = new double[WindowSize / 2];

        for (int frame = 0; frame < numFrames; frame++)
        {
            var frameStart = frame * HopSize;
            if (frameStart + WindowSize > samples.Length) break;

            var window = samples.AsSpan(frameStart, WindowSize);
            var (magnitude, _) = ComputeMagnitudeSpectrumWithPhase(window);
            
            double currentEnergy = 0;
            for (int i = 0; i < magnitude.Length; i++)
                currentEnergy += magnitude[i] * magnitude[i];
            currentEnergy = Math.Sqrt(currentEnergy / magnitude.Length);
            
            double flux = Math.Max(0, currentEnergy - previousEnergy[0]);
            onsetStrength[frame] = flux;
            
            for (int i = 0; i < previousEnergy.Length - 1; i++)
                previousEnergy[i] = previousEnergy[i + 1];
            previousEnergy[previousEnergy.Length - 1] = currentEnergy;
        }

        onsetStrength = ApplyOnsetSmoothing(onsetStrength);
        return (onsetStrength, Array.Empty<double>());
    }

    private (double[] onsetStrength, double[] phases) ComputeComplexDomainOnset(float[] samples, int sampleRate, double minBpm, double maxBpm)
    {
        int numFrames = (samples.Length - WindowSize) / HopSize + 1;
        if (numFrames <= 0) return (Array.Empty<double>(), Array.Empty<double>());
        
        var onsetStrength = new double[numFrames];
        var previousSpectrum = new System.Numerics.Complex[WindowSize / 2];

        for (int frame = 0; frame < numFrames; frame++)
        {
            var frameStart = frame * HopSize;
            if (frameStart + WindowSize > samples.Length) break;

            var window = samples.AsSpan(frameStart, WindowSize);
            var (magnitude, phase) = ComputeMagnitudeSpectrumWithPhase(window);
            var complexSpectrum = new System.Numerics.Complex[WindowSize / 2];
            for (int i = 0; i < complexSpectrum.Length; i++)
                complexSpectrum[i] = System.Numerics.Complex.FromPolarCoordinates(magnitude[i], phase[i]);
            
            double flux = 0;
            for (int i = 0; i < complexSpectrum.Length && i < previousSpectrum.Length; i++)
            {
                var diff = complexSpectrum[i] - previousSpectrum[i];
                if (diff.Real > 0) flux += diff.Real;
            }
            onsetStrength[frame] = flux;
            
            previousSpectrum = complexSpectrum;
        }

        onsetStrength = ApplyOnsetSmoothing(onsetStrength);
        return (onsetStrength, Array.Empty<double>());
    }

    private double[] ApplyOnsetSmoothing(double[] onsetStrength)
    {
        int windowSize = 3;
        var smoothed = new double[onsetStrength.Length];
        
        for (int i = 0; i < onsetStrength.Length; i++)
        {
            double sum = 0;
            int count = 0;
            for (int j = -windowSize; j <= windowSize; j++)
            {
                int idx = i + j;
                if (idx >= 0 && idx < onsetStrength.Length)
                {
                    sum += onsetStrength[idx];
                    count++;
                }
            }
            smoothed[i] = sum / count;
        }
        
        return smoothed;
    }

    private (double[] magnitude, double[] phase) ComputeMagnitudeSpectrumWithPhase(Span<float> window)
    {
        var complex = new System.Numerics.Complex[WindowSize];
        for (int i = 0; i < WindowSize; i++)
            complex[i] = i < window.Length ? new System.Numerics.Complex(window[i], 0) : System.Numerics.Complex.Zero;

        FFT(complex);

        var magnitude = new double[WindowSize / 2];
        var phase = new double[WindowSize / 2];
        for (int i = 0; i < magnitude.Length; i++)
        {
            magnitude[i] = System.Numerics.Complex.Abs(complex[i]);
            phase[i] = Math.Atan2(complex[i].Imaginary, complex[i].Real);
        }

        return (magnitude, phase);
    }

    private double ComputeSpectralFlux(double[] spectrum, double[] previousSpectrum)
    {
        double flux = 0;
        for (int i = 0; i < spectrum.Length && i < previousSpectrum.Length; i++)
        {
            var diff = spectrum[i] - previousSpectrum[i];
            if (diff > 0) flux += diff * diff;
        }
        return Math.Sqrt(flux);
    }

    private (double minBpm, double maxBpm) DetectAdaptiveRange(float[] samples, int sampleRate)
    {
        // Always search the full useful BPM range (50-200).
        // The previous adaptive logic would cap maxBpm at 140 for bass-heavy tracks,
        // which incorrectly excluded fast tempos like 152 BPM (common in reggaetón, trap, EDM).
        double minBpm = 50;
        double maxBpm = 200;

        LoggerService.Log($"WaveformAnalyzer.DetectAdaptiveRange - Using range {minBpm}-{maxBpm} BPM");
        return (minBpm, maxBpm);
    }

    private List<(double bpm, double confidence)> EstimateBpmWithCandidates(
        double[] onsetStrength, int sampleRate, double minBpm, double maxBpm, int topK)
    {
        var candidates = new List<(double bpm, double confidence)>();
        
        if (onsetStrength.Length < 100) return candidates;

        int minLag = (int)(sampleRate * 60.0 / maxBpm / HopSize);
        int maxLag = (int)(sampleRate * 60.0 / minBpm / HopSize);
        if (minLag < 1) minLag = 1;
        if (maxLag > onsetStrength.Length - 1) maxLag = onsetStrength.Length - 1;

        var acf = new double[maxLag + 1];
        for (int lag = minLag; lag <= maxLag && lag < onsetStrength.Length; lag++)
        {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < onsetStrength.Length - lag; i++)
            {
                sum += onsetStrength[i] * onsetStrength[i + lag];
                count++;
            }
            if (count > 0) acf[lag] = sum / count;
        }

        var peaks = new List<(int lag, double value)>();
        for (int lag = minLag + 1; lag < maxLag - 1; lag++)
        {
            if (acf[lag] > acf[lag - 1] && acf[lag] > acf[lag + 1] && acf[lag] > 0)
                peaks.Add((lag, acf[lag]));
        }

        peaks.Sort((a, b) => b.value.CompareTo(a.value));
        var topPeaks = peaks.Take(Math.Min(topK * 2, peaks.Count)).ToList();

        foreach (var peak in topPeaks)
        {
            double bpm = sampleRate * 60.0 / (peak.lag * HopSize);
            while (bpm < minBpm) bpm *= 2;
            while (bpm > maxBpm) bpm /= 2;
            
            double normalizedBpm = Math.Round(bpm * 2) / 2;
            double confidence = peak.value / (acf[minLag] > 0 ? acf[minLag] : 1);
            confidence = Math.Min(1.0, confidence);
            
            candidates.Add((normalizedBpm, confidence));
        }

        return candidates.Take(topK).ToList();
    }

    private (double bpm, double confidence) WeightedVotingAndHarmonicCheck(
        List<(double bpm, double weight, string method)> candidates, 
        double[] onsetStrength, int sampleRate)
    {
        if (candidates.Count == 0) return (0, 0);

        var bpmBuckets = new Dictionary<double, double>();
        
        foreach (var candidate in candidates)
        {
            var normalizedBpm = Math.Round(candidate.bpm * 2) / 2;
            
            if (!bpmBuckets.ContainsKey(normalizedBpm))
                bpmBuckets[normalizedBpm] = 0;
            bpmBuckets[normalizedBpm] += candidate.weight;

            double halfBpm = normalizedBpm / 2;
            double doubleBpm = normalizedBpm * 2;
            
            if (!bpmBuckets.ContainsKey(halfBpm))
                bpmBuckets[halfBpm] = 0;
            bpmBuckets[halfBpm] += candidate.weight * 0.3;

            if (!bpmBuckets.ContainsKey(doubleBpm))
                bpmBuckets[doubleBpm] = 0;
            bpmBuckets[doubleBpm] += candidate.weight * 0.3;
        }

        var best = bpmBuckets.OrderByDescending(x => x.Value).First();
        double bestBpm = best.Key;
        double baseConfidence = best.Value / candidates.Count;

        double consistency = CheckBeatPeriodConsistency(onsetStrength, bestBpm, sampleRate);
        double finalConfidence = baseConfidence * (0.5 + 0.5 * consistency);

        double adjustedBpm = PreferFundamentalFrequency(bestBpm, bpmBuckets, onsetStrength, sampleRate);
        
        return (Math.Round(adjustedBpm * 10) / 10, Math.Min(1.0, finalConfidence));
    }

    private double PreferFundamentalFrequency(double bpm, Dictionary<double, double> bpmBuckets, double[] onsetStrength, int sampleRate)
    {
        // Only correct BPMs that are clearly outside the usable range (< 55 or > 200).
        // Range 55-200 covers all common music genres (hip-hop ~70, pop ~120, reggaetón ~90-160, 
        // EDM ~128-150, drum & bass ~170-180).
        
        if (bpm < 55 && bpmBuckets.Count > 0)
        {
            double[] multipliers = { 2.0, 1.5, 3.0 };
            foreach (var mult in multipliers)
            {
                double potentialBpm = bpm * mult;
                if (potentialBpm >= 60 && potentialBpm <= 200)
                {
                    var roundedPf = Math.Round(potentialBpm * 2) / 2;
                    if (bpmBuckets.TryGetValue(roundedPf, out double pfWeight) && pfWeight > 0)
                    {
                        double pfConsistency = CheckBeatPeriodConsistency(onsetStrength, potentialBpm, sampleRate);
                        double bpmConsistency = CheckBeatPeriodConsistency(onsetStrength, bpm, sampleRate);
                        
                        if (pfConsistency > bpmConsistency * 0.85)
                        {
                            LoggerService.Log($"WaveformAnalyzer.PreferFundamental - {bpm} -> {potentialBpm} (x{mult})");
                            return potentialBpm;
                        }
                    }
                }
            }
        }
        
        // Only reduce BPMs above 200 (truly unreasonable tempos)
        if (bpm > 200)
        {
            double[] divisors = { 2.0, 1.5, 3.0 };
            foreach (var div in divisors)
            {
                double potentialBpm = bpm / div;
                if (potentialBpm >= 60 && potentialBpm <= 200)
                {
                    var roundedPf = Math.Round(potentialBpm * 2) / 2;
                    if (bpmBuckets.TryGetValue(roundedPf, out double pfWeight) && pfWeight > 0)
                    {
                        double pfConsistency = CheckBeatPeriodConsistency(onsetStrength, potentialBpm, sampleRate);
                        double bpmConsistency = CheckBeatPeriodConsistency(onsetStrength, bpm, sampleRate);
                        
                        if (pfConsistency >= bpmConsistency * 0.8)
                        {
                            LoggerService.Log($"WaveformAnalyzer.PreferFundamental - {bpm} -> {potentialBpm} (/{div})");
                            return potentialBpm;
                        }
                    }
                }
            }
        }
        
        return bpm;
    }

    private double CheckBeatPeriodConsistency(double[] onsetStrength, double bpm, int sampleRate)
    {
        double secondsPerBeat = 60.0 / bpm;
        int framesPerBeat = (int)(secondsPerBeat * sampleRate / HopSize);
        if (framesPerBeat < 1) framesPerBeat = 1;

        int checkLength = Math.Min(onsetStrength.Length, framesPerBeat * 32);
        if (checkLength < framesPerBeat * 4) return 0.5;

        double consistency = 0;
        int count = 0;

        for (int i = framesPerBeat; i < checkLength; i++)
        {
            double beatPhase = (i % framesPerBeat) / (double)framesPerBeat;
            if (beatPhase < 0.2 || beatPhase > 0.8)
            {
                consistency += onsetStrength[i];
                count++;
            }
        }

        if (count == 0) return 0.5;
        double avgOnset = consistency / count;

        double totalAvg = 0;
        for (int i = 0; i < checkLength; i++)
            totalAvg += onsetStrength[i];
        totalAvg /= checkLength;

        return totalAvg > 0 ? Math.Min(1.0, avgOnset / totalAvg) : 0.5;
    }

    private void FFT(System.Numerics.Complex[] data) => FftHelper.FFT(data);

    // =========================================================================
    // TRANSIENT-BASED BPM DETECTION (Dual-Band + Beat Grid Fitting)
    // =========================================================================

    /// <summary>
    /// <summary>
    /// Designs Butterworth filter coefficients using bilinear transform.
    /// Returns (b0, b1, b2, a1, a2) coefficients.
    /// </summary>
    private (double b0, double b1, double b2, double a1, double a2) DesignButterworthFilter(double cutoffHz, int sampleRate, bool isHighPass = false)
    {
        double nyquist = sampleRate / 2.0;
        double norm = cutoffHz / nyquist;
        if (norm >= 1.0) norm = 0.99;
        if (isHighPass && norm <= 0.0) norm = 0.001;

        double omega = Math.Tan(Math.PI * norm);
        double omega2 = omega * omega;
        double sqrt2 = Math.Sqrt(2.0);
        double denom = 1.0 + sqrt2 * omega + omega2;

        double b0, b1, b2;
        if (isHighPass)
        {
            b0 = 1.0 / denom;
            b1 = -2.0 / denom;
            b2 = 1.0 / denom;
        }
        else
        {
            b0 = omega2 / denom;
            b1 = 2.0 * omega2 / denom;
            b2 = omega2 / denom;
        }

        double a1 = 2.0 * (omega2 - 1.0) / denom;
        double a2 = (1.0 - sqrt2 * omega + omega2) / denom;

        return (b0, b1, b2, a1, a2);
    }

    /// <summary>
    /// 2nd-order Butterworth IIR low-pass filter.
    /// </summary>
    private float[] ApplyLowPassFilter(float[] samples, int sampleRate, double cutoffHz)
    {
        var result = new float[samples.Length];
        var (b0, b1, b2, a1, a2) = DesignButterworthFilter(cutoffHz, sampleRate, isHighPass: false);

        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double x0 = samples[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            result[i] = (float)y0;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
        }
        return result;
    }

    /// <summary>
    /// 2nd-order Butterworth IIR high-pass filter.
    /// </summary>
    private float[] ApplyHighPassFilter(float[] samples, int sampleRate, double cutoffHz)
    {
        var result = new float[samples.Length];
        var (b0, b1, b2, a1, a2) = DesignButterworthFilter(cutoffHz, sampleRate, isHighPass: true);

        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            double x0 = samples[i];
            double y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            result[i] = (float)y0;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
        }
        return result;
    }

    /// <summary>
    /// Band-pass filter: cascaded high-pass + low-pass.
    /// </summary>
    private float[] ApplyBandPassFilter(float[] samples, int sampleRate, double lowCutoff, double highCutoff)
    {
        var hp = ApplyHighPassFilter(samples, sampleRate, lowCutoff);
        return ApplyLowPassFilter(hp, sampleRate, highCutoff);
    }

    /// <summary>
    /// Isolates two frequency bands for transient detection:
    /// - Low band (20-150 Hz): kick drum
    /// - High band (2000-8000 Hz): snare, hi-hat, claps
    /// </summary>
    public (float[] lowBand, float[] hiBand) IsolateTransientBands(float[] samples, int sampleRate)
    {
        var lowBand = ApplyLowPassFilter(samples, sampleRate, 150.0);
        var hiBand = ApplyBandPassFilter(samples, sampleRate, 2000.0, 8000.0);
        return (lowBand, hiBand);
    }

    /// <summary>
    /// Detects transient positions in a filtered signal using RMS energy peaks.
    /// Returns a list of (positionInSeconds, amplitude) tuples.
    /// </summary>
    public List<(double position, double amplitude)> DetectTransients(float[] signal, int sampleRate, double thresholdMultiplier = 2.0)
    {
        // ~10ms windows for transient detection
        int windowSize = Math.Max(1, sampleRate / 100);
        int hopSize = windowSize / 2;
        // Minimum 30ms between transients to avoid duplicates
        double deadTimeSec = 0.030;

        int numFrames = (signal.Length - windowSize) / hopSize;
        if (numFrames <= 0) return new List<(double, double)>();

        // Calculate RMS energy per frame
        var energy = new double[numFrames];
        for (int f = 0; f < numFrames; f++)
        {
            int start = f * hopSize;
            double sum = 0;
            for (int i = start; i < start + windowSize && i < signal.Length; i++)
                sum += signal[i] * (double)signal[i];
            energy[f] = Math.Sqrt(sum / windowSize);
        }

        // Calculate local adaptive threshold using a ~200ms sliding window
        int thresholdWindowFrames = Math.Max(3, (int)(0.200 / (hopSize / (double)sampleRate)));
        var transients = new List<(double position, double amplitude)>();
        double lastTransientTime = -1.0;

        for (int f = 1; f < numFrames; f++)
        {
            // Local average in surrounding window
            int wStart = Math.Max(0, f - thresholdWindowFrames);
            int wEnd = Math.Min(numFrames, f + thresholdWindowFrames);
            double localAvg = 0;
            for (int j = wStart; j < wEnd; j++)
                localAvg += energy[j];
            localAvg /= (wEnd - wStart);

            // Detect onset: energy rise exceeds threshold * local average
            double rise = energy[f] - energy[f - 1];
            if (rise > 0 && energy[f] > localAvg * thresholdMultiplier)
            {
                double timeSec = (f * hopSize) / (double)sampleRate;

                // Apply dead time
                if (timeSec - lastTransientTime >= deadTimeSec)
                {
                    transients.Add((timeSec, energy[f]));
                    lastTransientTime = timeSec;
                }
            }
        }

        return transients;
    }

    /// <summary>
    /// Merges transient lists from two bands, removing duplicates within 15ms.
    /// </summary>
    public List<(double position, double amplitude)> MergeTransients(
        List<(double position, double amplitude)> lowBandTransients,
        List<(double position, double amplitude)> hiBandTransients)
    {
        var all = new List<(double position, double amplitude)>();
        all.AddRange(lowBandTransients);
        all.AddRange(hiBandTransients);
        all.Sort((a, b) => a.position.CompareTo(b.position));

        if (all.Count == 0) return all;

        // Remove duplicates within 15ms, keeping the stronger one
        var merged = new List<(double position, double amplitude)> { all[0] };
        for (int i = 1; i < all.Count; i++)
        {
            var last = merged[^1];
            if (all[i].position - last.position < 0.015)
            {
                // Keep the one with higher amplitude
                if (all[i].amplitude > last.amplitude)
                    merged[^1] = all[i];
            }
            else
            {
                merged.Add(all[i]);
            }
        }

        return merged;
    }

    /// <summary>
    /// Autocorrelation on transient positions to find BPM candidates.
    /// For each candidate BPM, counts how many transients fall within ±15ms of a beat grid tick.
    /// </summary>
    public List<(double bpm, double score)> AutocorrelateTransients(
        List<(double position, double amplitude)> transients, double minBpm = 50, double maxBpm = 200)
    {
        if (transients.Count < 10) return new List<(double, double)>();

        var positions = transients.Select(t => t.position).ToArray();
        var amplitudes = transients.Select(t => t.amplitude).ToArray();
        double tolerance = 0.015; // ±15ms

        // Test every 0.5 BPM increment for fine resolution
        var candidates = new List<(double bpm, double score)>();

        for (double bpm = minBpm; bpm <= maxBpm; bpm += 0.5)
        {
            double period = 60.0 / bpm;
            double totalScore = 0;
            int hits = 0;

            // Use first transient as reference point, evaluate alignment
            for (int refIdx = 0; refIdx < Math.Min(5, positions.Length); refIdx++)
            {
                double refTime = positions[refIdx];
                double score = 0;
                int hitCount = 0;

                for (int i = 0; i < positions.Length; i++)
                {
                    double dt = positions[i] - refTime;
                    if (dt < 0) continue;

                    // How close is this transient to the nearest beat grid tick?
                    double beatIndex = dt / period;
                    double fractional = beatIndex - Math.Round(beatIndex);
                    double deviation = Math.Abs(fractional * period);

                    if (deviation < tolerance)
                    {
                        score += amplitudes[i]; // weight by amplitude
                        hitCount++;
                    }
                }

                if (hitCount > hits || (hitCount == hits && score > totalScore))
                {
                    totalScore = score;
                    hits = hitCount;
                }
            }

            // Normalize: score = fraction of transients that align with the grid
            double normalizedScore = (double)hits / positions.Length;
            candidates.Add((bpm, normalizedScore));
        }

        // Return top candidates sorted by score
        return candidates.OrderByDescending(c => c.score).Take(10).ToList();
    }

    /// <summary>
    /// Scores a BPM candidate by fitting a beat grid to the transient positions.
    /// Finds the optimal downbeat alignment and returns the standard deviation
    /// of distances between grid ticks and their nearest transient.
    /// Lower stdDev = better fit.
    /// </summary>
    public (double stdDev, double hitRate, double bestDownbeat) ScoreBeatGrid(
        List<(double position, double amplitude)> transients, double candidateBpm, double segmentDuration)
    {
        if (transients.Count < 8 || candidateBpm <= 0)
            return (double.MaxValue, 0, 0);

        double period = 60.0 / candidateBpm;
        var positions = transients.Select(t => t.position).ToArray();

        // Try multiple downbeat positions (top 10 strongest transients)
        var strongTransients = transients
            .OrderByDescending(t => t.amplitude)
            .Take(Math.Min(10, transients.Count))
            .Select(t => t.position)
            .ToList();

        double bestStdDev = double.MaxValue;
        double bestHitRate = 0;
        double bestDownbeat = 0;

        foreach (var downbeat in strongTransients)
        {
            int numTicks = (int)((segmentDuration - downbeat) / period) + 1;
            if (numTicks < 8) continue;

            var deviations = new List<double>();
            int hits = 0;

            for (int tick = 0; tick < numTicks; tick++)
            {
                double gridTime = downbeat + tick * period;

                // Find nearest transient using binary search
                double minDist = double.MaxValue;
                int searchIdx = Array.BinarySearch(positions, gridTime);
                if (searchIdx < 0) searchIdx = ~searchIdx;

                // Check neighbors
                for (int j = Math.Max(0, searchIdx - 1); j <= Math.Min(positions.Length - 1, searchIdx + 1); j++)
                {
                    double dist = Math.Abs(positions[j] - gridTime);
                    if (dist < minDist) minDist = dist;
                }

                deviations.Add(minDist);
                if (minDist < 0.020) hits++; // 20ms tolerance for a "hit"
            }

            // Calculate standard deviation
            double mean = deviations.Average();
            double variance = deviations.Sum(d => (d - mean) * (d - mean)) / deviations.Count;
            double stdDev = Math.Sqrt(variance);
            double hitRate = (double)hits / numTicks;

            if (stdDev < bestStdDev || (Math.Abs(stdDev - bestStdDev) < 0.001 && hitRate > bestHitRate))
            {
                bestStdDev = stdDev;
                bestHitRate = hitRate;
                bestDownbeat = downbeat;
            }
        }

        return (bestStdDev, bestHitRate, bestDownbeat);
    }

    /// <summary>
    /// Full transient-based BPM detection pipeline:
    /// Dual-band filtering → transient detection → autocorrelation → beat grid fitting.
    /// Returns (bpm, confidence).
    /// </summary>
    public (double bpm, double confidence) DetectBpmByTransientGrid(float[] monoSamples, int sampleRate)
    {
        if (monoSamples.Length < sampleRate * 5) return (0, 0);

        double segmentDuration = monoSamples.Length / (double)sampleRate;

        // Step 1: Dual-band isolation
        var (lowBand, hiBand) = IsolateTransientBands(monoSamples, sampleRate);

        // Step 2: Detect transients in each band
        var lowTransients = DetectTransients(lowBand, sampleRate, 2.0);
        var hiTransients = DetectTransients(hiBand, sampleRate, 1.8);

        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Low-band transients: {lowTransients.Count}, Hi-band transients: {hiTransients.Count}");

        // Step 3: Merge transients
        var allTransients = MergeTransients(lowTransients, hiTransients);
        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Merged transients: {allTransients.Count}");

        if (allTransients.Count < 15)
        {
            LoggerService.Log("WaveformAnalyzer.TransientGrid - Too few transients, falling back");
            return (0, 0);
        }

        // Step 4: Autocorrelation on transient positions → top candidates
        var candidates = AutocorrelateTransients(allTransients, 50, 200);
        if (candidates.Count == 0) return (0, 0);

        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Top candidates: {string.Join(", ", candidates.Take(5).Select(c => $"{c.bpm:F1}({c.score:F2})"))}");

        // Step 5: Beat Grid Fitting for top candidates
        // Use a composite score: hitRate is primary (more transients aligned = correct BPM),
        // stdDev is secondary (precision of alignment).
        double bestBpm = 0;
        double bestComposite = -1;
        double bestHitRate = 0;
        double bestStdDev = double.MaxValue;

        foreach (var candidate in candidates.Take(5))
        {
            var (stdDev, hitRate, _) = ScoreBeatGrid(allTransients, candidate.bpm, segmentDuration);

            // Composite score: hitRate weighted heavily, penalize high stdDev
            // hitRate range: 0-1, stdDev range: ~0.01-0.1
            // A candidate with hitRate=0.38 should always beat hitRate=0.15 regardless of stdDev
            double composite = hitRate - (stdDev * 2.0);

            LoggerService.Log($"WaveformAnalyzer.GridFit - BPM {candidate.bpm:F1}: stdDev={stdDev:F4}, hitRate={hitRate:F2}, composite={composite:F3}");

            if (composite > bestComposite)
            {
                bestComposite = composite;
                bestHitRate = hitRate;
                bestStdDev = stdDev;
                bestBpm = candidate.bpm;
            }
        }

        double confidence = Math.Min(1.0, bestHitRate);
        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Best: {bestBpm:F1} BPM (stdDev={bestStdDev:F4}, hitRate={bestHitRate:F2}, composite={bestComposite:F3})");

        return (bestBpm, confidence);
    }
}
