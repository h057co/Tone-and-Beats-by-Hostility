using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using AudioAnalyzer.Commands;
using AudioAnalyzer.Infrastructure;
using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;
using AudioAnalyzer.Services;
using AudioAnalyzer.Themes;

namespace AudioAnalyzer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IBpmDetectorService _bpmDetectorService;
    private readonly IKeyDetectorService _keyDetectorService;
    private readonly IWaveformAnalyzerService _waveformAnalyzerService;
    private readonly IFilePickerService _filePickerService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILoudnessAnalyzerService _loudnessAnalyzerService;
    private readonly MetadataWriter _metadataWriter;

    private string _fileName = "No file selected";
    private bool _isFileSelected = false;
    private string _positionText = "00:00";
    private string _durationText = "00:00";
    private string _bpmText = "--";
    private string _alternativeBpmText = "";
    private string _keyText = "--";
    private string _modeText = "";
    private string _bpmConfidence = "";
    private string _keyConfidence = "";
    private string _statusText = "Ready";
    private string _statusState = "Normal";
    private double _analysisProgress;
    private bool _isAnalysisProgressVisible;
    private bool _arePlaybackControlsEnabled;
    private bool _isAnalyzeButtonEnabled;
    private bool _isSaveMetadataEnabled;
    private bool _isAnalyzingInProgress = false;
    private string? _pendingFilePath = null;
    private WaveformData? _waveformData;
    private RelayCommand? _browseCommand;
    private RelayCommand? _playCommand;
    private RelayCommand? _pauseCommand;
    private RelayCommand? _stopCommand;
    private RelayCommand? _saveMetadataCommand;
    private RelayCommand? _analyzeCommand;
    private RelayCommand? _cycleThemeCommand;
    private RelayCommand? _openUrlCommand;
    private double _originalBpm;
    private double _displayBpm;
    private bool _bpmAdjusted;
    private string _bpmModifierText = "";
    private BpmRangeProfile _selectedBpmProfile = BpmRangeProfile.Auto;
    private string _audioFileType = "";
    private string _sampleRateText = "";
    private string _bitDepthText = "";
    private string _channelsText = "";
    private string _bitrateText = "";
    private string _bitrateModeText = "";
    private AudioFileInfo? _currentAudioInfo;
    private int _keyIndex = -1;
    private bool _showRelativeKey = false;
    private LoudnessResult? _loudnessResult;
    private bool _isLoudnessVisible = false;

    public MainViewModel(
        IAudioPlayerService audioPlayerService,
        IBpmDetectorService bpmDetectorService,
        IKeyDetectorService keyDetectorService,
        IWaveformAnalyzerService waveformAnalyzerService,
        IFilePickerService filePickerService,
        IMessageBoxService messageBoxService,
        ILoudnessAnalyzerService loudnessAnalyzerService)
    {
        _audioPlayerService = audioPlayerService;
        _bpmDetectorService = bpmDetectorService;
        _keyDetectorService = keyDetectorService;
        _waveformAnalyzerService = waveformAnalyzerService;
        _filePickerService = filePickerService;
        _messageBoxService = messageBoxService;
        _loudnessAnalyzerService = loudnessAnalyzerService;
        _metadataWriter = new MetadataWriter();

        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    /// <summary>
    /// Semantic flag: true when a valid audio file is loaded.
    /// The View uses this with DataTrigger to switch FileNameForeground color.
    /// </summary>
    public bool IsFileSelected
    {
        get => _isFileSelected;
        set => SetProperty(ref _isFileSelected, value);
    }

    public string PositionText
    {
        get => _positionText;
        set => SetProperty(ref _positionText, value);
    }

    public string DurationText
    {
        get => _durationText;
        set => SetProperty(ref _durationText, value);
    }

    public string AudioFileType
    {
        get => _audioFileType;
        set => SetProperty(ref _audioFileType, value);
    }

    public string SampleRateText
    {
        get => _sampleRateText;
        set => SetProperty(ref _sampleRateText, value);
    }

    public string BitDepthText
    {
        get => _bitDepthText;
        set => SetProperty(ref _bitDepthText, value);
    }

    public string ChannelsText
    {
        get => _channelsText;
        set => SetProperty(ref _channelsText, value);
    }

    public string BitrateText
    {
        get => _bitrateText;
        set => SetProperty(ref _bitrateText, value);
    }

    public string BitrateModeText
    {
        get => _bitrateModeText;
        set => SetProperty(ref _bitrateModeText, value);
    }

    public string AudioInfoSummary => BuildAudioInfoSummary();

    private string BuildAudioInfoSummary()
    {
        if (_currentAudioInfo == null)
            return "No file loaded";

        var parts = new List<string>();

        parts.Add(_currentAudioInfo.FileType ?? "Unknown");

        if (_currentAudioInfo.SampleRate > 0)
            parts.Add($"{_currentAudioInfo.SampleRate} Hz");

        if (_currentAudioInfo.BitDepth > 0)
            parts.Add($"{_currentAudioInfo.BitDepth}-bit");

        if (_currentAudioInfo.Bitrate > 0)
        {
            var bitrateInfo = $"{_currentAudioInfo.Bitrate} kbps";
            if (!string.IsNullOrEmpty(_currentAudioInfo.BitrateMode))
                bitrateInfo += $" {_currentAudioInfo.BitrateMode}";
            parts.Add(bitrateInfo);
        }

        if (_currentAudioInfo.Channels > 0)
            parts.Add(_currentAudioInfo.ChannelsDisplay);

        return string.Join(" • ", parts);
    }

    public string BpmText
    {
        get => _bpmText;
        set => SetProperty(ref _bpmText, value);
    }

    public string BpmDisplayText
    {
        get
        {
            if (_originalBpm <= 0) return _bpmText;
            string suffix = _bpmModifierText;
            return $"{_displayBpm.ToString("F1")}{suffix}";
        }
    }

    public string AlternativeBpmText
    {
        get => _alternativeBpmText;
        set => SetProperty(ref _alternativeBpmText, value);
    }

    public Dictionary<BpmRangeProfile, string> AvailableBpmProfiles { get; } = new Dictionary<BpmRangeProfile, string>
    {
        { BpmRangeProfile.Auto, "Auto (Recomendado)" },
        { BpmRangeProfile.Low_50_100, "Low (50 - 100 BPM)" },
        { BpmRangeProfile.Mid_75_150, "Mid (75 - 150 BPM)" },
        { BpmRangeProfile.High_100_200, "High (100 - 200 BPM)" },
        { BpmRangeProfile.VeryHigh_150_300, "Very High (150 - 300 BPM)" }
    };

    public BpmRangeProfile SelectedBpmProfile
    {
        get => _selectedBpmProfile;
        set => SetProperty(ref _selectedBpmProfile, value);
    }

    /// <summary>
    /// Semantic flag: true when BPM has been adjusted (×2 or ÷2).
    /// The View uses this with DataTrigger to switch between TitleBrush and BpmModifiedBrush.
    /// </summary>
    public bool IsBpmModified => !string.IsNullOrEmpty(_bpmModifierText);

    public string KeyText
    {
        get => _keyText;
        set => SetProperty(ref _keyText, value);
    }

    public string ModeText
    {
        get => _modeText;
        set => SetProperty(ref _modeText, value);
    }

    public string BpmConfidence
    {
        get => _bpmConfidence;
        set => SetProperty(ref _bpmConfidence, value);
    }

    public string KeyConfidence
    {
        get => _keyConfidence;
        set => SetProperty(ref _keyConfidence, value);
    }

    public LoudnessResult? LoudnessResult
    {
        get => _loudnessResult;
        set
        {
            SetProperty(ref _loudnessResult, value);
            OnPropertyChanged(nameof(LoudnessIntegratedDisplay));
            OnPropertyChanged(nameof(LoudnessShortTermDisplay));
            OnPropertyChanged(nameof(LoudnessLraDisplay));
            OnPropertyChanged(nameof(LoudnessTruePeakDisplay));
            OnPropertyChanged(nameof(LoudnessIntegratedLevel));
            OnPropertyChanged(nameof(LoudnessTruePeakLevel));
        }
    }

    public bool IsLoudnessVisible
    {
        get => _isLoudnessVisible;
        set => SetProperty(ref _isLoudnessVisible, value);
    }

    public string LoudnessIntegratedDisplay => _loudnessResult?.IntegratedDisplay ?? "--";
    public string LoudnessShortTermDisplay => _loudnessResult?.ShortTermDisplay ?? "--";
    public string LoudnessLraDisplay => _loudnessResult?.ShortTermDisplay ?? "--";
    public string LoudnessTruePeakDisplay => _loudnessResult?.TruePeakDisplay ?? "--";

    /// <summary>
    /// Semantic level for LUFS Integrated: "Good", "Warning", "Danger", or "None".
    /// The View uses LevelToBrushConverter to map this to the appropriate color.
    /// </summary>
    public string LoudnessIntegratedLevel
    {
        get
        {
            if (_loudnessResult == null || !_loudnessResult.IsValid)
                return "None";

            if (_loudnessResult.IntegratedLufs >= -12)
                return "Danger";   // Too loud

            if (_loudnessResult.IntegratedLufs >= -16)
                return "Warning";  // Caution

            return "Good";         // OK
        }
    }

    /// <summary>
    /// Semantic level for True Peak: "Good", "Warning", "Danger", or "None".
    /// The View uses LevelToBrushConverter to map this to the appropriate color.
    /// </summary>
    public string LoudnessTruePeakLevel
    {
        get
        {
            if (_loudnessResult == null || _loudnessResult.TruePeak == 0)
                return "None";

            if (_loudnessResult.TruePeak >= 0)
                return "Danger";   // Clipping

            if (_loudnessResult.TruePeak > -1)
                return "Warning";  // Close to clipping

            return "Good";         // OK
        }
    }

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public string KeyDisplayText
    {
        get
        {
            if (string.IsNullOrEmpty(_keyText) || _keyText == "--")
                return "--";
            if (_showRelativeKey)
                return CalculateRelativeKey();
            return $"{_keyText} {_modeText}";
        }
    }

    private string CalculateRelativeKey()
    {
        if (_keyIndex < 0 || string.IsNullOrEmpty(_modeText))
            return "--";

        int relativeIndex;
        string relativeMode;

        if (_modeText == "Major")
        {
            relativeIndex = (_keyIndex - 3 + 12) % 12;
            relativeMode = "m";
        }
        else
        {
            relativeIndex = (_keyIndex + 3) % 12;
            relativeMode = "M";
        }

        return $"{NoteNames[relativeIndex]}{relativeMode}";
    }

    public void ToggleKeyDisplay()
    {
        if (string.IsNullOrEmpty(_keyText) || _keyText == "--") return;
        _showRelativeKey = !_showRelativeKey;
        OnPropertyChanged(nameof(KeyDisplayText));
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Semantic state: "Normal", "Success", or "Error".
    /// The View uses DataTriggers to map these to StatusForegroundBrush, StatusSuccessBrush, StatusErrorBrush.
    /// </summary>
    public string StatusState
    {
        get => _statusState;
        set => SetProperty(ref _statusState, value);
    }

    public double AnalysisProgress
    {
        get => _analysisProgress;
        set => SetProperty(ref _analysisProgress, value);
    }

    public bool IsAnalysisProgressVisible
    {
        get => _isAnalysisProgressVisible;
        set => SetProperty(ref _isAnalysisProgressVisible, value);
    }

    public bool ArePlaybackControlsEnabled
    {
        get => _arePlaybackControlsEnabled;
        set
        {
            if (SetProperty(ref _arePlaybackControlsEnabled, value))
            {
                PlayCommand.RaiseCanExecuteChanged();
                PauseCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsAnalyzeButtonEnabled
    {
        get => _isAnalyzeButtonEnabled;
        set => SetProperty(ref _isAnalyzeButtonEnabled, value);
    }

    private double _waveformPosition;
    public double WaveformPosition
    {
        get => _waveformPosition;
        set => SetProperty(ref _waveformPosition, value);
    }

    public WaveformData? WaveformData
    {
        get => _waveformData;
        set => SetProperty(ref _waveformData, value);
    }

    public string? FilePath { get; private set; }

    public RelayCommand BrowseCommand
    {
        get => _browseCommand ??= new RelayCommand(ExecuteBrowse, () => true);
        private set => _browseCommand = value;
    }
    public RelayCommand PlayCommand
    {
        get => _playCommand ??= new RelayCommand(ExecutePlay, () => ArePlaybackControlsEnabled);
        private set => _playCommand = value;
    }
    public RelayCommand PauseCommand
    {
        get => _pauseCommand ??= new RelayCommand(ExecutePause, () => ArePlaybackControlsEnabled);
        private set => _pauseCommand = value;
    }
    public RelayCommand StopCommand
    {
        get => _stopCommand ??= new RelayCommand(ExecuteStop, () => ArePlaybackControlsEnabled);
        private set => _stopCommand = value;
    }
    public RelayCommand AnalyzeCommand 
    {
        get => _analyzeCommand ??= new RelayCommand(
            () => { if (!string.IsNullOrEmpty(FilePath)) ExecuteAnalyze(); },
            () => !string.IsNullOrEmpty(FilePath) && !_isAnalyzingInProgress);
        private set => _analyzeCommand = value;
    }

    public RelayCommand CycleThemeCommand
    {
        get => _cycleThemeCommand ??= new RelayCommand(() => ThemeManager.CycleTheme());
        private set => _cycleThemeCommand = value;
    }

    public RelayCommand OpenUrlCommand
    {
        get => _openUrlCommand ??= new RelayCommand(urlObj => 
        {
            if (urlObj is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"OpenUrlCommand failed: {ex.Message}");
                }
            }
        });
        private set => _openUrlCommand = value;
    }

    public bool IsAnalyzingInProgress => _isAnalyzingInProgress;

    public bool IsSaveMetadataEnabled
    {
        get => _isSaveMetadataEnabled;
        set
        {
            if (SetProperty(ref _isSaveMetadataEnabled, value))
            {
                SaveMetadataCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand SaveMetadataCommand
    {
        get => _saveMetadataCommand ??= new RelayCommand(ExecuteSaveMetadata, () => IsSaveMetadataEnabled);
        private set => _saveMetadataCommand = value;
    }

    private void ExecuteBrowse()
    {
        var filter = "Audio Files|*.mp3;*.wav;*.ogg;*.flac;*.m4a;*.aac;*.aiff;*.wma";
        var filePath = _filePickerService.OpenFile(filter, "Select Audio File");

        if (!string.IsNullOrEmpty(filePath))
        {
            LoadAudioFile(filePath);
        }
    }

    public void LoadAudioFile(string filePath)
    {
        try
        {
            if (!_filePickerService.ValidateAudioFile(filePath))
            {
                _messageBoxService.ShowError("Archivo no válido. Seleccione un archivo de audio válido (MP3, WAV, OGG, FLAC, M4A, AAC, AIFF, WMA).");
                StatusText = "Archivo no válido.";
                return;
            }

            if (_isAnalyzingInProgress)
            {
                if (_pendingFilePath != null)
                {
                    StatusText = "Ya hay archivo en cola. Solo se permite uno.";
                    return;
                }
                
                _pendingFilePath = filePath;
                
                try
                {
                    _audioPlayerService.Stop();
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Warning: AudioPlayer Stop failed during file queue - {ex.Message}");
                }
                
                try
                {
                    _audioPlayerService.UnloadFile();
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"Warning: AudioPlayer Unload failed during file queue - {ex.Message}");
                }
                
                FilePath = null;
                FileName = "Archivo en cola";
                IsFileSelected = false;
                
                StatusText = "Análisis en proceso. Archivo en cola.";
                return;
            }

            FilePath = filePath;
            _audioPlayerService.LoadFile(filePath);

            FileName = Path.GetFileName(filePath);
            IsFileSelected = true;

            ArePlaybackControlsEnabled = true;
            IsAnalyzeButtonEnabled = true;

            var audioInfo = _audioPlayerService.GetAudioFileInfo();
            LoggerService.Log($"LoadAudioFile() - audioInfo es null: {audioInfo == null}");
            
            if (audioInfo != null)
            {
                LoggerService.Log($"LoadAudioFile() - Asignando: Type={audioInfo.FileType}, SR={audioInfo.SampleRate}, BD={audioInfo.BitDepth}, Ch={audioInfo.Channels}, BR={audioInfo.Bitrate}, BRM={audioInfo.BitrateMode}");

                _currentAudioInfo = audioInfo;
                AudioFileType = audioInfo.FileType;
                SampleRateText = audioInfo.SampleRateDisplay;
                BitDepthText = audioInfo.BitDepthDisplay;
                ChannelsText = audioInfo.ChannelsDisplay;
                BitrateText = audioInfo.BitrateDisplay;
                BitrateModeText = audioInfo.BitrateModeDisplay;

                OnPropertyChanged(nameof(AudioFileType));
                OnPropertyChanged(nameof(SampleRateText));
                OnPropertyChanged(nameof(BitDepthText));
                OnPropertyChanged(nameof(ChannelsText));
                OnPropertyChanged(nameof(BitrateText));
                OnPropertyChanged(nameof(BitrateModeText));
                OnPropertyChanged(nameof(AudioInfoSummary));

                StatusText = $"Audio: {audioInfo.FileType} | {audioInfo.SampleRateDisplay} | {audioInfo.BitDepthDisplay} | {audioInfo.BitrateDisplay} | {audioInfo.BitrateModeDisplay} | {audioInfo.ChannelsDisplay}";
            }
            else
            {
                LoggerService.Log("LoadAudioFile() - audioInfo es null, asignando valores vacios");

                _currentAudioInfo = null;
                AudioFileType = "";
                SampleRateText = "";
                BitDepthText = "";
                ChannelsText = "";
                BitrateText = "";
                BitrateModeText = "";

                OnPropertyChanged(nameof(AudioFileType));
                OnPropertyChanged(nameof(SampleRateText));
                OnPropertyChanged(nameof(BitDepthText));
                OnPropertyChanged(nameof(ChannelsText));
                OnPropertyChanged(nameof(BitrateText));
                OnPropertyChanged(nameof(BitrateModeText));
                OnPropertyChanged(nameof(AudioInfoSummary));
            }

            BpmText = "--";
            KeyText = "--";
            ModeText = "";
            BpmConfidence = "";
            KeyConfidence = "";
            WaveformData = null;
            
            _originalBpm = 0;
            _displayBpm = 0;
            _bpmAdjusted = false;
            _bpmModifierText = "";
            _keyIndex = -1;
            _showRelativeKey = false;
            OnPropertyChanged(nameof(BpmDisplayText));
            OnPropertyChanged(nameof(IsBpmModified));
            OnPropertyChanged(nameof(KeyDisplayText));

            UpdatePositionDisplay();
            StatusText = "Archivo cargado. Listo para analizar.";
            StatusState = "Normal";
        }
        catch (Exception ex)
        {
            _messageBoxService.ShowError($"Error al cargar archivo: {ex.Message}");
            StatusText = "Error al cargar archivo.";
        }
    }

    private void ExecutePlay()
    {
        _audioPlayerService.Play();
        StatusText = "Reproduciendo...";
    }

    private void ExecutePause()
    {
        _audioPlayerService.Pause();
        StatusText = "En pausa.";
    }

    private void ExecuteStop()
    {
        _audioPlayerService.Stop();
        StatusText = "Detenido.";
        UpdatePositionDisplay();
    }

    private async void ExecuteAnalyze()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        try
        {
            _audioPlayerService.Stop();
        }
        catch (Exception ex)
        {
            LoggerService.Log($"Warning: AudioPlayer Stop failed before analysis - {ex.Message}");
        }

        _isAnalyzingInProgress = true;
        IsAnalyzeButtonEnabled = false;
        IsAnalysisProgressVisible = true;
        AnalysisProgress = 0;

        BpmText = "...";
        KeyText = "...";
        ModeText = "";
        StatusText = "Analizando audio...";

        try
        {
            AnalysisProgress = 10;
            StatusText = "Cargando audio...";
            LoggerService.Log("ExecuteAnalyze - Cargando audio una sola vez con AudioDataProvider");

            // === SINGLE FILE LOAD: eliminates 3x redundant I/O ===
            var audioProvider = new AudioDataProvider();
            var (monoSamples, sampleRate) = await Task.Run(() => audioProvider.LoadMono(FilePath));
            LoggerService.Log($"ExecuteAnalyze - Audio loaded: {monoSamples.Length} samples @ {sampleRate}Hz");

            AnalysisProgress = 20;
            StatusText = "Detectando BPM, Key y Loudness...";
            LoggerService.Log("ExecuteAnalyze - Iniciando deteccion paralela BPM, Key, Loudness y Waveform");

            // BPM, Key and Waveform share the SAME samples in memory (zero-copy)
            // Loudness uses FFmpeg externally — receives filePath independently
            var bpmTask = _bpmDetectorService.DetectBpmAsync(monoSamples, sampleRate, null, SelectedBpmProfile);
            var keyTask = _keyDetectorService.DetectKeyAsync(monoSamples, sampleRate);
            var waveformTask = _waveformAnalyzerService.AnalyzeAsync(monoSamples, sampleRate, null);
            var loudnessTask = _loudnessAnalyzerService.AnalyzeAsync(FilePath, null);

            double bpm = 0;
            double altBpm = 0;
            string key = "Error";
            string mode = "Error";
            double keyConfidence = 0;
            WaveformData? waveformData = null;
            LoudnessResult loudness = new LoudnessResult();

            try { (bpm, altBpm) = await bpmTask; }
            catch (Exception ex) { LoggerService.Log($"ExecuteAnalyze - BPM module failed: {ex.Message}"); }

            try { (key, mode, keyConfidence) = await keyTask; }
            catch (Exception ex) { LoggerService.Log($"ExecuteAnalyze - Key module failed: {ex.Message}"); }

            try { waveformData = await waveformTask; }
            catch (Exception ex) { LoggerService.Log($"ExecuteAnalyze - Waveform module failed: {ex.Message}"); }

            try { loudness = await loudnessTask; }
            catch (Exception ex) { LoggerService.Log($"ExecuteAnalyze - Loudness module failed: {ex.Message}"); }

            LoggerService.Log($"ExecuteAnalyze - Resultados: BPM={bpm}, Key={key}/{mode}, Loudness={loudness.IntegratedDisplay}");

            BpmText = bpm > 0 ? bpm.ToString("F1") : "--";
            AlternativeBpmText = altBpm > 0 && altBpm != bpm ? $"Alt: {altBpm:F0} BPM" : "";
            BpmConfidence = bpm > 0 ? "Detected" : "";
            if (bpm > 0)
            {
                _originalBpm = bpm;
                _displayBpm = bpm;
                _bpmAdjusted = false;
                _bpmModifierText = "";
                OnPropertyChanged(nameof(BpmDisplayText));
                OnPropertyChanged(nameof(IsBpmModified));
            }

            KeyText = key != "Error" ? key : "--";
            ModeText = mode != "Error" ? mode : "";
            KeyConfidence = keyConfidence > 0 ? $"Confidence: {keyConfidence:P0}" : "";
            if (key != "Error")
            {
                _keyIndex = Array.IndexOf(NoteNames, key);
                _showRelativeKey = false;
                OnPropertyChanged(nameof(KeyDisplayText));
            }

            LoudnessResult = loudness;
            IsLoudnessVisible = true;

            if (bpm > 0 && waveformData != null)
            {
                LoggerService.Log("ExecuteAnalyze - Re-analizando waveform con BPM");
                WaveformData = await _waveformAnalyzerService.AnalyzeAsync(monoSamples, sampleRate, bpm);
            }
            else if (waveformData != null)
            {
                WaveformData = waveformData;
            }

            LoggerService.Log("ExecuteAnalyze - Waveform analizado");

            AnalysisProgress = 100;
            StatusText = "¡Análisis completo!";
            LoggerService.Log("ExecuteAnalyze - Analisis completo");
        }
        catch (Exception ex)
        {
            StatusText = $"Error en análisis: {ex.Message}";
            LoggerService.Log($"ExecuteAnalyze - Error: {ex.Message}");
        }
        finally
        {
            IsAnalyzeButtonEnabled = true;
            IsAnalysisProgressVisible = false;
            
            bool hasResults = BpmText != "..." && BpmText != "--" && 
                             KeyText != "..." && KeyText != "--";
            IsSaveMetadataEnabled = hasResults && !string.IsNullOrEmpty(FilePath);

            _isAnalyzingInProgress = false;

            if (!string.IsNullOrEmpty(_pendingFilePath))
            {
                var nextFile = _pendingFilePath;
                _pendingFilePath = null;
                LoadAudioFile(nextFile);
                ExecuteAnalyze();
            }
        }
    }

    private void ExecuteSaveMetadata()
    {
        if (string.IsNullOrEmpty(FilePath)) return;
        
        if (!double.TryParse(BpmText, out double bpm)) return;
        if (string.IsNullOrEmpty(KeyText) || KeyText == "--") return;
        
        var (hasMetadata, currentBpm, currentKey) = _metadataWriter.GetCurrentMetadata(FilePath);
        
        string message;
        if (hasMetadata)
        {
            message = $"El archivo ya tiene metadata:\nBPM actual: {currentBpm}\nKey actual: {currentKey}\n\n¿Deseas sobrescribir los valores?";
        }
        else
        {
            message = $"¿Guardar metadata en el archivo?\n\nBPM: {bpm}\nKey: {KeyText} {ModeText}";
        }
        
        var result = _messageBoxService.ShowConfirmation(message, "Guardar Metadata");
        
        if (result)
        {
            _audioPlayerService.Stop();
            _audioPlayerService.UnloadFile();
            
            var (success, msg) = _metadataWriter.WriteMetadata(FilePath, bpm, KeyText, ModeText);
            StatusText = msg;
            
            if (success && !string.IsNullOrEmpty(FilePath))
            {
                _audioPlayerService.LoadFile(FilePath);
                StatusText = msg + " (Archivo recargado)";
            }
        }
        else
        {
            StatusText = "Guardado de metadata cancelado.";
        }
    }

    private void OnPlaybackStateChanged(object? sender, NAudio.Wave.PlaybackState state)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            switch (state)
            {
                case NAudio.Wave.PlaybackState.Playing:
                    StatusText = "Reproduciendo...";
                    break;
                case NAudio.Wave.PlaybackState.Stopped:
                case NAudio.Wave.PlaybackState.Paused:
                    UpdatePositionDisplay();
                    break;
            }
        });
    }

    public void UpdatePosition()
    {
        if (_audioPlayerService.State == NAudio.Wave.PlaybackState.Playing)
        {
            UpdatePositionDisplay();
            WaveformPosition = _audioPlayerService.Position.TotalSeconds;
        }
    }

    public void ExecuteAnalyzeCommand()
    {
        ExecuteAnalyze();
    }

    private void UpdatePositionDisplay()
    {
        PositionText = FormatTime(_audioPlayerService.Position);
        DurationText = FormatTime(_audioPlayerService.Duration);
    }

    public void SeekToPosition(double positionInSeconds)
    {
        if (_audioPlayerService != null && _audioPlayerService.Duration.TotalSeconds > 0)
        {
            // Clamp position to valid range
            positionInSeconds = Math.Max(0, Math.Min(_audioPlayerService.Duration.TotalSeconds, positionInSeconds));
            
            var newPosition = TimeSpan.FromSeconds(positionInSeconds);
            _audioPlayerService.Seek(newPosition);
            PositionText = FormatTime(newPosition);
            WaveformPosition = positionInSeconds;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}";
    }

    public void HandleDragEnter(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && _filePickerService.ValidateAudioFile(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                StatusText = "Suelta el archivo de audio aquí";
                StatusState = "Success";
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        StatusText = "Formato no válido - Solo archivos de audio";
        StatusState = "Error";
    }

    public void HandleDragLeave()
    {
        StatusText = "Listo";
        StatusState = "Normal";
    }

    public void HandleDrop(DragEventArgs e)
    {
        StatusText = "Listo";
        StatusState = "Normal";

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && _filePickerService.ValidateAudioFile(files[0]))
            {
                LoadAudioFile(files[0]);
            }
            else
            {
                StatusText = "Formato no válido - Solo archivos de audio";
                StatusState = "Error";
            }
        }
    }

    public void ModifyBpmMultiply()
    {
        if (_displayBpm > 0 && _originalBpm > 0)
        {
            if (_bpmAdjusted)
            {
                _displayBpm = _originalBpm;
                _bpmAdjusted = false;
                _bpmModifierText = "";
                StatusText = "BPM reset to original";
            }
            else if (_displayBpm < 65)
            {
                _displayBpm *= 2;
                _bpmAdjusted = true;
                _bpmModifierText = "*";
                StatusText = $"BPM adjusted ×2 = {_displayBpm}";
            }
            else
            {
                _displayBpm /= 2;
                _bpmAdjusted = true;
                _bpmModifierText = "*";
                StatusText = $"BPM adjusted ÷2 = {_displayBpm}";
            }
            OnPropertyChanged(nameof(BpmDisplayText));
            OnPropertyChanged(nameof(IsBpmModified));
        }
    }

    public void ModifyBpmDivide()
    {
        if (_displayBpm > 0 && _originalBpm > 0)
        {
            if (_bpmAdjusted)
            {
                _displayBpm = _originalBpm;
                _bpmAdjusted = false;
                _bpmModifierText = "";
                StatusText = "BPM reset to original";
            }
            else if (_displayBpm > 135)
            {
                _displayBpm /= 2;
                _bpmAdjusted = true;
                _bpmModifierText = "*";
                StatusText = $"BPM adjusted ÷2 = {_displayBpm}";
            }
            else
            {
                _displayBpm *= 2;
                _bpmAdjusted = true;
                _bpmModifierText = "*";
                StatusText = $"BPM adjusted ×2 = {_displayBpm}";
            }
            OnPropertyChanged(nameof(BpmDisplayText));
            OnPropertyChanged(nameof(IsBpmModified));
        }
    }

    public void Cleanup()
    {
        _audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioPlayerService.Dispose();
    }
}