using System.IO;

namespace AudioAnalyzer.Services;

public static class LoggerService
{
    private static readonly object _lock = new object();

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToneAndBeats",
        "app.log");

    public static void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggerService.Log failed: {ex.Message}");
            }
        }
    }

    public static void ClearLog()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(LogFilePath))
                    File.Delete(LogFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggerService.ClearLog failed: {ex.Message}");
            }
        }
    }
}