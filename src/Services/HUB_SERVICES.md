# Audio Analysis Services Hub

This hub contains the core audio processing logic for **Tone & Beats**.

## Detection Modules
- [[BpmDetector.cs]]: Hybrid BPM detection using SoundTouch, TransientGrid, and SpectralFlux.
- [[KeyDetector.cs]]: Key detection using Krumhansl-Schmuckler algorithm.
- [[LoudnessAnalyzer.cs]]: LUFS, LRA, and True Peak analysis via FFmpeg.
- [[WaveformAnalyzer.cs]]: Audio downsampling and visual data generation.

## Orchestration
- [[AudioAnalysisPipeline.cs]]: Coordinates all detectors to run in parallel.
- [[AudioDataProvider.cs]]: Loads and prepares mono audio samples for analysis.

## Supporting Constants
- [[BpmConstants.cs]]: Thresholds and ratios for BPM logic.
- [[DspConstants.cs]]: FFT sizes and windowing constants.
- [[FftHelper.cs]]: Shared FFT implementation.

## Related Documentation
- [[../../docs/BPM_Detection_Module_Report.md|BPM Detection Report]]
- [[../../docs/Key_Detection_Module_Report.md|Key Detection Report]]
- [[../../docs/ARCHITECTURE.md|Project Architecture]]
