using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AudioAnalyzer.ViewModels;
using AudioAnalyzer.Themes;

namespace AudioAnalyzer;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            WaveformDisplay.SeekRequested += WaveformDisplay_SeekRequested;
        }

        UpdateLogoForTheme();
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void KeyText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ViewModel?.ToggleKeyDisplay();
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.CycleTheme();
        UpdateLogoForTheme();
    }

    private void UpdateLogoForTheme()
    {
        try
        {
            var currentTheme = ThemeManager.CurrentTheme;
            
            if (currentTheme == "Light")
            {
                LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Assets/HOST_NEGRO.png"));
            }
            else
            {
                LogoImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Assets/HOST_BLANCO.png"));
            }
        }
        catch
        {
            // Logo failed to load - ignore silently
        }
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }

    private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // ComboBox removed - using button instead
        // This method is kept for compatibility but not used
    }

    private void WaveformDisplay_SeekRequested(object? sender, double position)
    {
        if (ViewModel != null)
        {
            ViewModel.SeekToPosition(position);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.WaveformData))
        {
            if (ViewModel?.WaveformData != null)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    WaveformDisplay.SetWaveformData(ViewModel.WaveformData);
                });
            }
        }
        
        if (e.PropertyName == nameof(MainViewModel.WaveformPosition))
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (ViewModel != null)
                {
                    WaveformDisplay.Position = ViewModel.WaveformPosition;
                }
            });
        }
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        ViewModel?.HandleDragEnter(e);
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel?.HandleDragLeave();
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.HandleDrop(e);
            
            if (!ViewModel.IsAnalyzingInProgress && !string.IsNullOrEmpty(ViewModel.FilePath))
            {
                ViewModel.ExecuteAnalyzeCommand();
            }
        }
    }

    private void BpmText_LeftClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ViewModel?.ModifyBpmDivide();
    }

    private void BpmText_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ViewModel?.ModifyBpmMultiply();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        base.OnClosed(e);
    }
}