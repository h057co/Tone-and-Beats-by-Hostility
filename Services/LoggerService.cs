using System.IO;

namespace AudioAnalyzer.Services;

public static class LoggerService
{
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToneAndBeats",
        "app.log");

    public static void Log(string message)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
        }
        catch
        {
        }
    }

    public static void ClearLog()
    {
        try
        {
            if (File.Exists(LogFilePath))
                File.Delete(LogFilePath);
        }
        catch
        {
        }
    }
}