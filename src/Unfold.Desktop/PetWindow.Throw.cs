using System.Diagnostics;
using Avalonia;

namespace Unfold.Desktop;

public sealed partial class PetWindow
{
    private readonly PetThrowMotion throwMotion = new();
    private readonly Stopwatch motionStopwatch = Stopwatch.StartNew();
    private bool springDrag;
    internal Func<TimeSpan>? MotionClock { get; set; }
    internal Func<bool> ReduceMotion { get; set; } = MotionPreference.IsReduced;
    internal bool IsThrown => throwMotion.IsFlying;
    private TimeSpan MotionNow => MotionClock?.Invoke() ?? motionStopwatch.Elapsed;
    // Diagnostics compare exact window positions; reduce-motion keeps the original direct drag.
    private bool MotionAllowed => !runtime.DiagnosticMode && !ReduceMotion();

    /// <summary>The window top-left range on the pet's screen; Right/Bottom are the largest X/Y.</summary>
    internal PixelRect ThrowBounds()
    {
        var screen = (OperatingSystem.IsMacOS() ? Screens.ScreenFromPoint(PetAnchor) : null)
            ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        var area = screen is null ? new PixelRect(Position, new PixelSize(1, 1))
            : PlacementArea(screen.Bounds, screen.WorkingArea, OperatingSystem.IsMacOS());
        var width = (int)Math.Ceiling(Width * DesktopScaling); var height = (int)Math.Ceiling(Height * DesktopScaling);
        return new(area.X, area.Y, Math.Max(0, area.Width - width), Math.Max(0, area.Height - height));
    }

    private void BeginThrowTracking(PixelPoint pointer)
    {
        throwMotion.Cancel();
        springDrag = MotionAllowed;
        throwMotion.Begin(PetAnchor, new(pointer.X, pointer.Y), MotionNow);
    }

    private void TrackDrag(PixelPoint pointer, PixelPoint? target)
    {
        throwMotion.Track(target ?? throwMotion.Target, new(pointer.X, pointer.Y), MotionNow);
        if (target is { } direct && !springDrag) Position = direct;
    }

    /// <summary>Returns true when the release became a throw; the rest of the release runs after landing.</summary>
    private bool ReleaseDrag(bool dragged)
    {
        if (dragged && springDrag && MotionAllowed && throwMotion.Release(MotionNow, DesktopScaling)) return true;
        throwMotion.Settle();
        if (dragged) Position = throwMotion.Target;
        return false;
    }

    internal void AdvanceThrow(double seconds)
    {
        if (down is not null)
        {
            if (springDrag && dragging) Position = throwMotion.Follow(seconds);
            return;
        }
        if (!throwMotion.IsFlying) return;
        var (position, landed) = throwMotion.Fly(ThrowBounds(), seconds, DesktopScaling);
        Position = position;
        if (landed) _ = CompleteRelease(clicked: false);
    }

    private async Task CompleteRelease(bool clicked)
    {
        ClampPosition(); runtime.SavePosition(PetAnchor);
        var handled = ReleaseCompanionPress(clicked);
        RefreshSpeech();
        if (!HasOriginalBehavior && runtime.Selected?.Manifest.Animations.ContainsKey("pointerUp") == true) await React("pointerUp");
        else
        {
            if (!HasOriginalBehavior && ActiveAnimation == "pointerDown") await ResumeMediaPointerReaction(InvalidatePlayback());
            if (clicked && !handled) await React();
        }
    }
}
