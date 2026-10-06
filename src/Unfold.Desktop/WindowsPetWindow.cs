using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Unfold.Desktop;

/// <summary>Native input routing for the desktop pet, including windows in other processes.</summary>
internal static class WindowsPetWindow
{
    internal const uint Transparent = 0x00000020;
    internal const uint Layered = 0x00080000;
    private static readonly NativeApi Native = new();

    internal static bool HasNativeHandle(IPlatformHandle? handle) =>
        handle is { HandleDescriptor: "HWND" } && handle.Handle != 0;

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

    internal interface IInputApi
    {
        bool TryGetStyle(nint hwnd, out uint style);
        bool TrySetStyle(nint hwnd, uint style);
        bool InitializeLayer(nint hwnd);
        bool RefreshStyle(nint hwnd);
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
}
