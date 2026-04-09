using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;

namespace AudioAnalyzer.Services;

/// <summary>
/// Orchestrates the complete audio analysis workflow.
/// Loads audio once, distributes to all analyzers, and refines results.
/// </summary>
public class AudioAnalysisPipeline : IAudioAnalysisPipeline
{
    private readonly IBpmDetectorService _bpmDetector;
    private readonly IKeyDetectorService _keyDetector;
    private readonly IWaveformAnalyzerService _waveformAnalyzer;
    private readonly ILoudnessAnalyzerService _loudnessAnalyzer;

    public AudioAnalysisPipeline(
        IBpmDetectorService bpmDetector,
        IKeyDetectorService keyDetector,
        IWaveformAnalyzerService waveformAnalyzer,
        ILoudnessAnalyzerService loudnessAnalyzer)
    {
        _bpmDetector = bpmDetector ?? throw new ArgumentNullException(nameof(bpmDetector));
        _keyDetector = keyDetector ?? throw new ArgumentNullException(nameof(keyDetector));
        _waveformAnalyzer = waveformAnalyzer ?? throw new ArgumentNullException(nameof(waveformAnalyzer));
        _loudnessAnalyzer = loudnessAnalyzer ?? throw new ArgumentNullException(nameof(loudnessAnalyzer));
    }

    public async Task<AudioAnalysisReport> AnalyzeAudioAsync(string filePath, IProgress<int>? progress = null)
    {
        var report = new AudioAnalysisReport();

        try
        {
            LoggerService.Log($"AudioAnalysisPipeline - Starting analysis: {filePath}");
            progress?.Report(0);

            // === STEP 1: Load audio once ===
            progress?.Report(5);
            LoggerService.Log("AudioAnalysisPipeline - Loading audio data...");
            var audioProvider = new AudioDataProvider();
            var (monoSamples, sampleRate) = await Task.Run(() => audioProvider.LoadMono(filePath));
            LoggerService.Log($"AudioAnalysisPipeline - Audio loaded: {monoSamples.Length} samples @ {sampleRate}Hz");

            progress?.Report(10);

            // === STEP 2: Parallel analysis - BPM, Key, Waveform, Loudness ===
            LoggerService.Log("AudioAnalysisPipeline - Starting parallel detection (BPM, Key, Waveform, Loudness)");
            var bpmProgress = new Progress<int>(p => progress?.Report(10 + p / 4));
            var keyProgress = new Progress<int>(p => progress?.Report(10 + (25 + p / 4)));
            var waveformProgress = new Progress<int>(p => progress?.Report(10 + (50 + p / 4)));
            var loudnessProgress = new Progress<int>(p => progress?.Report(10 + (75 + p / 4)));

            var bpmTask = _bpmDetector.DetectBpmAsync(monoSamples, sampleRate, bpmProgress);
            var keyTask = _keyDetector.DetectKeyAsync(monoSamples, sampleRate, keyProgress);
            var waveformTask = _waveformAnalyzer.AnalyzeAsync(monoSamples, sampleRate, null, waveformProgress);
            var loudnessTask = _loudnessAnalyzer.AnalyzeAsync(filePath, loudnessProgress);

            double bpm = 0;
            string key = "Unknown";
            string mode = "";
            double keyConfidence = 0;
            WaveformData? waveform = null;
            LoudnessResult loudness = new();

            try { bpm = await bpmTask; }
            catch (Exception ex) { LoggerService.Log($"AudioAnalysisPipeline - BPM detection failed: {ex.Message}"); }

            try { (key, mode, keyConfidence) = await keyTask; }
            catch (Exception ex) { LoggerService.Log($"AudioAnalysisPipeline - Key detection failed: {ex.Message}"); }

            try { waveform = await waveformTask; }
            catch (Exception ex) { LoggerService.Log($"AudioAnalysisPipeline - Waveform analysis failed: {ex.Message}"); }

            try { loudness = await loudnessTask; }
            catch (Exception ex) { LoggerService.Log($"AudioAnalysisPipeline - Loudness analysis failed: {ex.Message}"); }

            progress?.Report(90);

            // === STEP 3: Refine waveform with detected BPM ===
            if (bpm > 0 && waveform != null)
            {
                LoggerService.Log($"AudioAnalysisPipeline - Refining waveform with BPM={bpm}");
                try
                {
                    waveform = await _waveformAnalyzer.AnalyzeAsync(monoSamples, sampleRate, bpm);
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"AudioAnalysisPipeline - Waveform refinement failed: {ex.Message}");
                }
            }

            progress?.Report(95);

            // === STEP 4: Build report ===
            report.Bpm = bpm;
            report.Key = key;
            report.Mode = mode;
            report.KeyConfidence = keyConfidence;
            report.Waveform = waveform;
            report.Loudness = loudness;

            LoggerService.Log($"AudioAnalysisPipeline - Analysis complete: BPM={bpm}, Key={key}/{mode}, Valid={report.IsValid}");
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            LoggerService.Log($"AudioAnalysisPipeline - Fatal error: {ex.Message}");
        }

        return report;
    }
}
