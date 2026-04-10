using System.Diagnostics;
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
        LoadEmbeddedImages();
        LoggerService.Log("AboutWindow - Constructor completado");
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

    /// <summary>
    /// Carga todas las imágenes incrustadas (EmbeddedResources) desde el assembly.
    /// Utiliza EmbeddedResourceHelper para centralizar la lógica de carga.
    /// </summary>
    private void LoadEmbeddedImages()
    {
        var qr = EmbeddedResourceHelper.LoadImage("qrdonaciones.png");
        if (qr != null && FindName("QrImage") is System.Windows.Controls.Image qrImg)
        {
            qrImg.Source = qr;
            LoggerService.Log("AboutWindow.LoadEmbeddedImages - ✓ QR image loaded");
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
}
