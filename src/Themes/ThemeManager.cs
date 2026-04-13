using System.Windows;

namespace AudioAnalyzer.Themes;

/// <summary>
/// Gestor centralizado de temas para la aplicación.
/// Soporta temas estándar (Dark, Light, Blue) e iOS (iOS Light, iOS Dark).
/// </summary>
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

    /// <summary>
    /// Evento que se dispara cuando el tema cambia.
    /// </summary>
    public static event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public static string CurrentTheme => _currentTheme;

    public static string[] AvailableThemes => ThemeFiles.Keys.ToArray();

    /// <summary>
    /// Aplica un tema a la aplicación.
    /// </summary>
    /// <param name="themeName">Nombre del tema a aplicar. Si no existe, usa "Dark".</param>
    public static void ApplyTheme(string themeName)
    {
        if (!ThemeFiles.ContainsKey(themeName))
            themeName = "Dark";

        if (_currentTheme == themeName)
            return; // No hacer nada si ya está el mismo tema

        _currentTheme = themeName;

        var app = Application.Current;
        if (app == null) return;

        try
        {
            var themePath = ThemeFiles[themeName];
            var themeUri = new Uri(themePath, UriKind.Relative);
            var newThemeDict = new ResourceDictionary { Source = themeUri };

            // Remover temas anteriores
            var existingThemes = app.Resources.MergedDictionaries
                .Where(d => d.Source?.ToString().Contains("Theme.xaml") == true)
                .ToList();

            foreach (var theme in existingThemes)
            {
                app.Resources.MergedDictionaries.Remove(theme);
            }

            // Agregar nuevo tema
            app.Resources.MergedDictionaries.Add(newThemeDict);

            // Aplicar cambios visuales
            UpdateWindowBackground(app);

            // Notificar cambio de tema
            OnThemeChanged(new ThemeChangedEventArgs(themeName));
        }
        catch (Exception ex)
        {
            Services.LoggerService.Log($"ThemeManager.ApplyTheme error: {ex.Message}");
        }
    }

    /// <summary>
    /// Actualiza el fondo de la ventana principal con el color del tema actual.
    /// </summary>
    private static void UpdateWindowBackground(Application app)
    {
        if (app?.MainWindow == null) return;

        try
        {
            var themeDict = app.Resources.MergedDictionaries.FirstOrDefault(d => 
                d.Source?.ToString().Contains("Theme.xaml") == true);
            
            if (themeDict?.Contains("BackgroundBrush") == true)
            {
                app.MainWindow.Background = themeDict["BackgroundBrush"] as System.Windows.Media.Brush;
            }
        }
        catch (Exception ex)
        {
            Services.LoggerService.Log($"ThemeManager.UpdateWindowBackground error: {ex.Message}");
        }
    }

    /// <summary>
    /// Inicializa el tema por defecto.
    /// </summary>
    public static void Initialize()
    {
        var theme = _currentTheme;
        _currentTheme = ""; // Reset para que ApplyTheme no haga early return
        ApplyTheme(theme);
    }

    /// <summary>
    /// Cicla al siguiente tema disponible.
    /// </summary>
    public static void CycleTheme()
    {
        var themes = AvailableThemes;
        var currentIndex = Array.IndexOf(themes, _currentTheme);
        var nextIndex = (currentIndex + 1) % themes.Length;
        ApplyTheme(themes[nextIndex]);
    }

    /// <summary>
    /// Dispara el evento de cambio de tema.
    /// </summary>
    private static void OnThemeChanged(ThemeChangedEventArgs e)
    {
        ThemeChanged?.Invoke(null, e);
    }
}

/// <summary>
/// Argumentos del evento cuando cambia el tema.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public string ThemeName { get; }

    public ThemeChangedEventArgs(string themeName)
    {
        ThemeName = themeName;
    }
}
