namespace AudioAnalyzer.Interfaces;

public enum BpmRangeProfile
{
    Auto,               // Comportamiento inteligente híbrido por defecto
    Low_50_100,         // 50 a 100 BPM
    Mid_75_150,         // 75 a 150 BPM
    High_100_200,       // 100 a 200 BPM
    VeryHigh_150_300    // 150 a 300 BPM
}

public interface IBpmDetectorService
{
    Task<(double PrimaryBpm, double AlternativeBpm)> DetectBpmAsync(string filePath, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto);
    (double PrimaryBpm, double AlternativeBpm) DetectBpm(string filePath, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto);

    /// <summary>
    /// Detects BPM from pre-loaded mono samples (avoids redundant file I/O).
    /// </summary>
    Task<(double PrimaryBpm, double AlternativeBpm)> DetectBpmAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto);
}
