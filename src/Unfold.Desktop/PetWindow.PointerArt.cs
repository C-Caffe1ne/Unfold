using Unfold.Core;

namespace Unfold.Desktop;

internal enum PetPointerPhase { None, Pending, Pickup, Held, Recovering }

public sealed partial class PetWindow
{
    private IReadOnlyList<AnimationFrame>[]? pointerClips;
    private (string Key, BreakSession? Session, PetNotice Notice)? deferredPointerReaction;
    internal PetPointerPhase PointerPhase { get; private set; }
    internal bool HasPointerArt => pointerClips is not null && character == runtime.Selected;

    private async Task<IReadOnlyList<AnimationFrame>[]?> LoadPointerArt(CharacterPackage? selected)
    {
        if (selected?.HasPointerArt != true) return null;
        try { return await Task.WhenAll(OriginalCompanion.PointerClips.Select(runtime.Clip)); }
        catch (Exception error) { AppPaths.Log(error); return null; }
    }

    private void SetPointerClip(int index, PetPointerPhase phase)
    {
        PointerPhase = phase; ActiveAnimation = OriginalCompanion.PointerClips[index];
        var frames = pointerClips![index];
        animation.SetRunning(true);
        animation.SetFrames(frames, index == 1, false, true);
    }

    private void BeginPointerArt()
    {
        // Every click begins with pointer-down. Wait for a hold or drag before
        // changing to the curled/hanging artwork.
        PointerPhase = PetPointerPhase.Pending;
        animation.SetPose(PetPose.Neutral);
        _ = RestoreBaseAnimation(InvalidatePlayback());
    }

    private void StartPointerHold()
    {
        if (!pressed || PointerPhase != PetPointerPhase.Pending) return;
        poseSeconds = 0;
        InvalidatePlayback(); SetPointerClip(0, PetPointerPhase.Pickup);
    }

    private void ReleasePointerArt(bool clicked)
    {
        if (clicked && PointerPhase == PetPointerPhase.Pending)
        {
            FinishPointerArt(clicked: true);
            return;
        }
        InvalidatePlayback();
        pressed = false; poseSeconds = 0;
        // Play the assigned release clip immediately, without moving or scaling
        // the canvas and without inserting a held-frame bounce before it.
        SetPointerClip(2, PetPointerPhase.Recovering);
    }

    private void AdvancePointerArt(double seconds)
    {
        if (PointerPhase != PetPointerPhase.Pending) return;
        poseSeconds += seconds;
        if (poseSeconds >= PetPose.LiftDelay) StartPointerHold();
    }

    private void PointerClipCompleted()
    {
        if (PointerPhase == PetPointerPhase.Pickup && pressed)
            SetPointerClip(1, PetPointerPhase.Held);
        else if (PointerPhase == PetPointerPhase.Recovering)
            FinishPointerArt(clicked: false);
    }

    private void FinishPointerArt(bool clicked)
    {
        var deferred = deferredPointerReaction;
        InvalidatePlayback(); CancelCompanionPose();
        if (deferred is { } next && runtime.PresentedReminder.Session == next.Session && runtime.PresentedReminder.Notice == next.Notice)
            _ = React(next.Key);
        else if (clicked) _ = React();
        else _ = RestoreBaseAnimation(generation);
    }

    private bool DeferPointerReaction(string? key)
    {
        if (PointerPhase == PetPointerPhase.None) return false;
        if (key == "stretch" && runtime.Reminder.Notice == PetNotice.Resting)
            originalStretchSession = runtime.Reminder.Session;
        if (key is not (null or "click"))
            deferredPointerReaction = (key, runtime.PresentedReminder.Session, runtime.PresentedReminder.Notice);
        return true;
    }

    private void ResetPointerArt()
    {
        PointerPhase = PetPointerPhase.None; deferredPointerReaction = null;
    }
}
