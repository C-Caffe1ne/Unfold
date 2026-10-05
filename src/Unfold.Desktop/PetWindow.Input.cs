using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Unfold.Desktop;

public sealed partial class PetWindow
{
    private Avalonia.Input.IPointer? interactionPointer;
    internal bool IsClickThrough => clickThrough;

    internal bool AcceptsPointerAt(Point point)
    {
        if (!IsVisible) return false;
        if (down is not null || (interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this) || ContextMenu?.IsOpen == true) return true;
        if (bubble.IsVisible && bubble.IsHitTestVisible &&
            this.TranslatePoint(point, bubble) is { } bubblePoint &&
            new Rect(bubble.Bounds.Size).Contains(bubblePoint)) return true;
        return this.TranslatePoint(point, animation) is { } petPoint &&
            animation.OpaqueAt(petPoint, includeEdgeTolerance: false);
    }

    private bool TryGetNativePointer(out Point point)
    {
        point = default;
        // Off-screen diagnostics must not react to the real desktop cursor.
        if (!IsVisible || runtime.DiagnosticMode) return false;
        if (OperatingSystem.IsMacOS()) return MacPetWindow.TryGetPointer(this, out point);
        if (!OperatingSystem.IsWindows() || !HasWindowsHandle(TryGetPlatformHandle()) || !GetCursorPos(out var cursor)) return false;
        point = this.PointToClient(new PixelPoint(cursor.X, cursor.Y));
        return true;
    }

    internal void UpdateClickThrough()
    {
        if (!TryGetNativePointer(out var point)) return;
        UpdateHover(PetPoint(point));
        var ignore = !AcceptsPointerAt(point);
        if (ignore == clickThrough) return;
        if (OperatingSystem.IsMacOS())
        {
            if (MacPetWindow.SetClickThrough(this, ignore)) clickThrough = ignore;
        }
        else if (OperatingSystem.IsWindows() && HasWindowsHandle(TryGetPlatformHandle()))
        {
            var hwnd = TryGetPlatformHandle()!.Handle;
            var style = (long)GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, (nint)(ignore ? style | 0x20 : style & ~0x20));
            clickThrough = ignore;
        }
    }

    internal static bool HasWindowsHandle(IPlatformHandle? handle) =>
        handle is { HandleDescriptor: "HWND" } && handle.Handle != 0;
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLong(nint hwnd, int index, nint value);
}
