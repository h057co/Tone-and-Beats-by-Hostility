using System.IO;
using System.Linq;
using Microsoft.Win32;
using AudioAnalyzer.Interfaces;

namespace AudioAnalyzer.Infrastructure;

public class FilePickerService : IFilePickerService
{
    private static readonly string[] ValidExtensions = 
        { ".mp3", ".wav", ".ogg", ".flac", ".m4a", ".aac", ".aiff", ".wma" };

    public string? OpenFile(string filter, string title)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title,
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool ValidateAudioFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        var ext = Path.GetExtension(filePath).ToLower();
        return ValidExtensions.Contains(ext);
    }
}