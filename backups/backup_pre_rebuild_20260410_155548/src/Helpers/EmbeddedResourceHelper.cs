using System.Reflection;
using System.Windows.Media.Imaging;

namespace AudioAnalyzer.Helpers;

/// <summary>
/// Helper para cargar imágenes incrustadas (EmbeddedResource) desde el assembly.
/// Centraliza la lógica de carga de recursos embebidos con manejo de errores.
/// </summary>
public static class EmbeddedResourceHelper
{
    /// <summary>
    /// Carga una imagen PNG incrustada desde el assembly por nombre de archivo.
    /// </summary>
    /// <param name="resourceFileName">Nombre del archivo (ej: "qrdonaciones.png")</param>
    /// <returns>BitmapImage cargada en memoria, o null si falla</returns>
    public static BitmapImage? LoadImage(string resourceFileName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"AudioAnalyzer.Assets.{resourceFileName}";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
