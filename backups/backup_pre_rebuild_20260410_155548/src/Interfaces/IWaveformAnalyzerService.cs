using AudioAnalyzer.Models;

namespace AudioAnalyzer.Interfaces;

public interface IWaveformAnalyzerService
{
    Task<WaveformData> AnalyzeAsync(string filePath, double? globalBpm = null);
    WaveformData Analyze(string filePath, double? globalBpm = null);

    /// <summary>
    /// Analyzes waveform from pre-loaded mono samples (avoids redundant file I/O).
    /// </summary>
    Task<WaveformData> AnalyzeAsync(float[] monoSamples, int sampleRate, double? globalBpm = null, IProgress<int>? progress = null);
}