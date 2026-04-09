using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using AudioAnalyzer.Services;

namespace AudioAnalyzer;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        LoggerService.Log("AboutWindow - Constructor iniciado");
        InitializeComponent();
        LoadEmbeddedImage();
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

    /// <summary>
    /// Carga la imagen QR incrustada (EmbeddedResource) desde el assembly.
    /// Esto garantiza que la imagen siempre esté disponible en todos los escenarios de build:
    /// Debug, Release, Single-File executable e Instalador.
    /// 
    /// Beneficios:
    /// - No depende de archivos externos copiados en CopyToOutputDirectory
    /// - Funciona en single-file executables sin problemas
    /// - Evita regresiones en futuras versiones (no se puede "perder")
    /// - Mejora la portabilidad y confiabilidad de la aplicación
    /// </summary>
    private void LoadEmbeddedImage()
    {
        try
        {
            LoggerService.Log("AboutWindow.LoadEmbeddedImage - Intentando cargar imagen QR incrustada");
            
            // Obtener el assembly actual
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "AudioAnalyzer.Assets.qrdonaciones.png";
            
            // Buscar el recurso incrustado
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    LoggerService.Log("AboutWindow.LoadEmbeddedImage - ERROR: Recurso no encontrado: " + resourceName);
                    return;
                }
                
                // Cargar la imagen desde el stream
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // Freeze para permitir acceso desde otros threads
                
                // Asignar a la imagen en XAML (asumiendo que existe un control llamado QrImage)
                if (FindName("QrImage") is System.Windows.Controls.Image qrImage)
                {
                    qrImage.Source = bitmap;
                    LoggerService.Log("AboutWindow.LoadEmbeddedImage - ✓ Imagen QR cargada exitosamente desde recurso incrustado");
                }
                else
                {
                    LoggerService.Log("AboutWindow.LoadEmbeddedImage - ERROR: Control QrImage no encontrado en XAML");
                }
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log($"AboutWindow.LoadEmbeddedImage - ERROR: {ex.Message}");
            LoggerService.Log($"Stack trace: {ex.StackTrace}");
        }
    }
}
