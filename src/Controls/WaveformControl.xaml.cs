using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Controls;

public partial class WaveformControl : UserControl
{
    private WaveformData? _waveformData;
    private double _duration;
    private double _currentPosition;
    private LinearGradientBrush? _waveformBrush;
    private GradientStop? _activeStopEnd;
    private GradientStop? _inactiveStopStart;
    private double _totalDuration = 1.0;
    private Polygon? _waveformPolygon;
    private Line? _playheadLine;

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register("Position", typeof(double), typeof(WaveformControl),
            new PropertyMetadata(0.0, OnPositionChanged));

    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public WaveformControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeWaveformBrush();
        DrawWaveform();
    }

    private void InitializeWaveformBrush()
    {
        Color activeColor = Colors.DeepSkyBlue;
        Color inactiveColor = Colors.DarkGray;

        if (Application.Current.Resources["AccentColor"] is Color resActive) activeColor = resActive;
        else if (Application.Current.Resources["AccentBrush"] is SolidColorBrush resActiveBrush) activeColor = resActiveBrush.Color;

        if (Application.Current.Resources["TextSecondaryBrush"] is SolidColorBrush resInactiveBrush) inactiveColor = resInactiveBrush.Color;
        else inactiveColor = Color.FromArgb(100, 150, 150, 150);

        _activeStopEnd = new GradientStop(activeColor, 0.0);
        _inactiveStopStart = new GradientStop(inactiveColor, 0.0);

        _waveformBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(activeColor, 0.0),
                _activeStopEnd,
                _inactiveStopStart,
                new GradientStop(inactiveColor, 1.0)
            }
        };
    }

    private void UpdateWaveformProgress(double currentPosition)
    {
        if (_totalDuration <= 0 || _waveformBrush == null) return;

        double progress = currentPosition / _totalDuration;
        progress = Math.Max(0.0, Math.Min(1.0, progress));

        _activeStopEnd!.Offset = progress;
        _inactiveStopStart!.Offset = progress;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_waveformData != null && WaveformCanvas != null)
        {
            Dispatcher.BeginInvoke(DrawWaveform, DispatcherPriority.Loaded);
        }
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformControl control)
        {
            control.UpdatePlayheadPosition();
            control.UpdateWaveformProgress(control.Position);
        }
    }

    public void SetWaveformData(WaveformData data)
    {
        _waveformData = data;
        _duration = data.Duration;
        _totalDuration = data.Duration;
        
        Dispatcher.BeginInvoke(() =>
        {
            DrawWaveform();
            UpdateTimeline();
        });
    }

    public void Clear()
    {
        _waveformData = null;
        if (WaveformCanvas != null)
        {
            WaveformCanvas.Children.Clear();
        }
        _waveformPolygon = null;
        _playheadLine = null;
    }

    private void DrawWaveform()
    {
        if (_waveformData == null || WaveformCanvas == null) return;

        try
        {
            double width = WaveformCanvas.ActualWidth;
            double height = WaveformCanvas.ActualHeight;

            if (width <= 0 || height <= 0) return;

            double playheadProportion = 0;
            if (_duration > 0 && _playheadLine != null)
            {
                playheadProportion = _currentPosition / _duration;
            }

            WaveformCanvas.Children.Clear();
            _waveformPolygon = null;
            _playheadLine = null;

            var points = _waveformData.WaveformPoints;
            if (points == null || points.Count == 0) return;

            var polygonPoints = new PointCollection();
            double centerY = height / 2;
            double xStep = width / Math.Max(points.Count - 1, 1);

            for (int i = 0; i < points.Count; i++)
            {
                double x = i * xStep;
                double y = centerY - (points[i][1] * centerY * 0.9);
                polygonPoints.Add(new Point(x, y));
            }

            for (int i = points.Count - 1; i >= 0; i--)
            {
                double x = i * xStep;
                double y = centerY - (points[i][0] * centerY * 0.9);
                polygonPoints.Add(new Point(x, y));
            }

            polygonPoints.Add(polygonPoints[0]);

            _waveformPolygon = new Polygon
            {
                Points = polygonPoints,
                Fill = (Brush)_waveformBrush ?? GetThemeBrush("WaveformBrush", Color.FromRgb(74, 144, 217), 128),
                Stroke = GetThemeBrush("WaveformBrush", Color.FromRgb(74, 144, 217)),
                StrokeThickness = 1
            };

            WaveformCanvas.Children.Add(_waveformPolygon);

            if (_duration > 0)
            {
                _playheadLine = new Line
                {
                    X1 = playheadProportion * width,
                    X2 = playheadProportion * width,
                    Y1 = 0,
                    Y2 = height,
                    Stroke = GetThemeBrush("PlayheadBrush", Color.FromRgb(255, 107, 107)),
                    StrokeThickness = 2,
                    Opacity = 1,
                    IsHitTestVisible = false
                };
                WaveformCanvas.Children.Add(_playheadLine);
            }
        }
        catch (System.Exception)
        {
            // Silently handle any rendering errors
        }
    }

    private void UpdatePlayheadPosition()
    {
        if (_waveformData == null || _duration <= 0 || WaveformCanvas == null) return;

        _currentPosition = Math.Max(0, Math.Min(_duration, Position));

        try
        {
            double width = WaveformCanvas.ActualWidth;
            double height = WaveformCanvas.ActualHeight;

            if (width <= 0 || height <= 0) return;

            if (_playheadLine != null)
            {
                double x = (_currentPosition / _duration) * width;
                x = Math.Max(0, Math.Min(x, width));
                _playheadLine.X1 = x;
                _playheadLine.X2 = x;
                _playheadLine.Y1 = 0;
                _playheadLine.Y2 = height;
            }
        }
        catch (System.Exception)
        {
            // Silently handle
        }
    }

    private void UpdateTimeline()
    {
        try
        {
            if (TimeStartLabel != null)
                TimeStartLabel.Text = "0:00";
            
            if (TimeEndLabel != null)
            {
                if (_duration > 0)
                {
                    int minutes = (int)(_duration / 60);
                    int seconds = (int)(_duration % 60);
                    TimeEndLabel.Text = $"{minutes}:{seconds:D2}";
                }
                else
                {
                    TimeEndLabel.Text = "0:00";
                }
            }
        }
        catch (System.Exception ex)
        {
            LoggerService.Log($"UpdateTimeline error: {ex.Message}");
        }
    }

    private bool _isDragging = false;

    public event EventHandler<double>? SeekRequested;

    private void WaveformCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isDragging = true;
        HandleSeek(e.GetPosition(WaveformCanvas).X);
        WaveformCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void WaveformCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging)
        {
            HandleSeek(e.GetPosition(WaveformCanvas).X);
        }
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            WaveformCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void HandleSeek(double x)
    {
        if (_duration <= 0 || WaveformCanvas.ActualWidth <= 0) return;

        double newPosition = (x / WaveformCanvas.ActualWidth) * _duration;
        newPosition = Math.Max(0, Math.Min(_duration, newPosition));

        _currentPosition = newPosition;
        Position = newPosition;
        UpdatePlayheadPosition();
        
        SeekRequested?.Invoke(this, newPosition);
    }

    private SolidColorBrush GetThemeBrush(string resourceKey, Color fallback, byte? alphaOverride = null)
    {
        try
        {
            if (Application.Current?.Resources[resourceKey] is SolidColorBrush themeBrush)
            {
                var color = themeBrush.Color;
                if (alphaOverride.HasValue)
                    color = Color.FromArgb(alphaOverride.Value, color.R, color.G, color.B);
                return new SolidColorBrush(color);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log($"WaveformControl.GetThemeBrush: {ex.Message}");
        }
        
        if (alphaOverride.HasValue)
            return new SolidColorBrush(Color.FromArgb(alphaOverride.Value, fallback.R, fallback.G, fallback.B));
        return new SolidColorBrush(fallback);
    }
}
