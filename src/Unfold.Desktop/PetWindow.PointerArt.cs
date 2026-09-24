using Unfold.Core;

namespace Unfold.Desktop;

internal enum PetPointerPhase { None, Pickup, Held, Bouncing, Recovering }

public sealed partial class PetWindow
{
    private IReadOnlyList<AnimationFrame>[]? pointerClips;
    private bool pointerClickPending;
    private (string Key, BreakSession? Session, PetNotice Notice)? deferredPointerReaction;
    internal PetPointerPhase PointerPhase { get; private set; }
    internal bool HasPointerArt => pointerClips is not null && character == runtime.Selected;

    private async Task<IReadOnlyList<AnimationFrame>[]?> LoadPointerArt(CharacterPackage? selected)
    {
        if (selected?.HasPointerArt != true) return null;
        try { return await Task.WhenAll(OriginalCompanion.PointerClips.Select(runtime.Clip)); }
        catch (Exception error) { AppPaths.Log(error); return null; }
    }

    private void SetPointerClip(int index, PetPointerPhase phase, bool firstFrameOnly = false)
    {
        PointerPhase = phase; ActiveAnimation = OriginalCompanion.PointerClips[index];
        var frames = pointerClips![index];
        animation.SetRunning(true);
        animation.SetFrames(firstFrameOnly ? [frames[0]] : frames, index == 1, false, true);
    }

    private void BeginPointerArt()
    {
        pointerClickPending = false;
        InvalidatePlayback(); SetPointerClip(0, PetPointerPhase.Pickup);
    }

    private void ReleasePointerArt(bool clicked)
    {
        pointerClickPending = clicked;
        releasedPose = animation.Pose; pressed = false; releasing = true; poseSeconds = 0;
        // Finish curling even after a very quick click, and keep that exact held
        // silhouette through the bounce. Uncurl only after the final landing.
        SetPointerClip(1, PetPointerPhase.Bouncing, firstFrameOnly: true);
    }

    private void AdvancePointerArt(double seconds)
    {
        poseSeconds += seconds;
        if (PointerPhase is PetPointerPhase.Pickup or PetPointerPhase.Held)
            animation.SetPose(PetPose.Pickup(poseSeconds, pressedPose));
        else if (PointerPhase == PetPointerPhase.Bouncing)
        {
            animation.SetPose(PetPose.BounceOnce(poseSeconds, releasedPose));
            if (poseSeconds >= PetPose.BounceDuration)
            {
                animation.SetPose(PetPose.Neutral);
                SetPointerClip(2, PetPointerPhase.Recovering);
            }
        }
    }

    private void PointerClipCompleted()
    {
        if (PointerPhase == PetPointerPhase.Pickup && pressed)
            SetPointerClip(1, PetPointerPhase.Held);
        else if (PointerPhase == PetPointerPhase.Recovering)
        {
            var click = pointerClickPending; var deferred = deferredPointerReaction;
            CancelCompanionPose();
            if (deferred is { } next && runtime.PresentedReminder.Session == next.Session && runtime.PresentedReminder.Notice == next.Notice)
                _ = React(next.Key);
            else if (click) _ = React();
            else _ = RestoreBaseAnimation(generation);
        }
    }

    private bool DeferPointerReaction(string? key)
    {
        if (PointerPhase == PetPointerPhase.None) return false;
        if (key == "stretch" && runtime.Reminder.Notice == PetNotice.Resting)
            originalStretchSession = runtime.Reminder.Session;
        if (key is null or "click") pointerClickPending = true;
        else deferredPointerReaction = (key, runtime.PresentedReminder.Session, runtime.PresentedReminder.Notice);
        return true;
    }

    private void ResetPointerArt()
    {
        PointerPhase = PetPointerPhase.None; pointerClickPending = false; deferredPointerReaction = null;
    }
}
