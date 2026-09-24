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
    private bool reacting, pressed, releasing, landing;
    private double poseSeconds;
    private PetPose pressedPose, releasedPose;
    private long lastCompanionTick = Stopwatch.GetTimestamp();
    internal bool HasOriginalBehavior => runtime.Selected?.HasOriginalBehavior == true;
    internal string ActiveAnimation { get; private set; } = "idle";
    internal bool IsRoaming => HasOriginalBehavior && walkingSession is not null &&
        runtime.Reminder.Session == walkingSession && runtime.Reminder.Notice == PetNotice.Resting;

    private void ResetCompanion()
    {
        down = null; dragging = false; pressedPointer?.Capture(null); pressedPointer = null;
        walkingSession = originalStretchSession = null; idleSchedule.Reset(); wander.Reset(); CancelCompanionPose();
        lastCompanionTick = Stopwatch.GetTimestamp();
    }
    internal void BeginCompanionPress()
    {
        if (!HasOriginalBehavior) return;
        // Wake a sleeping pet immediately. Pointer motion is applied to the image, never its window.
        pressedPose = animation.Pose;
        pressed = true; releasing = landing = false; poseSeconds = 0; idleSchedule.Reset();
        if (HasPointerArt) { BeginPointerArt(); return; }
        var current = InvalidatePlayback(); _ = RestoreBaseAnimation(current);
    }
    internal bool ReleaseCompanionPress(bool clicked)
    {
        if (!HasOriginalBehavior) { CancelCompanionPose(); return false; }
        if (!pressed) return false;
        if (HasPointerArt) { ReleasePointerArt(clicked); return true; }
        landing = !clicked || poseSeconds > PetPose.LiftDelay;
        releasedPose = animation.Pose; pressed = false; releasing = true; poseSeconds = 0;
        if (!clicked) _ = RestoreBaseAnimation(generation);
        return false;
    }
    private void CancelCompanionPress()
    {
        CancelCompanionPose();
        if (HasOriginalBehavior) _ = RestoreBaseAnimation(generation);
    }
    private void CancelCompanionPose()
    {
        ResetPointerArt();
        pressed = releasing = landing = false; poseSeconds = 0; animation.SetPose(PetPose.Neutral);
    }
    private async Task RestoreBaseAnimation(int current, bool idleOnly = false)
    {
        try
        {
            if (current != generation || !IsVisible) return;
            var selected = runtime.Selected;
            if (!idleOnly && originalStretchSession is not null && originalStretchSession == runtime.Reminder.Session &&
                runtime.Reminder.Notice == PetNotice.Resting && !pressed)
            { await React("stretch"); return; }
            var key = !idleOnly && IsRoaming ? "walk" : "idle";
            var frames = await runtime.Clip(key);
            if (current != generation || !IsVisible) return;
            reacting = false; ActiveAnimation = key;
            animation.SetRunning(true);
            animation.SetFrames(frames, true, selected?.Manifest.RenderStyle == "pixel", selected?.HasOriginalBehavior == true);
            if (key != "walk") wander.Reset();
        }
        catch (Exception error)
        {
            AppPaths.Log(error);
            if (current != generation || !IsVisible) return;
            reacting = false; walkingSession = originalStretchSession = null;
            ActiveAnimation = "idle"; wander.Reset(); CancelCompanionPose();
            // A broken walk clip may still have a healthy idle. Bound the fallback
            // so a broken idle cannot recurse or keep a completed reaction active.
            if (!idleOnly) await RestoreBaseAnimation(current, idleOnly: true);
            else if (runtime.PresentedReminder.HasNotice && runtime.Selected is { } selected)
                ShowReminderFallback(selected);
            else animation.SetFrames([], true);
        }
    }
    private void RefreshCompanionContext()
    {
        if (walkingSession is not null && !IsRoaming)
        {
            walkingSession = null; wander.Reset();
            if (PointerPhase == PetPointerPhase.None) animation.SetPose(PetPose.Neutral);
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
        if (PointerPhase != PetPointerPhase.None) AdvancePointerArt(seconds);
        else if (pressed || releasing)
        {
            poseSeconds += seconds;
            animation.SetPose(pressed ? PetPose.Hold(poseSeconds, pressedPose)
                : landing ? PetPose.Land(poseSeconds, releasedPose) : PetPose.Release(poseSeconds, releasedPose));
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
