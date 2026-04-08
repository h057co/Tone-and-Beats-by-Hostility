using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Controls;

public partial class WaveformControl : UserControl
{
    private WaveformData? _waveformData;
    private double _duration;
    private double _currentPosition;

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
        DrawWaveform();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_waveformData != null)
        {
            DrawWaveform();
        }
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformControl control)
        {
            control.UpdatePlayheadPosition();
        }
    }

    public void SetWaveformData(WaveformData data)
    {
        _waveformData = data;
        _duration = data.Duration;
        
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
    }

    private void DrawWaveform()
    {
        if (_waveformData == null) return;

        try
        {
            // Get dimensions safely
            double width = 600;
            double height = 80;

            if (RootGrid != null)
            {
                width = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : 600;
                
                // Get height from the first child (Border)
                if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Border border)
                {
                    height = border.ActualHeight > 0 ? border.ActualHeight : 80;
                }
            }

            WaveformCanvas.Children.Clear();

            var points = _waveformData.WaveformPoints;
            if (points == null || points.Count == 0) return;

            var polygonPoints = new PointCollection();
            double centerY = height / 2;
            double xStep = width / Math.Max(points.Count - 1, 1);

            // Top line
            for (int i = 0; i < points.Count; i++)
            {
                double x = i * xStep;
                double y = centerY - (points[i][1] * centerY * 0.9);
                polygonPoints.Add(new Point(x, y));
            }

            // Bottom line (reverse)
            for (int i = points.Count - 1; i >= 0; i--)
            {
                double x = i * xStep;
                double y = centerY - (points[i][0] * centerY * 0.9);
                polygonPoints.Add(new Point(x, y));
            }

            polygonPoints.Add(polygonPoints[0]);

            var waveformPolygon = new Polygon
            {
                Points = polygonPoints,
                Fill = new SolidColorBrush(Color.FromArgb(128, 74, 144, 217)),
                Stroke = new SolidColorBrush(Color.FromRgb(74, 144, 217)),
                StrokeThickness = 1
            };

            WaveformCanvas.Children.Add(waveformPolygon);

            // Draw playhead
            DrawPlayhead(width, height);
        }
        catch (System.Exception)
        {
            // Silently handle any rendering errors
        }
    }

    private void DrawPlayhead(double width, double height)
    {
        if (_duration <= 0 || _currentPosition < 0) return;

        double x = (_currentPosition / _duration) * width;
        x = Math.Max(0, Math.Min(x, width));
        
        var playheadLine = new Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = height,
            Stroke = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
            StrokeThickness = 2,
            Opacity = 1,
            IsHitTestVisible = false
        };

        WaveformCanvas.Children.Add(playheadLine);
    }

    private void UpdatePlayheadPosition()
    {
        if (_waveformData == null || _duration <= 0) return;

        // Clamp position to valid range
        _currentPosition = Math.Max(0, Math.Min(_duration, Position));

        try
        {
            double width = 600;
            double height = 80;

            if (RootGrid != null)
            {
                width = RootGrid.ActualWidth > 0 ? RootGrid.ActualWidth : 600;
                
                if (RootGrid.Children.Count > 0 && RootGrid.Children[0] is Border border)
                {
                    height = border.ActualHeight > 0 ? border.ActualHeight : 80;
                }
            }

            // Remove old playhead lines
            var linesToRemove = new List<UIElement>();
            foreach (object child in WaveformCanvas.Children)
            {
                if (child is Line line && line.StrokeThickness == 2)
                {
                    linesToRemove.Add(line);
                }
            }
            foreach (var line in linesToRemove)
            {
                WaveformCanvas.Children.Remove(line);
            }

            // Add new playhead
            DrawPlayhead(width, height);
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
            HandleSeek(e.GetPosition(WaveformCanvas).X);
            _isDragging = false;
            WaveformCanvas.ReleaseMouseCapture();
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
}