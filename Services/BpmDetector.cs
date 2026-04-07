using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Services;
using BpmFinder;

namespace AudioAnalyzer.Services;

public class BpmDetector : IBpmDetectorService
{
    private readonly WaveformAnalyzer _waveformAnalyzer = new();

    public async Task<double> DetectBpmAsync(string filePath, IProgress<int>? progress = null)
    {
        return await Task.Run(() => DetectBpm(filePath, progress));
    }

    public double DetectBpm(string filePath, IProgress<int>? progress = null)
    {
        try
        {
            progress?.Report(10);

            var options = new BpmAnalysisOptions
            {
                MinBpm = 50,
                MaxBpm = 220,
                TrimLeadingSilence = true,
                PreferStableTempo = true
            };

            var bpmFinderResult = BpmAnalyzer.AnalyzeFileAsync(filePath, options).GetAwaiter().GetResult();
            double bpmFinderBpm = bpmFinderResult.Bpm > 0 ? Math.Round(bpmFinderResult.Bpm, 1) : 0;

            progress?.Report(50);

            var advancedResult = GetAdvancedBpm(filePath);

            progress?.Report(90);

            double finalBpm = CombineBpmResults(bpmFinderBpm, advancedResult.bpm, advancedResult.confidence);

            progress?.Report(100);

            return finalBpm;
        }
        catch
        {
            return 0;
        }
    }

    private (double bpm, double confidence) GetAdvancedBpm(string filePath)
    {
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);
            var sampleRate = reader.WaveFormat.SampleRate;
            var samples = new List<float>();
            var buffer = new float[reader.Length / sizeof(float)];
            int read = reader.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < read; i += reader.WaveFormat.Channels)
            {
                float sum = 0;
                for (int c = 0; c < reader.WaveFormat.Channels && i + c < read; c++)
                    sum += buffer[i + c];
                samples.Add(sum / reader.WaveFormat.Channels);
            }

            return _waveformAnalyzer.DetectBpmWithConfidence(samples.ToArray(), sampleRate);
        }
        catch
        {
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
