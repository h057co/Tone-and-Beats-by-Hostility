using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Interfaces;

public interface IWaveformAnalyzerService
{
    Task<WaveformData> AnalyzeAsync(string filePath, double? globalBpm = null);
    WaveformData Analyze(string filePath, double? globalBpm = null);
}