namespace AudioAnalyzer.Interfaces;

public interface IBpmDetectorService
{
    Task<double> DetectBpmAsync(string filePath, IProgress<int>? progress = null);
    double DetectBpm(string filePath, IProgress<int>? progress = null);
}