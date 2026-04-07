using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AudioAnalyzer.Infrastructure;

public static class CornerResizeBehavior
{
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
    private const int HTCLIENT = 1;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
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
        return IntPtr.Zero;
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

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
}