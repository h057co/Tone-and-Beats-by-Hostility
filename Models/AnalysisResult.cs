namespace AudioAnalyzer.Models;

public class AnalysisResult
{
    public double Bpm { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int DurationMs { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsValid => Bpm > 0 && !string.IsNullOrEmpty(Key);
}