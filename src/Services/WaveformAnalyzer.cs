using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;
using NAudio.Dsp;

namespace AudioAnalyzer.Services;

public class WaveformAnalyzer : IWaveformAnalyzerService
{
    private const int WindowSize = DspConstants.FFT_SIZE;
    private const int HopSize = DspConstants.HOP_SIZE;
    private const double TempoChangeThreshold = 2.0;
    private const int BarsForSection = 8;
    private const int TopKCandidates = 5;

    // Cache de transientes para BpmDetector
    private List<(double position, double amplitude)> _lastTransients;
    private double _lastSegmentDuration;

    public List<(double position, double amplitude)> GetLastTransients()
        => _lastTransients ?? new List<(double, double)>();

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
        var lowBand = ApplyLowPassFilter(samples, sampleRate, DspConstants.TRANSIENT_LOW_BAND);
        var hiBand = ApplyBandPassFilter(samples, sampleRate, DspConstants.TRANSIENT_HIGH_BAND_MIN, DspConstants.TRANSIENT_HIGH_BAND_MAX);
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
        double deadTimeSec = DspConstants.TRANSIENT_DEAD_TIME;

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
            if (all[i].position - last.position < DspConstants.DUPLICATE_THRESHOLD)
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

        // Test every 0.25 BPM increment for fine resolution
        var candidates = new List<(double bpm, double score)>();

        for (double bpm = minBpm; bpm <= maxBpm; bpm += 0.25)
        {
            double period = 60.0 / bpm;
            // Tolerancia proporcional al período: 4% del beat, mínimo 15ms
            // Elimina sesgo hacia BPMs altos donde 15ms fijo es muy generoso
            double dynamicTolerance = Math.Max(0.015, period * 0.04);
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

                    if (deviation < dynamicTolerance)
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
                                if (minDist < DspConstants.HIT_TOLERANCE_SEC) hits++;  // 25ms tolerance for a "hit"
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
    /// Calcula el Spectral Flux Onset Strength Envelope usando NAudio FFT.
    ///
    /// DIFERENCIA CLAVE vs DetectTransients():
    ///   DetectTransients  → busca peaks de AMPLITUD absoluta
    ///                        → falla en audio masterizado (peaks aplastados)
    ///   SpectralFlux      → mide CAMBIOS de energía espectral frame a frame
    ///                        → robusto a compresión: un beat comprimido sigue
    ///                          generando un cambio espectral medible
    ///
    /// Equivalente C# de librosa.onset.onset_strength() usando infraestructura
    /// NAudio que ya existe en el proyecto.
    /// </summary>
    private double[] ComputeSpectralFluxOnsets(float[] samples, int sampleRate)
    {
        int numFrames = (samples.Length - DspConstants.SF_FFT_SIZE) / DspConstants.SF_HOP_SIZE + 1;
        if (numFrames <= 0) return Array.Empty<double>();

        var onsetStrength = new double[numFrames];
        var previousSpectrum = new double[DspConstants.SF_FFT_SIZE / 2];
        var window = new float[DspConstants.SF_FFT_SIZE];

        // Límite espectral: 8000 Hz (Sweet spot para retener ataques de percusión y filtrar artefactos de compresión)
        int maxBin = (int)(8000.0 * DspConstants.SF_FFT_SIZE / sampleRate);
        if (maxBin > DspConstants.SF_FFT_SIZE / 2) maxBin = DspConstants.SF_FFT_SIZE / 2;

        for (int frame = 0; frame < numFrames; frame++)
        {
            int start = frame * DspConstants.SF_HOP_SIZE;
            
            // Aplicar ventana de Hann
            for (int i = 0; i < DspConstants.SF_FFT_SIZE; i++)
            {
                if (start + i < samples.Length)
                {
                    double multiplier = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (DspConstants.SF_FFT_SIZE - 1)));
                    window[i] = (float)(samples[start + i] * multiplier);
                }
                else
                {
                    window[i] = 0;
                }
            }

            var complex = new System.Numerics.Complex[DspConstants.SF_FFT_SIZE];
            for (int i = 0; i < DspConstants.SF_FFT_SIZE; i++)
                complex[i] = new System.Numerics.Complex(window[i], 0);

            FftHelper.FFT(complex);

            double flux = 0;
            // Calcular flujo espectral solo hasta maxBin (Band-Limited)
            for (int i = 0; i < maxBin; i++)
            {
                double magnitude = System.Numerics.Complex.Abs(complex[i]);
                double diff = magnitude - previousSpectrum[i];
                if (diff > 0) flux += diff;
                previousSpectrum[i] = magnitude;
            }

            onsetStrength[frame] = flux;
        }

        // Normalización global al rango [0,1]
        double maxFlux = 0;
        for (int i = 0; i < numFrames; i++)
            if (onsetStrength[i] > maxFlux) maxFlux = onsetStrength[i];

        if (maxFlux > 0)
        {
            for (int i = 0; i < numFrames; i++)
                onsetStrength[i] /= maxFlux;
        }

        return onsetStrength;
    }

    /// <summary>
    /// Extrae picos locales del onset strength envelope.
    /// Non-maximum suppression con ventana de tiempo configurable.
    /// Equivalente conceptual a la detección de transientes, pero sobre
    /// la curva de flux normalizada en lugar de la amplitud cruda.
    /// </summary>
    private List<(double position, double amplitude)> PickOnsetPeaks(double[] onsetStrength, int sampleRate)
    {
        var peaks = new List<(double position, double amplitude)>();
        if (onsetStrength.Length == 0) return peaks;

        int windowFrames = (int)(DspConstants.SF_ONSET_WINDOW_SEC * sampleRate / DspConstants.SF_HOP_SIZE);
        if (windowFrames < 1) windowFrames = 1;

        for (int i = 0; i < onsetStrength.Length; i++)
        {
            double currentValue = onsetStrength[i];
            bool isPeak = true;
            
            int start = Math.Max(0, i - windowFrames);
            int end = Math.Min(onsetStrength.Length - 1, i + windowFrames);
            
            double sum = 0;
            int count = 0;

            // Evaluar Non-Maximum Suppression y calcular promedio local simultáneamente
            for (int j = start; j <= end; j++)
            {
                sum += onsetStrength[j];
                count++;
                
                if (j != i && onsetStrength[j] >= currentValue)
                {
                    isPeak = false;
                }
            }

            if (isPeak)
            {
                // Umbral Adaptativo: Superar el promedio local + un piso de ruido mínimo de 0.05
                double localAverage = count > 0 ? sum / count : 0;
                double adaptiveThreshold = (localAverage * 1.5) + 0.05;

                if (currentValue > adaptiveThreshold)
                {
                    double timeInSeconds = (i * DspConstants.SF_HOP_SIZE) / (double)sampleRate;
                    peaks.Add((timeInSeconds, currentValue));
                }
            }
        }

        return peaks;
    }

    /// <summary>
    /// Pipeline BPM completo basado en Spectral Flux.
    ///
    /// Arquitectura: nuevo origen de onsets + pipeline existente reutilizado
    ///
    ///   ComputeSpectralFluxOnsets()  [NUEVO]  → onsets robustos a mastering
    ///         ↓
    ///   PickOnsetPeaks()             [NUEVO]  → extrae peaks de la curva
    ///         ↓
    ///   MergeTransients()            [existente] → deduplica
    ///         ↓
    ///   AutocorrelateTransients()    [existente] → candidatos BPM
    ///         ↓
    ///   ScoreBeatGrid()              [existente] → scoring final
    ///
    /// Retorna la misma firma que DetectBpmByTransientGrid() para
    /// poder intercambiarse o combinarse en BpmDetector.
    /// </summary>
    public (double bpm, double confidence, List<(double bpm, double score)> allCandidates)
        DetectBpmBySpectralFlux(float[] monoSamples, int sampleRate)
    {
        var empty = new List<(double bpm, double score)>();
        if (monoSamples.Length < sampleRate * 5) return (0, 0, empty);

        double segmentDuration = monoSamples.Length / (double)sampleRate;

        // Paso 1: Calcular onset strength via Spectral Flux
        var rawOnsets = ComputeSpectralFluxOnsets(monoSamples, sampleRate);
        if (rawOnsets.Length == 0) return (0, 0, empty);

        // Paso 2: Extraer picos locales (onset events)
        var sfOnsets = PickOnsetPeaks(rawOnsets, sampleRate);

        LoggerService.Log($"[SpectralFlux] Raw frames: {rawOnsets.Length}, Onset peaks: {sfOnsets.Count}");

        if (sfOnsets.Count < 15) return (0, 0, empty);

        // Paso 3: Merge para eliminar duplicados muy cercanos
        // Pasamos sfOnsets en ambas bandas — MergeTransients deduplica por tiempo
        var merged = MergeTransients(sfOnsets, new List<(double, double)>());

        // Paso 4: Autocorrelación para encontrar candidatos BPM
        var candidates = AutocorrelateTransients(
            merged,
            DspConstants.BPM_RANGE_MIN,
            DspConstants.BPM_RANGE_MAX);

        if (candidates.Count == 0) return (0, 0, empty);

        // Paso 5: Grid fitting sobre los top candidatos
        double bestBpm       = 0;
        double bestComposite = double.MinValue;
        double bestHitRate   = 0;
        var allCandidates    = new List<(double bpm, double score)>();

        foreach (var candidate in candidates.Take(10))
        {
            var (stdDev, hitRate, _) = ScoreBeatGrid(merged, candidate.bpm, segmentDuration);
            double composite = (hitRate * 1.3) - (stdDev * 1.5);

            allCandidates.Add((candidate.bpm, composite));

            LoggerService.Log($"[SpectralFlux] Candidato {candidate.bpm:F1} BPM: " +
                $"hitRate={hitRate:F2}, stdDev={stdDev:F4}, composite={composite:F3}");

            if (composite > bestComposite)
            {
                bestComposite = composite;
                bestHitRate   = hitRate;
                bestBpm       = candidate.bpm;
            }
        }

        // Preferir fundamental sobre sub-armónico:
        // Si el mejor BPM < 90, verificar si existe candidato ~2x con score razonable.
        // En música real, el tempo completo (152) es más probable que su mitad (76).
        if (bestBpm > 0 && bestBpm < 90)
        {
            double doubleBpm = bestBpm * 2.0;
            var doubleCandidate = allCandidates.FirstOrDefault(
                c => Math.Abs(c.bpm - doubleBpm) < 3.0);
            
            if (doubleCandidate.bpm > 0 && doubleCandidate.score > bestComposite * 0.35)
            {
                LoggerService.Log($"[SpectralFlux] Preferencia fundamental: {bestBpm:F1} → {doubleCandidate.bpm:F1} BPM " +
                    $"(sub-score={doubleCandidate.score:F3} vs best={bestComposite:F3})");
                bestBpm = doubleCandidate.bpm;
                bestHitRate = Math.Min(1.0, bestHitRate * 0.9);
            }
        }

        double confidence = Math.Min(1.0, bestHitRate);
        LoggerService.Log($"[SpectralFlux] Mejor: {bestBpm:F1} BPM (conf: {confidence:F2}, " +
            $"composite: {bestComposite:F3})");

        return (bestBpm, confidence, allCandidates);
    }

    /// <summary>
    /// Full transient-based BPM detection pipeline:
    /// Dual-band filtering → transient detection → autocorrelation → beat grid fitting.
    /// Returns (bpm, confidence).
    /// </summary>
    public (double bpm, double confidence, List<(double bpm, double score)> allCandidates) DetectBpmByTransientGrid(float[] monoSamples, int sampleRate)
    {
        if (monoSamples.Length < sampleRate * 5) return (0, 0, new List<(double, double)>());

        double segmentDuration = monoSamples.Length / (double)sampleRate;

        // Step 1: Dual-band isolation
        var (lowBand, hiBand) = IsolateTransientBands(monoSamples, sampleRate);

        // Step 2: Detect transients in each band (usando thresholds reducidos para audio masterizado)
        var lowTransients = DetectTransients(lowBand, sampleRate, DspConstants.TRANSIENT_THRESHOLD_LOW);
        var hiTransients = DetectTransients(hiBand, sampleRate, DspConstants.TRANSIENT_THRESHOLD_HI);

        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Low-band transients: {lowTransients.Count}, Hi-band transients: {hiTransients.Count}");

        // Step 3: Merge transients
        var allTransients = MergeTransients(lowTransients, hiTransients);
        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Merged transients: {allTransients.Count}");

        // Cache transients para BpmDetector
        _lastTransients = allTransients;
        _lastSegmentDuration = segmentDuration;

        if (allTransients.Count < 15)
        {
            LoggerService.Log("WaveformAnalyzer.TransientGrid - Too few transients, falling back");
            return (0, 0, new List<(double, double)>());
        }

        // Step 4: Autocorrelation on transient positions → top candidates
        var candidates = AutocorrelateTransients(allTransients, 50, 200);
        if (candidates.Count == 0) return (0, 0, new List<(double, double)>());

        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Top candidates: {string.Join(", ", candidates.Take(5).Select(c => $"{c.bpm:F1}({c.score:F2})"))}");

        // Step 5: Beat Grid Fitting for top candidates + Saturation Guard
        // Use a composite score: hitRate is primary (more transients aligned = correct BPM),
        // stdDev is secondary (precision of alignment).
        double bestBpm = 0;
        double bestComposite = -1;
        double bestHitRate = 0;
        double bestStdDev = double.MaxValue;
        
        var top5Evaluations = new List<(double bpm, double hitRate, double stdDev, double composite)>();

        foreach (var candidate in candidates.Take(5))
        {
            var (stdDev, hitRate, _) = ScoreBeatGrid(allTransients, candidate.bpm, segmentDuration);

            // Composite score: mas peso a hitRate (critico en audio masterizado)
            double composite = (hitRate * 1.3) - (stdDev * 1.5);
            top5Evaluations.Add((candidate.bpm, hitRate, stdDev, composite));

            LoggerService.Log($"WaveformAnalyzer.GridFit - BPM {candidate.bpm:F1}: stdDev={stdDev:F4}, hitRate={hitRate:F2}, composite={composite:F3}");

            if (composite > bestComposite)
            {
                bestComposite = composite;
                bestHitRate = hitRate;
                bestStdDev = stdDev;
                bestBpm = candidate.bpm;
            }
        }

        // NOTA: La guardia de Grid es compleja porque audio1/audio10 (válido) y audio6/audio11 (ruido)
        // tienen patrones muy similares de candidatos (todos en 185-200 con hitRate 0.85+).
        // No se puede usar solo hitRate + rango como criterio sin regresiones.
        // El verdadero diferenciador está en SpectralFlux: archivos válidos tienen SF > 0.3,
        // archivos con ruido Grid tienen SF < 0.25. Pero ese dato no está disponible aquí.
        // Por ahora, NO aplicamos una guardia en Grid. El problema se resuelve en VoteThreeSources.

        double confidence = Math.Min(1.0, bestHitRate);
        LoggerService.Log($"WaveformAnalyzer.TransientGrid - Best: {bestBpm:F1} BPM (stdDev={bestStdDev:F4}, hitRate={bestHitRate:F2}, composite={bestComposite:F3})");

        // Construir lista de todos los candidatos evaluados para BpmDetector
        var allCandidatesResult = candidates
            .Take(5)
            .Select(c =>
            {
                var (sd, hr, _) = ScoreBeatGrid(allTransients, c.bpm, segmentDuration);
                double comp = (hr * 1.3) - (sd * 1.5);
                return (bpm: c.bpm, score: comp);
            })
            .ToList();

        return (bestBpm, confidence, allCandidatesResult);
    }
}
