using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AudioAnalyzer.Infrastructure;

/// <summary>
/// Converts a "level" string (Good, Warning, Danger, None) to the corresponding
/// theme Brush. Used for Loudness LUFS and True Peak visual feedback.
/// This decouples color logic from the ViewModel (SoC audit fix #6).
/// </summary>
public class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string level)
            return new SolidColorBrush(Color.FromRgb(102, 102, 102));

        return level switch
        {
            "Good"    => new SolidColorBrush(Color.FromRgb(100, 200, 100)),  // Green
            "Warning" => new SolidColorBrush(Color.FromRgb(255, 200, 100)),  // Yellow
            "Danger"  => new SolidColorBrush(Color.FromRgb(255, 100, 100)),  // Red
            _         => new SolidColorBrush(Color.FromRgb(102, 102, 102)),  // Gray/None
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
