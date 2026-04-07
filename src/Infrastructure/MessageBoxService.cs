using System.Windows;
using AudioAnalyzer.Interfaces;

namespace AudioAnalyzer.Infrastructure;

public class MessageBoxService : IMessageBoxService
{
    public void ShowError(string message, string title = "Error")
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInfo(string message, string title = "Information")
    {
        System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public MessageBoxResult ShowQuestion(string message, string title = "Question")
    {
        return System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
    }

    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        var result = System.Windows.MessageBox.Show(
            message, 
            title, 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Question);
        
        return result == MessageBoxResult.Yes;
    }
}