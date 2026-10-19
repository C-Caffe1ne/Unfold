using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Platform;

namespace Unfold.Desktop;

/// <summary>Native input routing for the desktop pet, including windows in other processes.</summary>
internal static class WindowsPetWindow
{
    internal const uint Transparent = 0x00000020;
    internal const uint Layered = 0x00080000;
    internal const uint TopmostFlags = 0x0001 | 0x0002 | 0x0010; // No size/move/activation; Z order changes.
    private static readonly NativeApi Native = new();

    internal static bool HasNativeHandle(IPlatformHandle? handle) =>
        handle is { HandleDescriptor: "HWND" } && handle.Handle != 0;

    internal static bool TryGetPointer(Window window, out Point point)
    {
        point = default;
        if (!OperatingSystem.IsWindows() || !HasNativeHandle(window.TryGetPlatformHandle()) || !GetCursorPos(out var cursor)) return false;
        point = window.PointToClient(new PixelPoint(cursor.X, cursor.Y));
        return true;
    }

    internal static bool SetClickThrough(Window window, bool enabled) =>
        OperatingSystem.IsWindows() && HasNativeHandle(window.TryGetPlatformHandle()) &&
        SetClickThrough(window.TryGetPlatformHandle()!.Handle, enabled, Native);

    internal static bool SetClickThrough(nint hwnd, bool enabled, IInputApi api)
    {
        if (hwnd == 0 || !api.TryGetStyle(hwnd, out var original)) return false;
        // WS_EX_TRANSPARENT alone is only a paint-order hint. Windows routes
        // input to other processes when it is combined with WS_EX_LAYERED.
        var desired = enabled ? original | Layered | Transparent : original & ~Transparent;
        // Read the HWND on every poll: Avalonia can rebuild extended styles
        // while our last successful click-through value remains unchanged.
        if (desired == original) return true;
        if (!api.TrySetStyle(hwnd, desired)) return false;

        // Initialize only a layer we added. Calling this on an existing layer
        // could invalidate an UpdateLayeredWindow-backed rendering surface.
        var addedLayer = (original & Layered) == 0 && (desired & Layered) != 0;
        if ((!addedLayer || api.InitializeLayer(hwnd)) && api.RefreshStyle(hwnd) &&
            api.TryGetStyle(hwnd, out var applied) &&
            (applied & (Layered | Transparent)) == (desired & (Layered | Transparent))) return true;

        // Do not cache a partial/failed transition or leave a newly added layer
        // invisible. Restore the old input state and let the next poll retry.
        api.TrySetStyle(hwnd, original);
        api.RefreshStyle(hwnd);
        return false;
    }

    internal static bool ReassertTopmost(Window window) =>
        OperatingSystem.IsWindows() && HasNativeHandle(window.TryGetPlatformHandle()) &&
        ReassertTopmost(window.TryGetPlatformHandle()!.Handle, Native);

    // The click-through write keeps WS_EX_TOPMOST set even after another window
    // lowers this HWND, so the style alone cannot reveal or repair a lost Z order.
    internal static bool ReassertTopmost(nint hwnd, IInputApi api) => hwnd != 0 && api.PlaceTopmost(hwnd);

    internal interface IInputApi
    {
        bool TryGetStyle(nint hwnd, out uint style);
        bool TrySetStyle(nint hwnd, uint style);
        bool InitializeLayer(nint hwnd);
        bool RefreshStyle(nint hwnd);
        bool PlaceTopmost(nint hwnd);
    }

    private sealed class NativeApi : IInputApi
    {
        public bool TryGetStyle(nint hwnd, out uint style)
        {
            var value = GetWindowLongPtr(hwnd, -20);
            style = unchecked((uint)(long)value);
            return value != 0 || Marshal.GetLastPInvokeError() == 0;
        }

        public bool TrySetStyle(nint hwnd, uint style)
        {
            var previous = SetWindowLongPtr(hwnd, -20, unchecked((nint)(long)style));
            // With SetLastError=true, .NET clears the native last error before
            // the call. A zero previous style is a valid successful result.
            return previous != 0 || Marshal.GetLastPInvokeError() == 0;
        }

        public bool InitializeLayer(nint hwnd) => SetLayeredWindowAttributes(hwnd, 0, 255, 0x2);
        public bool RefreshStyle(nint hwnd) => SetWindowPos(hwnd, 0, 0, 0, 0, 0,
            0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); // No size/move/Z-order/activation; frame changed.
        public bool PlaceTopmost(nint hwnd) => SetWindowPos(hwnd, -1, 0, 0, 0, 0, TopmostFlags); // HWND_TOPMOST
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint colorKey, byte alpha, uint flags);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPoint point);
}

/// <summary>Spaces topmost repairs so they never compete with other topmost apps every frame.</summary>
internal sealed class TopmostKeepAlive(TimeSpan interval)
{
    internal static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private TimeSpan? last;

    internal bool Due(TimeSpan now, bool eligible)
    {
        if (!eligible) return false;
        if (last is null) { last = now; return false; }
        if (now - last.Value < interval) return false;
        last = now;
        return true;
    }
}
