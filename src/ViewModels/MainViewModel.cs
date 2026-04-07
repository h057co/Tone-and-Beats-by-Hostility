using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AudioAnalyzer.Commands;
using AudioAnalyzer.Infrastructure;
using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IBpmDetectorService _bpmDetectorService;
    private readonly IKeyDetectorService _keyDetectorService;
    private readonly IWaveformAnalyzerService _waveformAnalyzerService;
    private readonly IFilePickerService _filePickerService;
    private readonly IMessageBoxService _messageBoxService;
    private readonly MetadataWriter _metadataWriter;

    private string _fileName = "No file selected";
    private Brush _fileNameForeground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
    private string _positionText = "00:00";
    private string _durationText = "00:00";
    private string _bpmText = "--";
    private string _keyText = "--";
    private string _modeText = "";
    private string _bpmConfidence = "";
    private string _keyConfidence = "";
    private string _statusText = "Ready";
    private Brush _statusForeground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
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
    private double _originalBpm;
    private double _displayBpm;
    private bool _bpmAdjusted;
    private string _bpmModifierText = "";
    private string _audioFileType = "";
    private string _sampleRateText = "";
    private string _bitDepthText = "";
    private string _channelsText = "";
    private string _bitrateText = "";
    private string _bitrateModeText = "";
    private AudioFileInfo? _currentAudioInfo;
    private int _keyIndex = -1;
    private bool _showRelativeKey = false;

    public MainViewModel(
        IAudioPlayerService audioPlayerService,
        IBpmDetectorService bpmDetectorService,
        IKeyDetectorService keyDetectorService,
        IWaveformAnalyzerService waveformAnalyzerService,
        IFilePickerService filePickerService,
        IMessageBoxService messageBoxService)
    {
        _audioPlayerService = audioPlayerService;
        _bpmDetectorService = bpmDetectorService;
        _keyDetectorService = keyDetectorService;
        _waveformAnalyzerService = waveformAnalyzerService;
        _filePickerService = filePickerService;
        _messageBoxService = messageBoxService;
        _metadataWriter = new MetadataWriter();

        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public Brush FileNameForeground
    {
        get => _fileNameForeground;
        set => SetProperty(ref _fileNameForeground, value);
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

    public Brush BpmForeground
    {
        get => !string.IsNullOrEmpty(_bpmModifierText)
            ? new SolidColorBrush(Color.FromRgb(255, 107, 107))  // Rojo/salmon cuando modificado
            : (Brush)Application.Current.Resources["TitleBrush"];
    }

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

    public Brush StatusForeground
    {
        get => _statusForeground;
        set => SetProperty(ref _statusForeground, value);
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
                StatusText = "Invalid file selected.";
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
                catch { }
                
                try
                {
                    _audioPlayerService.UnloadFile();
                }
                catch { }
                
                FilePath = null;
                FileName = "Archivo en cola";
                FileNameForeground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
                
                StatusText = "Análisis en proceso. Archivo en cola.";
                return;
            }

            FilePath = filePath;
            _audioPlayerService.LoadFile(filePath);

            FileName = Path.GetFileName(filePath);
            FileNameForeground = new SolidColorBrush(Color.FromRgb(224, 224, 224));

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
            OnPropertyChanged(nameof(BpmForeground));
            OnPropertyChanged(nameof(KeyDisplayText));

            UpdatePositionDisplay();
            StatusText = "File loaded. Ready to analyze.";
            StatusForeground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
        }
        catch (Exception ex)
        {
            _messageBoxService.ShowError($"Error loading file: {ex.Message}");
            StatusText = "Error loading file.";
        }
    }

    private void ExecutePlay()
    {
        _audioPlayerService.Play();
        StatusText = "Playing...";
    }

    private void ExecutePause()
    {
        _audioPlayerService.Pause();
        StatusText = "Paused.";
    }

    private void ExecuteStop()
    {
        _audioPlayerService.Stop();
        StatusText = "Stopped.";
        UpdatePositionDisplay();
    }

    private async void ExecuteAnalyze()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        try
        {
            _audioPlayerService.Stop();
        }
        catch { }

        _isAnalyzingInProgress = true;
        IsAnalyzeButtonEnabled = false;
        IsAnalysisProgressVisible = true;
        AnalysisProgress = 0;

        BpmText = "...";
        KeyText = "...";
        ModeText = "";
        StatusText = "Analyzing audio...";

        try
        {
            AnalysisProgress = 10;
            StatusText = "Detecting BPM & Key...";
            LoggerService.Log("ExecuteAnalyze - Iniciando deteccion paralela BPM y Key");

            var bpmTask = _bpmDetectorService.DetectBpmAsync(FilePath);
            var keyTask = _keyDetectorService.DetectKeyAsync(FilePath);
            var waveformTask = _waveformAnalyzerService.AnalyzeAsync(FilePath, null);

            await Task.WhenAll(bpmTask, keyTask, waveformTask);

            var bpm = await bpmTask;
            var (key, mode, confidence) = await keyTask;
            var waveformData = await waveformTask;

            LoggerService.Log($"ExecuteAnalyze - BPM detectado: {bpm}");
            BpmText = bpm > 0 ? bpm.ToString("F1") : "--";
            BpmConfidence = bpm > 0 ? "Detected" : "";
            if (bpm > 0)
            {
                _originalBpm = bpm;
                _displayBpm = bpm;
                _bpmAdjusted = false;
                _bpmModifierText = "";
                OnPropertyChanged(nameof(BpmDisplayText));
                OnPropertyChanged(nameof(BpmForeground));
            }

            LoggerService.Log($"ExecuteAnalyze - Key detectada: {key}, Mode: {mode}");
            KeyText = key != "Error" ? key : "--";
            ModeText = mode != "Error" ? mode : "";
            KeyConfidence = confidence > 0 ? $"Confidence: {confidence:P0}" : "";

            _keyIndex = Array.IndexOf(NoteNames, key);
            _showRelativeKey = false;
            OnPropertyChanged(nameof(KeyDisplayText));

            if (bpm > 0)
            {
                LoggerService.Log("ExecuteAnalyze - Re-analizando waveform con BPM");
                WaveformData = await _waveformAnalyzerService.AnalyzeAsync(FilePath, bpm);
            }
            else
            {
                WaveformData = waveformData;
            }

            LoggerService.Log("ExecuteAnalyze - Waveform analizado");

            AnalysisProgress = 100;
            StatusText = "Analysis complete!";
            LoggerService.Log("ExecuteAnalyze - Analisis completo");
        }
        catch (Exception ex)
        {
            StatusText = $"Analysis error: {ex.Message}";
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
                    StatusText = "Playing...";
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
                StatusText = "Drop audio file here";
                StatusForeground = new SolidColorBrush(Color.FromRgb(100, 200, 100));
                return;
            }
        }
        e.Effects = DragDropEffects.None;
        StatusText = "Invalid file format - Audio files only";
        StatusForeground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
    }

    public void HandleDragLeave()
    {
        StatusText = "Ready";
        StatusForeground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
    }

    public void HandleDrop(DragEventArgs e)
    {
        StatusText = "Ready";
        StatusForeground = new SolidColorBrush(Color.FromRgb(102, 102, 102));

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && _filePickerService.ValidateAudioFile(files[0]))
            {
                LoadAudioFile(files[0]);
            }
            else
            {
                StatusText = "Invalid file format - Audio files only";
                StatusForeground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
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
            OnPropertyChanged(nameof(BpmForeground));
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
            OnPropertyChanged(nameof(BpmForeground));
        }
    }

    public void Cleanup()
    {
        _audioPlayerService.Dispose();
    }
}