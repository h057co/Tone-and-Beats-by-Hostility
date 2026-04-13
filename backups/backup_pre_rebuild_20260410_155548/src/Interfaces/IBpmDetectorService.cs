namespace AudioAnalyzer.Interfaces;

public interface IBpmDetectorService
{
    Task<double> DetectBpmAsync(string filePath, IProgress<int>? progress = null);
    double DetectBpm(string filePath, IProgress<int>? progress = null);

    /// <summary>
    /// Detects BPM from pre-loaded mono samples (avoids redundant file I/O).
    /// </summary>
    Task<double> DetectBpmAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null);
}