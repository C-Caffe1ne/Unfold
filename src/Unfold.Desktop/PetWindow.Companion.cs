using System.Diagnostics;
using Avalonia;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class PetWindow
{
    private readonly CompanionIdleSchedule idleSchedule = new(Random.Shared);
    private readonly PetWanderMotion wander = new(Random.Shared);
    private BreakSession? walkingSession;
    private BreakSession? originalStretchSession;
    private bool reacting, pressed, releasing;
    private double poseSeconds;
    private PetPose releasedPose;
    private long lastCompanionTick = Stopwatch.GetTimestamp();
    internal bool HasOriginalBehavior => runtime.Selected?.HasOriginalBehavior == true;
    internal string ActiveAnimation { get; private set; } = "idle";
    internal bool IsRoaming => HasOriginalBehavior && walkingSession is not null &&
        runtime.Reminder.Session == walkingSession && runtime.Reminder.Notice == PetNotice.Resting;

    private void ResetCompanion()
    {
        walkingSession = originalStretchSession = null; idleSchedule.Reset(); wander.Reset(); CancelCompanionPose();
        lastCompanionTick = Stopwatch.GetTimestamp();
    }
    internal void BeginCompanionPress()
    {
        if (!HasOriginalBehavior) return;
        // Wake a sleeping pet immediately. Pointer motion is applied to the image, never its window.
        pressed = true; releasing = false; poseSeconds = 0; idleSchedule.Reset();
        var current = InvalidatePlayback(); _ = RestoreBaseAnimation(current);
    }
    internal void ReleaseCompanionPress(bool clicked)
    {
        if (!HasOriginalBehavior) { CancelCompanionPose(); return; }
        if (!clicked)
        {
            CancelCompanionPose();
            _ = RestoreBaseAnimation(generation);
            return;
        }
        releasedPose = animation.Pose; pressed = false; releasing = true; poseSeconds = 0;
    }
    private void CancelCompanionPose()
    {
        pressed = releasing = false; poseSeconds = 0; animation.SetPose(PetPose.Neutral);
    }
    private async Task RestoreBaseAnimation(int current)
    {
        try
        {
            if (current != generation || !IsVisible) return;
            var selected = runtime.Selected;
            if (originalStretchSession is not null && originalStretchSession == runtime.Reminder.Session &&
                runtime.Reminder.Notice == PetNotice.Resting && !pressed)
            { await React("stretch"); return; }
            var key = IsRoaming ? "walk" : "idle";
            var frames = await runtime.Clip(key);
            if (current != generation || !IsVisible) return;
            reacting = false; ActiveAnimation = key;
            animation.SetRunning(true);
            animation.SetFrames(frames, true, selected?.Manifest.RenderStyle == "pixel", selected?.HasOriginalBehavior == true);
            if (key != "walk") wander.Reset();
        }
        catch (Exception error) { AppPaths.Log(error); }
    }
    private void RefreshCompanionContext()
    {
        if (walkingSession is not null && !IsRoaming)
        {
            walkingSession = null; wander.Reset(); animation.SetPose(PetPose.Neutral);
            if (ActiveAnimation == "walk") _ = RestoreBaseAnimation(InvalidatePlayback());
        }
        // Stop/cancel can occur before the one-shot stretch finishes.
        if (originalStretchSession is not null && (originalStretchSession != runtime.Reminder.Session || runtime.Reminder.Notice != PetNotice.Resting))
        {
            originalStretchSession = null;
            if (ActiveAnimation == "stretch") _ = RestoreBaseAnimation(InvalidatePlayback());
        }
    }
    private void TickCompanion()
    {
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(lastCompanionTick, now).TotalSeconds;
        lastCompanionTick = now;
        AdvanceCompanion(seconds);
    }
    internal void AdvanceCompanion(double seconds)
    {
        if (!HasOriginalBehavior || !IsVisible || !double.IsFinite(seconds) || seconds <= 0 || seconds > .25) return;
        if (pressed || releasing)
        {
            poseSeconds += seconds;
            animation.SetPose(pressed ? PetPose.Press(poseSeconds) : PetPose.Release(poseSeconds, releasedPose));
            if (releasing && poseSeconds >= .44) { releasing = false; animation.SetPose(PetPose.Neutral); }
        }
        var blocked = down is not null || pressed || releasing || reacting || IsPointerOver || ContextMenu?.IsOpen == true;
        if (IsRoaming && ActiveAnimation == "walk")
        {
            animation.SetRunning(!blocked);
            if (!blocked && !runtime.DiagnosticMode)
            {
                var work = Screens.ScreenFromWindow(this)?.WorkingArea ?? Screens.Primary?.WorkingArea;
                if (work is { } area)
                {
                    Position = wander.Step(Position, layout.Size, area, DesktopScaling, seconds);
                    animation.SetPose(PetPose.Neutral, wander.FacingLeft);
                }
            }
        }
        else if (!runtime.DiagnosticMode && idleSchedule.Tick(seconds,
            !blocked && !runtime.PresentedReminder.HasNotice && ActiveAnimation == "idle") is { } key)
            _ = React(key);
    }
}
