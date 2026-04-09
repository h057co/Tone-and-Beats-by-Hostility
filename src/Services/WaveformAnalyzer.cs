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

    public WaveformData Analyze(string filePath, double? globalBpm = null)
    {
        LoggerService.Log($"WaveformAnalyzer.Analyze - Iniciando para: {filePath}");
        
        var (samples, sampleRate) = LoadAudioFile(filePath);
        LoggerService.Log($"WaveformAnalyzer.Analyze - Muestras cargadas: {samples.Length}, SampleRate: {sampleRate}");

        var waveformData = GetWaveformData(samples, 1000);
        LoggerService.Log($"WaveformAnalyzer.Analyze - Waveform data completado");

        // Simplified for performance - skip complex beat detection
        var beatPositions = new List<double>();
        LoggerService.Log($"WaveformAnalyzer.Analyze - Beat positions simplificado");

        var energyData = GetEnergySections(samples, sampleRate);
        LoggerService.Log($"WaveformAnalyzer.Analyze - Energy sections completado");

        var tempoChanges = new List<TempoChange>();
        LoggerService.Log($"WaveformAnalyzer.Analyze - Tempo changes simplificado");

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

    private (float[] samples, int sampleRate) LoadAudioFile(string filePath)
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

            for (int i = 0; i < read; i += channels)
            {
                float sum = 0;
                for (int c = 0; c < channels && i + c < read; c++)
                    sum += buffer[i + c];
                samples.Add(sum / channels);
            }

            return (samples.ToArray(), sampleRate);
        }
    }

    public List<double[]> GetWaveformData(float[] samples, int numPoints)
    {
        var result = new List<double[]>();
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
        var beatPositions = new List<double>();
        var secondsPerBeat = 60.0 / bpm;
        var samplesPerBeat = secondsPerBeat * sampleRate;

        for (double pos = 0; pos < samples.Length; pos += samplesPerBeat)
        {
            beatPositions.Add(pos / sampleRate);
        }

        return beatPositions;
    }

    public List<EnergySection> GetEnergySections(float[] samples, int sampleRate, int windowSeconds = 10)
    {
        var sections = new List<EnergySection>();
        var windowSamples = windowSeconds * sampleRate;
        var numWindows = samples.Length / windowSamples;

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

        var (minBpm, maxBpm) = DetectAdaptiveRange(samples, sampleRate);

        var preprocessed = PreprocessForBeatDetection(samples, sampleRate);

        var (spectralFlux, _) = ComputeOnsetStrength(preprocessed, sampleRate, minBpm, maxBpm);
        var (energyFlux, _) = ComputeEnergyFluxOnset(preprocessed, sampleRate, minBpm, maxBpm);
        var (complexOnset, _) = ComputeComplexDomainOnset(preprocessed, sampleRate, minBpm, maxBpm);

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
        var result = new float[samples.Length];
        int filterLength = sampleRate / 20;
        if (filterLength < 3) filterLength = 3;
        
        var b = new float[filterLength];
        for (int i = 0; i < filterLength; i++)
            b[i] = 1.0f / filterLength;

        for (int i = 0; i < samples.Length; i++)
        {
            double sum = 0;
            for (int j = 0; j < filterLength && i - j >= 0; j++)
            {
                sum += samples[i - j] * b[j];
            }
            result[i] = (float)sum;
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
        double bassEnergy = 0;
        double totalEnergy = 0;
        int bassEnd = sampleRate / 2;
        
        for (int i = 0; i < samples.Length && i < bassEnd; i++)
        {
            bassEnergy += samples[i] * samples[i];
            totalEnergy += samples[i] * samples[i];
        }
        for (int i = bassEnd; i < samples.Length; i++)
            totalEnergy += samples[i] * samples[i];
        
        if (totalEnergy > 0)
            bassEnergy /= totalEnergy;

        double minBpm = 50;
        double maxBpm = 200;

        if (bassEnergy > 0.4)
        {
            minBpm = 55;
            maxBpm = 140;
        }
        else if (bassEnergy > 0.25)
        {
            minBpm = 60;
            maxBpm = 180;
        }
        else
        {
            minBpm = 40;
            maxBpm = 220;
        }

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

        return (Math.Round(bestBpm * 10) / 10, Math.Min(1.0, finalConfidence));
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
}
