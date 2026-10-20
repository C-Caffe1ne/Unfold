using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Unfold.Desktop;

/// <summary>OS-drawn title bar controls for the regular windows. The pet, bubble and timer surfaces stay undecorated.</summary>
internal static class NativeWindowChrome
{
    // WTNCA_NODRAWCAPTION | WTNCA_NODRAWICON. The title still names the window in the taskbar and Alt+Tab,
    // and the system menu stays available.
    internal const uint HiddenTitleBarParts = 1 | 2;
    private const int DwmwaCaptionColor = 35, WtaNonClient = 1;
    private static readonly ConditionalWeakTable<Window, TitleBarPaint> paints = new();

    internal sealed record TitleBarPaint(uint CaptionColor, int CaptionResult, int ThemeResult);

    /// <summary>COLORREF (0x00BBGGRR) as DWM expects it.</summary>
    internal static uint CaptionColor(Color color) => (uint)(color.R | color.G << 8 | color.B << 16);

    public static void Configure(Window window)
    {
        window.WindowDecorations = WindowDecorations.Full;
        // AppKit keeps its standard buttons in an extended title bar. In Avalonia 12, extending on
        // Windows instead opts into Avalonia-drawn caption controls, so Windows keeps its OS title bar.
        window.ExtendClientAreaToDecorationsHint = OperatingSystem.IsMacOS();
        window.ExtendClientAreaTitleBarHeightHint = -1;
        // Windows 10 ignores the caption color, so it keeps the standard title bar with its title and icon.
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) PaintWindowsTitleBar(window);
    }

    /// <summary>Places <paramref name="content"/> below the extended title bar. The band above it hands presses
    /// to the OS window move and title bar double-click; the app never moves the window itself.</summary>
    public static Panel Host(Window window, Control content, string titleBarName = "NativeTitleBar")
    {
        var titleBar = new Border { Name = titleBarName, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Top };
        WindowDecorationProperties.SetElementRole(titleBar, WindowDecorationsElementRole.TitleBar);
        var margin = content.Margin;
        void UpdateInsets()
        {
            var inset = window.IsExtendedIntoWindowDecorations ? window.WindowDecorationMargin : default;
            content.Margin = margin + inset;
            titleBar.Height = inset.Top;
            titleBar.IsVisible = inset.Top > 0;
        }
        window.PropertyChanged += (_, args) =>
        {
            if (args.Property == Window.WindowDecorationMarginProperty || args.Property == Window.IsExtendedIntoWindowDecorationsProperty)
                UpdateInsets();
        };
        window.Opened += (_, _) => UpdateInsets();
        UpdateInsets();
        // The Windows title bar is painted with this canvas, so the OS band and the client area meet without a seam.
        return new Grid { Background = DesignSystem.Canvas, Children = { content, titleBar } };
    }

    /// <summary>The last Windows 11 title bar paint of <paramref name="window"/>, for diagnostics.</summary>
    internal static TitleBarPaint? LastTitleBarPaint(Window window) => paints.TryGetValue(window, out var paint) ? paint : null;

    [SupportedOSPlatform("windows10.0.22000")]
    private static void PaintWindowsTitleBar(Window window)
    {
        void Paint()
        {
            if (window.TryGetPlatformHandle()?.Handle is not { } handle || handle == 0) return;
            var color = CaptionColor(DesignSystem.Canvas.Color);
            var options = new WindowThemeOptions { Flags = HiddenTitleBarParts, Mask = HiddenTitleBarParts };
            paints.AddOrUpdate(window, new(color,
                DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref color, sizeof(uint)),
                SetWindowThemeAttribute(handle, WtaNonClient, ref options, (uint)Marshal.SizeOf<WindowThemeOptions>())));
        }
        // Themes recolor the shared canvas brush in place.
        void CanvasChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
        {
            if (args.Property == SolidColorBrush.ColorProperty) Paint();
        }
        DesignSystem.Canvas.PropertyChanged += CanvasChanged;
        window.Closed += (_, _) => DesignSystem.Canvas.PropertyChanged -= CanvasChanged;
        window.Opened += (_, _) => Paint();
        Paint();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowThemeOptions { public uint Flags, Mask; }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref uint value, int size);
    [DllImport("uxtheme.dll")]
    private static extern int SetWindowThemeAttribute(nint hwnd, int type, ref WindowThemeOptions options, uint size);
}
