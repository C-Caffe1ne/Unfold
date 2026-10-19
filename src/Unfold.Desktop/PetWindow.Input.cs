using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace Unfold.Desktop;

public sealed partial class PetWindow
{
    private Avalonia.Input.IPointer? interactionPointer;
    private readonly TopmostKeepAlive topmost = new(TopmostKeepAlive.Interval);
    private readonly System.Diagnostics.Stopwatch topmostClock = System.Diagnostics.Stopwatch.StartNew();
    internal bool IsClickThrough => clickThrough;
    internal bool PointerOverBody(Point point) => this.TranslatePoint(point, animation) is { } local &&
        animation.OpaqueAt(local, includeEdgeTolerance: false);
    internal PixelRect InteractiveScreenBounds
    {
        get
        {
            var rect = animation.InteractiveBounds;
            var corners = new[] { rect.TopLeft, rect.TopRight, rect.BottomLeft, rect.BottomRight }
                .Select(p => animation.TranslatePoint(p, this) ?? p).ToArray();
            var left = Math.Clamp(corners.Min(p => p.X), 0, animation.Width);
            var top = Math.Clamp(corners.Min(p => p.Y), 0, animation.Height);
            var right = Math.Clamp(corners.Max(p => p.X), left, animation.Width);
            var bottom = Math.Clamp(corners.Max(p => p.Y), top, animation.Height);
            if (right <= left || bottom <= top) { left = top = 0; right = animation.Width; bottom = animation.Height; }
            var scale = DesktopScaling;
            return new PixelRect(Position.X + (int)Math.Floor(left * scale), Position.Y + (int)Math.Floor(top * scale),
                (int)Math.Ceiling((right - left) * scale), (int)Math.Ceiling((bottom - top) * scale));
        }
    }

    internal bool AcceptsPointerAt(Point point)
    {
        if (!IsVisible) return false;
        if (down is not null || (interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this) || ContextMenu?.IsOpen == true) return true;
        return PointerOverBody(point);
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

    private void KeepTopmost()
    {
        if (!OperatingSystem.IsWindows() || runtime.DiagnosticMode) return;
        // Raising the pet above its own menu or confirmation would cover them.
        var eligible = IsVisible && !HasPointerInteraction && ContextMenu?.IsOpen != true && !Overlay.HoldsPlacement;
        if (!topmost.Due(topmostClock.Elapsed, eligible)) return;
        WindowsPetWindow.ReassertTopmost(this);
        if (Overlay.Timer.IsVisible) WindowsPetWindow.ReassertTopmost(Overlay.Timer);
        if (Overlay.IsVisible) WindowsPetWindow.ReassertTopmost(Overlay);
    }

    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPoint point);
}
