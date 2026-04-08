using AudioAnalyzer.Models;

namespace AudioAnalyzer.Interfaces;

public interface ILoudnessAnalyzerService
{
    Task<LoudnessResult> AnalyzeAsync(string filePath, IProgress<int>? progress = null);
    LoudnessResult Analyze(string filePath, IProgress<int>? progress = null);
}