using System.Diagnostics;
using System.Reflection;
using System.Windows;
using AudioAnalyzer.Helpers;
using AudioAnalyzer.Services;
using AudioAnalyzer.Themes;

namespace AudioAnalyzer;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        LoggerService.Log("AboutWindow - Constructor iniciado");
        InitializeComponent();
        
        // Cap initial height to available screen work area (accounts for taskbar)
        var workArea = SystemParameters.WorkArea;
        if (Height > workArea.Height)
        {
            Height = workArea.Height;
            LoggerService.Log($"AboutWindow - Height capped to {Height} (screen work area: {workArea.Height})");
        }
        
        LoadEmbeddedImages();
        SetVersionText();
        LoggerService.Log("AboutWindow - Constructor completado");
    }

    private void SetVersionText()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"Versión {version.Major}.{version.Minor}.{version.Build}";
                LoggerService.Log($"AboutWindow - Versión detectada: {VersionText.Text}");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log($"AboutWindow - Error al obtener versión: {ex.Message}");
            VersionText.Text = "Versión 1.1.0"; // Fallback
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Log("AboutWindow.CloseButton_Click - Cerrando ventana");
        Close();
        LoggerService.Log("AboutWindow.CloseButton_Click - Ventana cerrada");
    }

    private void KoFiButton_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Log("AboutWindow.KoFiButton_Click - Abriendo ko-fi");
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://ko-fi.com/hostilityme",
            UseShellExecute = true
        });
        LoggerService.Log("AboutWindow.KoFiButton_Click - Navegador abierto");
    }

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void QrImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        QrOverlay.Opacity = 0;
        QrOverlay.Visibility = Visibility.Visible;
        
        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.2))
        };
        QrOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void QrOverlay_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var animation = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = new Duration(TimeSpan.FromSeconds(0.2))
        };
        
        animation.Completed += (s, a) => 
        {
            QrOverlay.Visibility = Visibility.Collapsed;
        };
        
        QrOverlay.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>
    /// Carga todas las imágenes incrustadas (EmbeddedResources) desde el assembly.
    /// Utiliza EmbeddedResourceHelper para centralizar la lógica de carga.
    /// </summary>
    private void LoadEmbeddedImages()
    {
        var qr = EmbeddedResourceHelper.LoadImage("qrdonaciones.png");
        if (qr != null)
        {
            if (FindName("QrImage") is System.Windows.Controls.Image qrImg) qrImg.Source = qr;
            if (FindName("QrOverlayImage") is System.Windows.Controls.Image qrOverlayImg) qrOverlayImg.Source = qr;
            LoggerService.Log("AboutWindow.LoadEmbeddedImages - ✓ QR image loaded (Main & Overlay)");
        }

        var currentTheme = ThemeManager.CurrentTheme;
        var logoFile = (currentTheme == "Light" || currentTheme == "iOS Light") 
            ? "HOST_NEGRO.png" 
            : "HOST_BLANCO.png";
        var logo = EmbeddedResourceHelper.LoadImage(logoFile);
        if (logo != null && FindName("LogoImageAbout") is System.Windows.Controls.Image logoImg)
        {
            logoImg.Source = logo;
            LoggerService.Log("AboutWindow.LoadEmbeddedImages - ✓ Logo image loaded");
        }
    }

    private bool _isUpdatingScale;

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isUpdatingScale)
        {
            Dispatcher.BeginInvoke(UpdateContentScale, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Calculates and applies uniform scale to fit all content vertically
    /// while keeping the width responsive (filling available space).
    /// </summary>
    private void UpdateContentScale()
    {
        if (_isUpdatingScale) return;
        _isUpdatingScale = true;

        try
        {
            ContentScale.ScaleX = 1;
            ContentScale.ScaleY = 1;

            ContentGrid.Measure(new Size(ContentGrid.ActualWidth > 0 ? ContentGrid.ActualWidth : ActualWidth, double.PositiveInfinity));
            double contentDesiredHeight = ContentGrid.DesiredSize.Height;

            // Available height = window height minus chrome and grid margin (5+5)
            double availableHeight = ActualHeight - 40;

            if (contentDesiredHeight > 0 && availableHeight > 0)
            {
                double scale = availableHeight / contentDesiredHeight;
                ContentScale.ScaleX = scale;
                ContentScale.ScaleY = scale;
            }
        }
        finally
        {
            _isUpdatingScale = false;
        }
    }
}
