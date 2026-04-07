namespace AudioAnalyzer.Interfaces;

public interface IKeyDetectorService
{
    Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(string filePath, IProgress<int>? progress = null);
    (string Key, string Mode, double Confidence) DetectKey(string filePath, IProgress<int>? progress = null);
}