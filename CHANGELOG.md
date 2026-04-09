# Changelog - Tone & Beats by Hostility

All notable changes to this project will be documented in this file.

---

## [1.0.4] - 2026-04-09

### Changed
- **BPM Detection Engine:** Complete rewrite using professional-grade dual-band transient analysis
  - Replaced BpmFinder library with SoundTouch.Net (LGPL-2.1) as quick-estimate engine
  - New custom algorithm: Dual-Band Transient Isolation (20-150Hz kick + 2-8kHz snare/hi-hat)
  - Beat Grid Fitting with composite scoring (hitRate + stdDev) over 32-bar segments
  - Autocorrelation on transient positions instead of raw audio signal
  - Intelligent segment selection: skips intro, analyzes post-intro section
  - Snap-to-integer rounding (0.3 BPM threshold) for clean output
  - Half-time normalization for urban/reggaetón (170-200 BPM → 85-100 BPM convention)
- **Performance:** BPM analysis reduced from ~1m48s to ~2-3s (IIR Butterworth filters, downsampling, parallel onset detection)
- **Key Detection:** Optimized to analyze 30s center segment instead of full file
- **Licenses:** Updated LICENSES.md - replaced BpmFinder (MIT) with SoundTouch.Net (LGPL-2.1), removed unused libKeyFinder.NET

### Fixed
- BPM now correctly detects trap/reggaetón/dembow half-time patterns (e.g., 152 BPM instead of ~101)
- Removed aggressive adaptive BPM range capping that excluded tempos above 140 BPM

---

## [1.0.3] - 2026-04-09

### Added
- **iOS Themes:** Two new themes (iOS Light and iOS Dark) with Apple-style colors and button styling
- **About Window Updates:** Added KoFi donation button with message "Invítame a una cosita?" and QR donations image
- **Visual Resize Grabber:** Added triangular grabber indicator in bottom-right corner of MainWindow
- **Documentation:** Complete set of documentation (README.md, LICENSE.md, LICENSES.md, RELEASE.md, CHANGELOG.md)
- **GitHub Repository:** Project published to https://github.com/h057co/Tone-and-Beats-by-Hostility

### Fixed
- **LRA Terminology:** Corrected LRA row to display "LRA" instead of "LUFS" in loudness section
- **Version Sync:** Fixed AssemblyInfo.cs version mismatch with .csproj (1.0.2 → 1.0.3)
- **QR Image Visibility:** Fixed single-file publish to include all embedded assets

### Changed
- **Release Build:** Enabled Release mode optimizations (DebugSymbols=false, Optimize=true)
- **Single-File Publish:** Changed to self-contained single-file executable (~148 MB) for easier distribution
- **Installer:** Updated Inno Setup installer to v1.0.3 with single-file app included (~146 MB)
- **License:** Changed to CC BY-NC-ND 4.0 (Donationware)

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

## [1.0.0] - 2026-04-07

### Added
- Initial release
- Core audio analysis features
- WPF desktop UI

---

*For detailed technical documentation, see DOCUMENTACION.md*
*For build instructions, see scripts/BUILD.md*