using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AudioAnalyzer.Services;

namespace AudioAnalyzer.Infrastructure;

public static class CornerResizeBehavior
{
    private static double _aspectRatio = 1.9; // Default (760/400)

    public static readonly DependencyProperty EnableOnlyCornerResizeProperty =
        DependencyProperty.RegisterAttached(
            "EnableOnlyCornerResize",
            typeof(bool),
            typeof(CornerResizeBehavior),
            new PropertyMetadata(false, OnEnableOnlyCornerResizeChanged));

    public static bool GetEnableOnlyCornerResize(DependencyObject obj)
        => (bool)obj.GetValue(EnableOnlyCornerResizeProperty);

    public static void SetEnableOnlyCornerResize(DependencyObject obj, bool value)
        => obj.SetValue(EnableOnlyCornerResizeProperty, value);

    private static void OnEnableOnlyCornerResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window && (bool)e.NewValue)
        {
            window.SourceInitialized += Window_SourceInitialized;
            window.Loaded += Window_Loaded;
        }
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
        {
            if (window.ActualWidth > 0 && window.ActualHeight > 0)
            {
                _aspectRatio = window.ActualHeight / window.ActualWidth;
            }
        }
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            var handle = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
        }
    }

    private const int WM_NCHITTEST = 0x0084;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const int HTCLIENT = 1;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                return HandleNCHitTest(hwnd, lParam, ref handled);
            case WM_WINDOWPOSCHANGING:
                HandleWindowPosChanging(hwnd, lParam);
                break;
        }
        return IntPtr.Zero;
    }

    private static IntPtr HandleNCHitTest(IntPtr hwnd, IntPtr lParam, ref bool handled)
    {
        int xPos = (short)(lParam.ToInt32() & 0xFFFF);
        int yPos = (short)((lParam.ToInt32() >> 16) & 0xFFFF);

        var result = GetHitTestResult(hwnd, xPos, yPos);
        if (result != 0)
        {
            handled = true;
            return new IntPtr(result);
        }
        else
        {
            handled = true;
            return new IntPtr(HTCLIENT);
        }
    }

    private static void HandleWindowPosChanging(IntPtr hwnd, IntPtr lParam)
    {
        try
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);

            if ((pos.flags & 0x0001) != 0) return; // SWP_NOSIZE flag set

            if (pos.cx > 0 && pos.cy > 0)
            {
                double newHeight = pos.cx * _aspectRatio;
                pos.cy = (int)Math.Round(newHeight);
                Marshal.StructureToPtr(pos, lParam, true);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Log($"CornerResizeBehavior.WndProc: {ex.Message}");
        }
    }

    private static int GetHitTestResult(IntPtr hwnd, int xPos, int yPos)
    {
        int cornerSize = 15;

        var windowRect = new RECT();
        GetWindowRect(hwnd, ref windowRect);

        int relativeX = xPos - windowRect.Left;
        int relativeY = yPos - windowRect.Top;
        int windowWidth = windowRect.Right - windowRect.Left;
        int windowHeight = windowRect.Bottom - windowRect.Top;

        bool onLeft = relativeX >= 0 && relativeX < cornerSize;
        bool onRight = relativeX > windowWidth - cornerSize && relativeX <= windowWidth;
        bool onTop = relativeY >= 0 && relativeY < cornerSize;
        bool onBottom = relativeY > windowHeight - cornerSize && relativeY <= windowHeight;

        if (onLeft && onTop) return HTTOPLEFT;
        if (onRight && onTop) return HTTOPRIGHT;
        if (onLeft && onBottom) return HTBOTTOMLEFT;
        if (onRight && onBottom) return HTBOTTOMRIGHT;

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public int flags;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
}