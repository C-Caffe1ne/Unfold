using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

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
        if (!OperatingSystem.IsWindows() || !WindowsPetWindow.HasNativeHandle(TryGetPlatformHandle()) || !GetCursorPos(out var cursor)) return false;
        point = this.PointToClient(new PixelPoint(cursor.X, cursor.Y));
        return true;
    }

    internal void UpdateClickThrough()
    {
        if (!TryGetNativePointer(out var point)) return;
        UpdateHover(PetPoint(point));
        var ignore = !AcceptsPointerAt(point);
        if (OperatingSystem.IsWindows())
        {
            // Reconcile against the HWND even when our cached value matches:
            // Avalonia can rebuild its extended styles after show/layout changes.
            if (WindowsPetWindow.SetClickThrough(this, ignore)) clickThrough = ignore;
        }
        else if (OperatingSystem.IsMacOS() && ignore != clickThrough)
        {
            if (MacPetWindow.SetClickThrough(this, ignore)) clickThrough = ignore;
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPoint point);
}
