using System.Windows;

namespace AudioAnalyzer.Themes;

public static class ThemeManager
{
    private static string _currentTheme = "Dark";
    private static readonly Dictionary<string, string> ThemeFiles = new()
    {
        { "Dark", "Themes/DarkTheme.xaml" },
        { "Light", "Themes/LightTheme.xaml" },
        { "Blue", "Themes/BlueTheme.xaml" },
        { "iOS Light", "Themes/IosLightTheme.xaml" },
        { "iOS Dark", "Themes/IosDarkTheme.xaml" }
    };

    public static string CurrentTheme => _currentTheme;

    public static string[] AvailableThemes => ThemeFiles.Keys.ToArray();

    public static void ApplyTheme(string themeName)
    {
        if (!ThemeFiles.ContainsKey(themeName))
            themeName = "Dark";

        _currentTheme = themeName;

        var app = Application.Current;
        if (app == null) return;

        var themePath = ThemeFiles[themeName];
        var themeUri = new Uri(themePath, UriKind.Relative);
        
        var newThemeDict = new ResourceDictionary { Source = themeUri };

        var existingThemes = app.Resources.MergedDictionaries
            .Where(d => d.Source?.ToString().Contains("Theme.xaml") == true)
            .ToList();

        foreach (var theme in existingThemes)
        {
            app.Resources.MergedDictionaries.Remove(theme);
        }

        app.Resources.MergedDictionaries.Add(newThemeDict);

        UpdateStaticStyles(themeName);
    }

    private static void UpdateStaticStyles(string themeName)
    {
        var app = Application.Current;
        if (app?.MainWindow == null) return;

        var mainWindow = app.MainWindow;

        try
        {
            if (app.Resources.MergedDictionaries.Count > 0)
            {
                var themeDict = app.Resources.MergedDictionaries.FirstOrDefault(d => 
                    d.Source?.ToString().Contains("Theme.xaml") == true);
                
                if (themeDict != null && themeDict.Contains("BackgroundBrush"))
                {
                    mainWindow.Background = themeDict["BackgroundBrush"] as System.Windows.Media.Brush;
                }
            }

            ApplyIosStyles(themeName);
        }
        catch (Exception ex)
        {
            Services.LoggerService.Log($"ThemeManager.UpdateStaticStyles error: {ex.Message}");
        }
    }

    private static void ApplyIosStyles(string themeName)
    {
        var app = Application.Current;
        if (app?.MainWindow == null) return;

        bool isIos = themeName.StartsWith("iOS");

        try
        {
            var mainWindow = app.MainWindow;

            // Buscar botones por nombre y aplicar estilos iOS si corresponde
            if (mainWindow.FindName("AnalyzeButton") is System.Windows.Controls.Button analyzeBtn)
            {
                if (isIos && app.Resources.Contains("IosAnalyzeButtonStyle"))
                    analyzeBtn.Style = app.Resources["IosAnalyzeButtonStyle"] as System.Windows.Style;
                else
                    analyzeBtn.Style = mainWindow.Resources["AnalyzeButton"] as System.Windows.Style;
            }

            if (mainWindow.FindName("SaveMetadataButton") is System.Windows.Controls.Button saveBtn)
            {
                if (isIos && app.Resources.Contains("IosButtonStyle"))
                    saveBtn.Style = app.Resources["IosButtonStyle"] as System.Windows.Style;
                else
                    saveBtn.Style = mainWindow.Resources["SaveMetadataButton"] as System.Windows.Style;
            }

            if (mainWindow.FindName("BrowseButton") is System.Windows.Controls.Button browseBtn)
            {
                if (isIos && app.Resources.Contains("IosButtonStyle"))
                    browseBtn.Style = app.Resources["IosButtonStyle"] as System.Windows.Style;
                else
                    browseBtn.ClearValue(System.Windows.FrameworkElement.StyleProperty);
            }
        }
        catch (Exception ex)
        {
            Services.LoggerService.Log($"ThemeManager.ApplyIosStyles error: {ex.Message}");
        }
    }

    public static void Initialize()
    {
        ApplyTheme(_currentTheme);
    }

    public static void CycleTheme()
    {
        var themes = AvailableThemes;
        var currentIndex = Array.IndexOf(themes, _currentTheme);
        var nextIndex = (currentIndex + 1) % themes.Length;
        ApplyTheme(themes[nextIndex]);
    }
}
