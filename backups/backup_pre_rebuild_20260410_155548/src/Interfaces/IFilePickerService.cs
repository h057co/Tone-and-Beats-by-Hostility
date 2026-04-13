namespace AudioAnalyzer.Interfaces;

public interface IFilePickerService
{
    string? OpenFile(string filter, string title);
    bool ValidateAudioFile(string filePath);
}