using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using AudioAnalyzer.Helpers;
using AudioAnalyzer.ViewModels;
using AudioAnalyzer.Themes;

namespace AudioAnalyzer;

public partial class MainWindow : Window
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        LogoImage.Source = EmbeddedResourceHelper.LoadImage("HOST_BLANCO.png");
        Loaded += MainWindow_Loaded;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

    private void UpdateLogoForTheme()
    {
        try
        {
            var currentTheme = ThemeManager.CurrentTheme;
            
            if (currentTheme == "Light" || currentTheme == "iOS Light")
            {
                LogoImage.Source = EmbeddedResourceHelper.LoadImage("HOST_NEGRO.png");
            }
            else
            {
                LogoImage.Source = EmbeddedResourceHelper.LoadImage("HOST_BLANCO.png");
            }
        }
        catch (Exception ex)
        {
            Services.LoggerService.Log($"MainWindow.UpdateLogoForTheme - Error: {ex.Message}");
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
        catch (Exception ex)
        {
            Services.LoggerService.Log($"MainWindow.Hyperlink_RequestNavigate - Error: {ex.Message}");
        }
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
        ViewModel?.CycleBpmAdjustment();
    }

    private void BpmSwap_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel?.SwapBpmValues();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            // Al maximizar con WindowStyle=None + AllowsTransparency,
            // la ventana se desborda fuera de la pantalla.
            // Ajustamos MaxWidth/MaxHeight al área de trabajo del monitor actual.
            var screen = System.Windows.SystemParameters.WorkArea;
            MaxHeight = screen.Height + 16; // +16 para compensar el Margin="10" del Border exterior
            MaxWidth = screen.Width + 16;

            // Actualizar icono del botón maximizar → restaurar
            MaximizeButton.Content = "❐";
        }
        else
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;

            // Restaurar icono del botón maximizar
            MaximizeButton.Content = "□";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        WaveformDisplay.SeekRequested -= WaveformDisplay_SeekRequested;
        base.OnClosed(e);
    }
}