namespace AudioAnalyzer.Interfaces;

public interface IKeyDetectorService
{
    Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(string filePath, IProgress<int>? progress = null);
    (string Key, string Mode, double Confidence) DetectKey(string filePath, IProgress<int>? progress = null);

    /// <summary>
    /// Detects key from pre-loaded mono samples (avoids redundant file I/O).
    /// </summary>
    Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null);
}