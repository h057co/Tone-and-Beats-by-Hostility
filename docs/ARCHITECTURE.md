# Tone & Beats by Hostility - Architecture Documentation

**Version:** 1.0.6  
**Date:** 2026-04-11  
**Framework:** .NET 8.0 + WPF  
**Status:** Production (Donationware)  
**License:** CC BY-NC-ND 4.0

---

## 1. Project Overview

**Tone & Beats by Hostility** is a Windows desktop application for audio analysis that automatically detects:
- **BPM (Tempo):** Automatic tempo detection with tresillo pattern support
- **Key (Musical Key):** Key identification using Camelot Wheel notation
- **Waveform:** Graphical audio waveform visualization
- **Loudness:** LUFS, LRA, and True Peak (dBTP) measurement

### Target Audience
- DJs
- Music Producers
- Beatmakers
- Audio professionals

---

## 2. Project Structure

```
src/
├── App.xaml / App.xaml.cs               # WPF entry point, DI container
├── MainWindow.xaml / .cs                # Main application window
├── AboutWindow.xaml / .cs               # About dialog
├── AssemblyInfo.cs                      # Assembly metadata
│
├── Assets/                              # Graphic resources
│   ├── HOST_BLANCO.png                  # White logo (Dark/Blue themes)
│   ├── HOST_NEGRO.png                   # Black logo (Light theme)
│   ├── qrdonaciones.png                 # Donation QR code
│   └── HOSTBLANCO.ico                   # Application icon
│
├── Controls/                            # Custom WPF controls
│   └── WaveformControl.xaml / .cs      # Waveform visualizer
│
├── Services/                            # Business logic
│   ├── AudioAnalysisPipeline.cs         # Analysis orchestration
│   ├── AudioDataProvider.cs             # Centralized audio loading
│   ├── AudioPlayerService.cs            # Audio playback (NAudio)
│   ├── AudioReaderFactory.cs            # Format-aware audio reader
│   ├── BpmDetector.cs                   # Hybrid BPM detection
│   ├── FftHelper.cs                     # Shared FFT utilities
│   ├── KeyDetector.cs                   # Key detection (Krumhansl)
│   ├── LoudnessAnalyzer.cs              # LUFS/LRA analysis (FFmpeg)
│   ├── MetadataWriter.cs                # Metadata writing (TagLib#)
│   ├── WaveformAnalyzer.cs              # Waveform analysis
│   └── LoggerService.cs                 # Logging system
│
├── ViewModels/                          # MVVM pattern
│   └── MainViewModel.cs                 # Main window ViewModel
│
├── Models/                              # Data models
│   ├── AudioFileInfo.cs                 # Audio file metadata
│   ├── LoudnessResult.cs                # Loudness analysis result
│   └── WaveformData.cs                  # Waveform data points
│
├── Themes/                              # Visual themes
│   ├── DarkTheme.xaml                   # Dark theme
│   ├── LightTheme.xaml                  # Light theme
│   ├── BlueTheme.xaml                   # Blue accent theme
│   ├── IosLightTheme.xaml               # iOS Light style
│   ├── IosDarkTheme.xaml                # iOS Dark style
│   └── ThemeManager.cs                  # Theme switching logic
│
├── Infrastructure/                      # Support classes
│   ├── BoolToVisibilityConverter.cs     # Bool → Visibility
│   ├── CornerResizeBehavior.cs          # Corner-only resize
│   ├── FilePickerService.cs             # File dialog service
│   ├── LevelToBrushConverter.cs         # Level → Color brush
│   ├── MessageBoxService.cs             # MessageBox abstraction
│   ├── StringToVisibilityConverter.cs   # String → Visibility
│   └── ViewModelBase.cs                 # INotifyPropertyChanged base
│
├── Interfaces/                          # Service contracts
│   ├── IAudioAnalysisPipeline.cs
│   ├── IAudioPlayerService.cs
│   ├── IBpmDetectorService.cs
│   ├── IFilePickerService.cs
│   ├── IKeyDetectorService.cs
│   ├── ILoudnessAnalyzerService.cs
│   ├── IMessageBoxService.cs
│   └── IWaveformAnalyzerService.cs
│
├── Commands/                            # MVVM commands
│   └── RelayCommand.cs                  # ICommand implementation
│
└── Helpers/                             # Utility helpers
    └── EmbeddedResourceHelper.cs        # Embedded resource loading
```

---

## 3. Architecture Pattern

### MVVM (Model-View-ViewModel)

The application follows the **MVVM pattern** strictly:

- **Models:** Pure data classes (`AudioFileInfo`, `LoudnessResult`, `WaveformData`)
- **Views:** XAML UI (`MainWindow`, `AboutWindow`, `WaveformControl`)
- **ViewModels:** Business logic + state (`MainViewModel`)

### Service-Oriented Design

Services are instantiated directly in `App.xaml.cs` (manual DI, not a container):

```
MainViewModel (receives via constructor)
├── IAudioPlayerService → AudioPlayerService
├── IBpmDetectorService → BpmDetector
├── IKeyDetectorService → KeyDetector
├── IWaveformAnalyzerService → WaveformAnalyzer
├── IFilePickerService → FilePickerService
├── IMessageBoxService → MessageBoxService
├── ILoudnessAnalyzerService → LoudnessAnalyzer
└── MetadataWriter (direct instantiation)

Note: IAudioAnalysisPipeline exists but is NOT injected into MainViewModel.
      It is created in App.xaml.cs but unused by the current UI flow.
```

---

## 4. Analysis Pipeline

### Audio Analysis Flow

```
User drops/selects file
         ↓
AudioDataProvider.LoadAudio()
         ↓
    ┌────┴────┐
    ↓         ↓
Mono samples   Metadata
(PCM float[])  (MediaInfo)
    ↓
┌───┴───┬──────┴──────┐
↓       ↓       ↓       ↓
BPM    Key    Waveform  Loudness
Task   Task   Task      Task
└───────┴──────┴──────┘
         ↓
   Task.WhenAll()
         ↓
   AnalysisReport
         ↓
   UI Update
```

### Key Design: Centralized I/O

**Problem (v1.0.3):** Each analyzer opened the file independently (4x I/O)

**Solution (v1.0.4+):** `AudioDataProvider` loads once, shares samples

```
AudioDataProvider.LoadAudio(filePath)
├── AudioReaderFactory.CreateReader()
├── Decode to float[] mono samples
├── Calculate sampleRate
└── Return AudioData object

Analyzers receive (float[] samples, int sampleRate)
```

---

## 5. Dependencies

| Package | Version | Purpose | License |
|---------|---------|---------|---------|
| NAudio | 2.2.1 | Audio playback & analysis | Ms-PL |
| NAudio.Vorbis | 1.5.0 | OGG format support | Ms-PL |
| FFMpegCore | 5.1.0 | Loudness analysis (LUFS) | MIT |
| MediaInfo.Wrapper.Core | 26.1.0 | Audio metadata extraction | BSD-2-Clause |
| TagLibSharp | 2.3.0 | Metadata read/write | LGPL v2.1 |
| SoundTouch.Net | 2.3.2 | BPM detection & audio processing | LGPL v2.1 |

**Note:** BpmFinder package was removed in v1.0.4+ - BPM detection now uses SoundTouch.Net exclusively.

---

## 6. Analysis Algorithms

**Note (2026-04-12):** Magic numbers in BPM detection have been extracted to `BpmConstants.cs` for better maintainability. Constants like `TRESILLO_RATIO`, `HIGH_CONFIDENCE_THRESHOLD`, `TRAP_CORRECTION_MULTIPLIER` are now named constants instead of hardcoded values.

### 6.1 BPM Detection (SoundTouch-based)

**Library:** SoundTouch.Net v2.3.2

**Approach:** Multi-strategy detection with tresillo support (v1.0.6)

1. **SoundTouch Quick BPM Estimate**
   - Uses `SoundTouch.ProfileType.BPM` for fast estimation
   - Applied directly to mono samples

2. **Transient-Based Analysis**
   - High-pass filter applied to isolate transients
   - `WaveformAnalyzer.DetectBpmByTransientGrid()` for detailed analysis
   - Beat grid fitting for rhythmic patterns

3. **Harmonic Ratio Detection (Tresillo)**
   - Ratio 1.5x correction for reggaetón/pop patterns
   - Ratio 0.667x correction for half-time patterns
   - Threshold: 0.08 for ratio matching
   - Trap Masterizado heuristic (corrects 101.4 → 76 BPM)

4. **Confidence-Based Selection**
   - High confidence override (>0.85) in TransientGrid
   - Threshold 0.15 minimum confidence
   - Disagreement threshold: 0.65 for BPM conflicts

5. **Range Profiles**
   - `BpmRangeProfile` enum: Auto, Low_50_100, Mid_75_150, High_100_200, VeryHigh_150_300
   - NormalizeTempoRange() for profile-based filtering

6. **Alternative BPM Display**
   - Returns `(PrimaryBpm, AlternativeBpm)` tuple
   - CalculateAlternativeBpm() for display purposes

### 6.2 Key Detection

**Algorithm:** Krumhansl-Schmuckler (custom implementation)

- Pitch Class Profile (PCP) calculation
- FFT: 16384 samples, A4=440Hz
- 8 harmonics per pitch class
- Correlation with Major/Minor templates
- Returns: Key + Mode + Confidence

### 6.3 Waveform Analysis

1. Downsample to 1000 points
2. Calculate min/max per window
3. Render using WPF Path
4. Beat grid overlay based on BPM

### 6.4 Loudness Analysis

Uses **FFmpeg** `loudnorm` filter:
- LUFS Integrated
- LRA (Loudness Range)
- True Peak (dBTP)

---

## 7. Themes System

### Available Themes

| Theme | Background | Accent | Use Case |
|-------|------------|--------|---------|
| Dark | #1E1E1E | #007ACC | Default dark |
| Light | #F5F5F5 | #0078D4 | Light environments |
| Blue | #1A1A2E | #00B4D8 | Blue accent |
| iOS Light | #F2F2F7 | #007AFF | Apple style light |
| iOS Dark | #000000 | #0A84FF | Apple style dark |

### Theme Switching

```csharp
ThemeManager.ApplyTheme(string themeName)
  → ResourceDictionary["CurrentTheme"] = themeName
  → Button styles, colors, logos update
```

---

## 8. User Interface

### Main Window Layout

```
┌─────────────────────────────────┐
│ [Logo] [Title]           [🎨] │  ← Row 0: Header + Theme Toggle
├─────────────────────────────────┤
│ [BPM] [KEY] [FORMAT] [DURATION]│  ← Row 1: File Info
├─────────────────────────────────┤
│ [═══════ WAVEFORM ═══════════] │  ← Row 2: Waveform Visualizer
│ [▶ PLAY] [━━━━━━━━━] [⏸] [⏹]  │  ← Row 3: Playback Controls
├─────────────────────────────────┤
│ Integrated: -14.2 LUFS          │
│ LRA: 12.3 LRA  TruePeak: -1.2  │  ← Row 4: Loudness
├─────────────────────────────────┤
│ [🔍 Analyze Audio]    [💾 Save] │  ← Row 5: Actions
├─────────────────────────────────┤
│ BPM: [176.0] [▼ BPM Range ▼]   │
│ Key: [8A] [Relative]           │  ← Row 6: Analysis Results
└─────────────────────────────────┘
```

### Resize Behavior

- **Diagonal resize only** (corners)
- **Proportional scaling** via Viewbox
- **Min:** 350×500 | **Max:** 600×750

---

## 9. Build & Distribution

### Build Commands

```bash
# Development
cd src
dotnet build -c Debug

# Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish-v1.0.6

# Installer
iscc installer/setup.iss
```

### Output Artifacts

| Artifact | Location | Size |
|----------|----------|------|
| Single-file exe | `publish-v1.0.6/` | ~370 MB |
| Installer | `installer/` | (generated via Inno Setup) |

**Note:** Actual installer size depends on build configuration and compression.

---

## 10. Version History

| Version | Date | Highlights |
|---------|------|------------|
| 1.0.6 | 2026-04-11 | Tresillo BPM detection, Alternative BPM display |
| 1.0.5 | 2026-04-09 | Theme-aware waveform, iOS themes, clickable links |
| 1.0.4 | 2026-04-09 | I/O centralization, pipeline pattern, GC optimization |
| 1.0.3 | 2026-04-09 | iOS themes, KoFi donation, LUFS module |
| 1.0.2 | 2026-04-08 | OGG support, Dark/Light/Blue themes |
| 1.0.0 | 2026-04-07 | Initial release |

---

## 11. Contributing

See `CONTRIBUTING.md` for development guidelines.

---

*Documentation updated: 2026-04-12 (refactoring: BpmConstants, Butterworth filter consolidation)*  
*Developed by: Luis Jiménez (Hostility) - Medellín, Colombia*  
*Contact: info@hostilitymusic.com*  
*Web: www.hostilitymusic.com*  
*Repository: https://github.com/h057co/Tone-and-Beats-by-Hostility*
