using AudioAnalyzer.Interfaces;

namespace AudioAnalyzer.Services;

public class KeyDetector : IKeyDetectorService
{
    private static readonly double[] MajorProfile = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
    private static readonly double[] MinorProfile = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public async Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(string filePath, IProgress<int>? progress = null)
    {
        return await Task.Run(() => DetectKey(filePath, progress));
    }

    public async Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        return await Task.Run(() => DetectKeyFromSamples(monoSamples, sampleRate, progress));
    }

    public (string Key, string Mode, double Confidence) DetectKey(string filePath, IProgress<int>? progress = null)
    {
        var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
        return DetectKeyFromSamples(monoSamples, sampleRate, progress);
    }

    /// <summary>
    /// Core key detection logic operating on pre-loaded mono samples.
    /// No file I/O occurs in this method.
    /// </summary>
    private (string Key, string Mode, double Confidence) DetectKeyFromSamples(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        const int MaxAnalysisSeconds = 30;

        try
        {
            if (monoSamples.Length < sampleRate)
                return ("Unknown", "Unknown", 0);

            // Limit to MaxAnalysisSeconds from center of audio for key detection
            int maxSamples = MaxAnalysisSeconds * sampleRate;
            float[] analysisData;
            if (monoSamples.Length > maxSamples)
            {
                int startOffset = (monoSamples.Length - maxSamples) / 2;
                analysisData = monoSamples.AsSpan(startOffset, maxSamples).ToArray();
                LoggerService.Log($"KeyDetector.DetectKeyFromSamples - Trimmed {monoSamples.Length} -> {maxSamples} samples (center segment)");
            }
            else
            {
                analysisData = monoSamples;
            }

            progress?.Report(20);

            var pcp = ComputePitchClassProfile(analysisData, sampleRate);
            
            progress?.Report(50);

            var (keyIndex, mode, correlation) = FindBestKey(pcp);
            
            progress?.Report(100);

            return (NoteNames[keyIndex], mode == 0 ? "Major" : "Minor", correlation);
        }
        catch (Exception ex)
        {
            LoggerService.Log($"KeyDetector.DetectKeyFromSamples - Error: {ex.Message}");
            return ("Error", "Error", 0);
        }
    }

    private double[] ComputePitchClassProfile(float[] samples, int sampleRate)
    {
        const int fftSize = DspConstants.FFT_SIZE_KEY_DETECTION;
        const int hopSize = 8192;
        const int numBins = 12;
        const double a4Freq = 440.0;

        var pcp = new double[numBins];
        int numFrames = (samples.Length - fftSize) / hopSize + 1;
        if (numFrames <= 0) numFrames = 1;

        var magnitudes = new double[fftSize / 2];

        for (int frame = 0; frame < numFrames; frame++)
        {
            var frameStart = frame * hopSize;
            if (frameStart + fftSize > samples.Length) break;

            Array.Clear(magnitudes, 0, magnitudes.Length);

            var window = samples.AsSpan(frameStart, fftSize);
            ComputeFFTMagnitudes(window, magnitudes, sampleRate);

            for (int pitchClass = 0; pitchClass < numBins; pitchClass++)
            {
                double pitchEnergy = 0;
                double c0Freq = a4Freq * Math.Pow(2, (pitchClass - 9) / 12.0);

                for (int harmonic = 1; harmonic <= 8; harmonic++)
                {
                    double harmonicFreq = c0Freq * harmonic;
                    if (harmonicFreq > sampleRate / 2) break;

                    int freqBin = (int)(harmonicFreq * fftSize / sampleRate);
                    if (freqBin > 0 && freqBin < magnitudes.Length - 1)
                    {
                        double interpolatedMag = magnitudes[freqBin] + 0.5 * (magnitudes[freqBin - 1] + magnitudes[freqBin + 1]);
                        pitchEnergy += interpolatedMag / harmonic;
                    }
                }
                pcp[pitchClass] += pitchEnergy;
            }
        }

        double totalEnergy = pcp.Sum();
        if (totalEnergy > 0)
        {
            for (int i = 0; i < numBins; i++)
                pcp[i] /= totalEnergy;
        }

        return pcp;
    }

    private void ComputeFFTMagnitudes(Span<float> window, double[] magnitudes, int sampleRate)
    {
        int n = magnitudes.Length * 2;
        var complex = new System.Numerics.Complex[n];

        for (int i = 0; i < n; i++)
        {
            double hann = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            complex[i] = new System.Numerics.Complex(i < window.Length ? window[i] * hann : 0, 0);
        }

        FFT(complex);

        for (int i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = Math.Sqrt(complex[i].Real * complex[i].Real + complex[i].Imaginary * complex[i].Imaginary);
        }
    }

    private void FFT(System.Numerics.Complex[] data) => FftHelper.FFT(data);

    private (int keyIndex, int mode, double correlation) FindBestKey(double[] pcp)
    {
        double bestCorrelation = -1;
        int bestKey = 0;
        int bestMode = 0;

        for (int root = 0; root < 12; root++)
        {
            var rotatedPcp = RotateArray(pcp, root);

            double majorCorr = ComputeCorrelation(rotatedPcp, MajorProfile);
            if (majorCorr > bestCorrelation)
            {
                bestCorrelation = majorCorr;
                bestKey = root;
                bestMode = 0;
            }

            double minorCorr = ComputeCorrelation(rotatedPcp, MinorProfile);
            if (minorCorr > bestCorrelation)
            {
                bestCorrelation = minorCorr;
                bestKey = root;
                bestMode = 1;
            }
        }

        double normalizedConfidence = Math.Min(1.0, Math.Max(0, (bestCorrelation + 1) / 2));
        return (bestKey, bestMode, normalizedConfidence);
    }

    private double[] RotateArray(double[] arr, int shift)
    {
        var result = new double[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            result[i] = arr[(i + shift) % arr.Length];
        return result;
    }

    private double ComputeCorrelation(double[] a, double[] b)
    {
        double sumAB = 0, sumA2 = 0, sumB2 = 0;
        for (int i = 0; i < a.Length; i++)
        {
            sumAB += a[i] * b[i];
            sumA2 += a[i] * a[i];
            sumB2 += b[i] * b[i];
        }

        double denominator = Math.Sqrt(sumA2) * Math.Sqrt(sumB2);
        return denominator > 0 ? sumAB / denominator : 0;
    }
}