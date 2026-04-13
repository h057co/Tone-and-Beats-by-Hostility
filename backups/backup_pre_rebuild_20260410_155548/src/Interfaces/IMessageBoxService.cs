using System.Windows;

namespace AudioAnalyzer.Interfaces;

public interface IMessageBoxService
{
    void ShowError(string message, string title = "Error");
    void ShowInfo(string message, string title = "Information");
    MessageBoxResult ShowQuestion(string message, string title = "Question");
    bool ShowConfirmation(string message, string title = "Confirm");
}