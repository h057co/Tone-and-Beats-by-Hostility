using System.Diagnostics;
using System.Windows;
using AudioAnalyzer.Services;

namespace AudioAnalyzer;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        LoggerService.Log("AboutWindow - Constructor iniciado");
        InitializeComponent();
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
}
