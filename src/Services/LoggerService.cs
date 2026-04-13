using System.IO;

namespace AudioAnalyzer.Services;

public static class LoggerService
{
    private static readonly object _lock = new object();
    private static StreamWriter? _writer;

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
                if (_writer == null)
                {
                    var directory = Path.GetDirectoryName(LogFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
                }

                _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
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