using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Interfaces;

/// <summary>
/// Orchestrates the complete audio analysis workflow.
/// Encapsulates the sequence of BPM → Key → Waveform refinement.
/// Returns a complete analysis report.
/// </summary>
public interface IAudioAnalysisPipeline
{
    /// <summary>
    /// Analyzes audio file end-to-end (BPM, Key, Waveform, Loudness).
    /// Handles the orchestration logic, including waveform re-analysis with detected BPM.
    /// </summary>
    Task<AudioAnalysisReport> AnalyzeAudioAsync(string filePath, IProgress<int>? progress = null, BpmRangeProfile profile = BpmRangeProfile.Auto);
}

/// <summary>
/// Complete analysis result from the pipeline.
/// </summary>
public class AudioAnalysisReport
{
    public double Bpm { get; set; }
    public double AlternativeBpm { get; set; }
    public string Key { get; set; } = "Unknown";
    public string Mode { get; set; } = "";
    public double KeyConfidence { get; set; }
    public WaveformData? Waveform { get; set; }
    public LoudnessResult Loudness { get; set; } = new();

    public bool IsValid => Bpm > 0 && Key != "Unknown";
}
