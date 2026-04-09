# Changelog - Tone & Beats by Hostility

All notable changes to this project will be documented in this file.

## [1.0.3] - 2026-04-09

### Added
- **iOS Themes:** Two new themes (iOS Light and iOS Dark) with Apple-style colors and buttons
- **About Window Updates:** Added KoFi donation button with message "Invítame a una cosita?" and QR donations image
- **Visual Resize Grabber:** Added triangular grabber indicator in bottom-right corner of MainWindow

### Fixed
- **LRA Terminology:** Corrected LRA row to display "LRA" instead of "LUFS" in loudness section
- **Version Sync:** Fixed AssemblyInfo.cs version mismatch with .csproj (1.0.2 → 1.0.3)

### Changed
- **Release Build:** Enabled Release mode optimizations (DebugSymbols=false, Optimize=true)
- **Single-File Publish:** Changed to self-contained single-file executable for easier distribution
- **Installer:** Updated installer to v1.0.3 with single-file app included

### Technical
- **Static Audit Resolved:** All 6 findings from previous audit addressed
  - FFT duplicated code → FftHelper shared class
  - Empty catch blocks → Logging via LoggerService
  - GetAwaiter().GetResult() blocking → Native async/await
  - List without capacity → Pre-allocated capacity
  - ViewModel-WPF coupling → Semantic states + Converter

---

## [1.0.2] - 2026-04-08

### Added
- **OGG Support:** Added NAudio.Vorbis for OGG audio format support
- **Themes:** Dark, Light, Blue themes with full color scheme
- **Audio Analysis:** BPM, Key, Waveform, Loudness (LUFS/LRA/TruePeak) detection

---

## [1.0.0] - 2026-04-XX

### Added
- Initial release
- Core audio analysis features
- WPF desktop UI