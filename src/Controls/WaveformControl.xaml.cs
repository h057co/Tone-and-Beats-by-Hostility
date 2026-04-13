using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using AudioAnalyzer.Models;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Controls;

public partial class WaveformControl : UserControl
{
    private WaveformData? _waveformData;
    private double _duration;
    private double _currentPosition;
    private double _totalDuration = 1.0;

    // Puntos normalizados [0..1] - se calculan una vez al cargar datos
    private float[]? _normUpper;
    private float[]? _normLower;

    // SkiaSharp paints (reutilizados, no recreados cada frame)
    private SKPaint? _waveformFillPaint;
    private SKPaint? _waveformStrokePaint;
    private SKPaint? _playheadPaint;

    // Estado del gradiente animado
    private float _activeOffset;
    private float _inactiveOffset;

    // Colores del tema
    private SKColor _activeColor = SKColors.DeepSkyBlue;
    private SKColor _inactiveColor = new SKColor(100, 100, 100);

    // Debounce para resize
    private System.Windows.Threading.DispatcherTimer? _resizeDebounceTimer;
    private const int ResizeDebounceMs = 30;

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
        InitializePaints();
        UpdateTimeline();
    }

    private void InitializePaints()
    {
        try
        {
            if (Application.Current.Resources["AccentBrush"] is System.Windows.Media.SolidColorBrush accentBrush)
                _activeColor = accentBrush.Color.ToSkColor();

            if (Application.Current.Resources["TextSecondaryBrush"] is System.Windows.Media.SolidColorBrush textBrush)
                _inactiveColor = new SKColor(textBrush.Color.R, textBrush.Color.G, textBrush.Color.B, 100);
        }
        catch { }

        _waveformFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        _waveformStrokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            Color = _activeColor,
            IsAntialias = true,
        };

        _playheadPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            Color = new SKColor(255, 107, 107),
            IsAntialias = true,
        };

        RebuildGradient();
    }

    private void RebuildGradient()
    {
        if (_waveformFillPaint == null) return;
        if (SkiaCanvas.CanvasSize.Width <= 0) return;

        _waveformFillPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(SkiaCanvas.CanvasSize.Width, 0),
            new[] { _activeColor, _activeColor, _inactiveColor, _inactiveColor },
            new[] { 0f, _activeOffset, _inactiveOffset, 1f },
            SKShaderTileMode.Clamp);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_waveformData == null || SkiaCanvas == null) return;

        if (_resizeDebounceTimer == null)
        {
            _resizeDebounceTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(ResizeDebounceMs)
            };
            _resizeDebounceTimer.Tick += (_, _) =>
            {
                _resizeDebounceTimer.Stop();
                RebuildGradient();
                SkiaCanvas.InvalidateVisual();
            };
        }

        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformControl control)
        {
            control._currentPosition = control.Position;
            control.UpdateProgress();
        }
    }

    private void UpdateProgress()
    {
        if (_totalDuration <= 0) return;

        float progress = (float)(_currentPosition / _totalDuration);
        progress = Math.Max(0f, Math.Min(1f, progress));

        _activeOffset = progress;
        _inactiveOffset = progress;

        RebuildGradient();
        SkiaCanvas?.InvalidateVisual();
    }

    public void SetWaveformData(WaveformData data)
    {
        _waveformData = data;
        _duration = data.Duration;
        _totalDuration = data.Duration;
        _currentPosition = 0;

        CacheNormalizedPoints();

        Dispatcher.BeginInvoke(() =>
        {
            UpdateProgress();
            UpdateTimeline();
            SkiaCanvas.InvalidateVisual();
        });
    }

    private void CacheNormalizedPoints()
    {
        if (_waveformData == null)
        {
            _normUpper = null;
            _normLower = null;
            return;
        }

        var points = _waveformData.WaveformPoints;
        if (points == null || points.Count == 0)
        {
            _normUpper = null;
            _normLower = null;
            return;
        }

        int count = points.Count;
        _normUpper = new float[count];
        _normLower = new float[count];

        for (int i = 0; i < count; i++)
        {
            // upper: max sample → top (0.05), lower: min sample → bottom (0.95)
            _normUpper[i] = (float)(0.5 - (points[i][1] * 0.45));
            _normLower[i] = (float)(0.5 - (points[i][0] * 0.45));
        }
    }

    public void Clear()
    {
        _waveformData = null;
        _normUpper = null;
        _normLower = null;
        _currentPosition = 0;
        _activeOffset = 0;
        _inactiveOffset = 0;
        SkiaCanvas?.InvalidateVisual();
    }

    private void SkiaCanvas_PaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;
        canvas.Clear(SKColors.Transparent);

        if (_normUpper == null || _normLower == null || info.Width <= 0 || info.Height <= 0)
            return;

        float w = info.Width;
        float h = info.Height;

        using var path = new SKPath();

        // Upper edge (left → right)
        path.MoveTo(0, _normUpper[0] * h);
        for (int i = 1; i < _normUpper.Length; i++)
        {
            path.LineTo((i / (float)(_normUpper.Length - 1)) * w, _normUpper[i] * h);
        }

        // Lower edge (right → left)
        for (int i = _normLower.Length - 1; i >= 0; i--)
        {
            path.LineTo((i / (float)(_normLower.Length - 1)) * w, _normLower[i] * h);
        }

        path.Close();

        // Draw fill with gradient shader (already built in RebuildGradient)
        if (_waveformFillPaint != null)
            canvas.DrawPath(path, _waveformFillPaint);

        // Draw stroke
        if (_waveformStrokePaint != null)
            canvas.DrawPath(path, _waveformStrokePaint);

        // Draw playhead
        if (_totalDuration > 0 && _currentPosition > 0)
        {
            float px = (float)(_currentPosition / _totalDuration) * w;
            canvas.DrawLine(px, 0, px, h, _playheadPaint!);
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
        catch (Exception ex)
        {
            LoggerService.Log($"UpdateTimeline error: {ex.Message}");
        }
    }

    private bool _isDragging = false;

    public event EventHandler<double>? SeekRequested;

    private void RootBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        HandleSeek(e.GetPosition(SkiaCanvas).X);
        SkiaCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void RootBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
            HandleSeek(e.GetPosition(SkiaCanvas).X);
    }

    private void RootBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            SkiaCanvas.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void HandleSeek(double x)
    {
        if (_duration <= 0 || SkiaCanvas.ActualWidth <= 0) return;

        double newPosition = (x / SkiaCanvas.ActualWidth) * _duration;
        newPosition = Math.Max(0, Math.Min(_duration, newPosition));

        _currentPosition = newPosition;
        Position = newPosition;
        UpdateProgress();
        SeekRequested?.Invoke(this, newPosition);
    }
}

public static class ColorExtensions
{
    public static SKColor ToSkColor(this System.Windows.Media.Color c) => new SKColor(c.R, c.G, c.B, c.A);
}
