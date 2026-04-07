using System.Windows;

namespace AudioAnalyzer.Themes;

public static class ThemeManager
{
    private static string _currentTheme = "Dark";
    private static readonly Dictionary<string, string> ThemeFiles = new()
    {
        { "Dark", "Themes/DarkTheme.xaml" },
        { "Light", "Themes/LightTheme.xaml" },
        { "Blue", "Themes/BlueTheme.xaml" }
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
        }
        catch
        {
            // Ignore errors in style updates
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
