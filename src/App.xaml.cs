using System.Windows;
using System.Windows.Threading;
using AudioAnalyzer.Infrastructure;
using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Services;
using AudioAnalyzer.Themes;
using AudioAnalyzer.ViewModels;

namespace AudioAnalyzer;

public partial class App : Application
{
    private MainViewModel? _viewModel;
    private DispatcherTimer? _positionTimer;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Create services (in a real app, use a DI container like Microsoft.Extensions.DependencyInjection)
        IAudioPlayerService audioPlayerService = new AudioPlayerService();
        IBpmDetectorService bpmDetectorService = new BpmDetector();
        IKeyDetectorService keyDetectorService = new KeyDetector();
        IWaveformAnalyzerService waveformAnalyzerService = new WaveformAnalyzer();
        IFilePickerService filePickerService = new FilePickerService();
        IMessageBoxService messageBoxService = new MessageBoxService();
        ILoudnessAnalyzerService loudnessAnalyzerService = new LoudnessAnalyzer();

        // Create ViewModel with injected dependencies (DIP)
        _viewModel = new MainViewModel(
            audioPlayerService,
            bpmDetectorService,
            keyDetectorService,
            waveformAnalyzerService,
            filePickerService,
            messageBoxService,
            loudnessAnalyzerService);

        // Create and show MainWindow
        var mainWindow = new MainWindow
        {
            DataContext = _viewModel
        };

        mainWindow.Show();

        // Initialize theme after window is loaded
        ThemeManager.Initialize();

        // Setup position timer for playback sync
        SetupPositionTimer();
        _positionTimer?.Start();
    }

    private void SetupPositionTimer()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _positionTimer.Tick += (s, e) =>
        {
            _viewModel?.UpdatePosition();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _positionTimer?.Stop();
        _viewModel?.Cleanup();
        base.OnExit(e);
    }
}