using Unfold.Core;

namespace Unfold.Desktop;

internal enum PetPointerPhase { None, Pending, Pickup, Held, Recovering }

public sealed partial class PetWindow
{
    private IReadOnlyList<AnimationFrame>[]? pointerClips;
    private (string Key, PetReminder Source, BreakSession? Session, PetNotice Notice)? deferredPointerReaction;
    internal PetPointerPhase PointerPhase { get; private set; }
    internal bool HasPointerArt => pointerClips is not null && character == runtime.Selected;

    private async Task<IReadOnlyList<AnimationFrame>[]?> LoadPointerArt(CharacterPackage? selected)
    {
        if (selected?.HasPointerArt != true) return null;
        var keys = selected.IsGlb ? GlbPetDraft.PointerClips : OriginalCompanion.PointerClips;
        try { return await Task.WhenAll(keys.Select(runtime.Clip)); }
        catch (Exception error) { AppPaths.Log(error); return null; }
    }

    private void SetPointerClip(int index, PetPointerPhase phase)
    {
        PointerPhase = phase;
        var keys = runtime.Selected!.IsGlb ? GlbPetDraft.PointerClips : OriginalCompanion.PointerClips;
        ActiveAnimation = keys[index];
        var frames = pointerClips![index];
        animation.SetRunning(true);
        var definition = runtime.Selected!.Manifest.Animations[ActiveAnimation];
        var repeat = definition.Loop && pressed && (ActiveAnimation == "held" ||
            runtime.Selected.IsGlb && ActiveAnimation == "pickup");
        // GLB has one lift clip rather than a separate held clip. Repeating it
        // will not emit Completed, so enter its held state immediately.
        if (repeat && runtime.Selected.IsGlb) PointerPhase = PetPointerPhase.Held;
        animation.SetFrames(frames, repeat, false, true, definition.PingPong);
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
        SetPointerClip(pointerClips!.Length - 1, PetPointerPhase.Recovering);
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
        {
            if (runtime.Selected?.IsGlb == true) PointerPhase = PetPointerPhase.Held;
            else SetPointerClip(1, PetPointerPhase.Held);
        }
        else if (PointerPhase == PetPointerPhase.Recovering)
            FinishPointerArt(clicked: false);
        else if (PointerPhase == PetPointerPhase.None && ActiveAnimation == "walk")
        {
            walkingSession = null; wander.Reset(); _ = RestoreBaseAnimation(InvalidatePlayback());
        }
    }

    private void FinishPointerArt(bool clicked)
    {
        var deferred = deferredPointerReaction;
        InvalidatePlayback(); CancelCompanionPose();
        if (deferred is { } next && runtime.PresentedReminder == next.Source && runtime.PresentedReminder.Session == next.Session && runtime.PresentedReminder.Notice == next.Notice)
            _ = React(next.Key);
        else if (clicked && runtime.Selected?.Manifest.Animations.ContainsKey("click") == true) _ = React();
        else _ = RestoreBaseAnimation(generation);
    }

    private bool DeferPointerReaction(string? key)
    {
        var mediaPointer = down is not null || reacting && ActiveAnimation == "pointerUp";
        if (PointerPhase == PetPointerPhase.None && !mediaPointer || key == "pointerDown") return false;
        if (key == "stretch" && runtime.Reminder.Notice == PetNotice.Resting)
            originalStretchSession = runtime.Reminder.Session;
        if (key is not (null or "click"))
            deferredPointerReaction = (key, runtime.PresentedReminder, runtime.PresentedReminder.Session, runtime.PresentedReminder.Notice);
        return true;
    }

    private async Task ResumeMediaPointerReaction(int current)
    {
        var deferred = deferredPointerReaction; deferredPointerReaction = null;
        if (current != generation || !IsVisible) return;
        reacting = false;
        if (deferred is { } next && runtime.PresentedReminder == next.Source && runtime.PresentedReminder.Session == next.Session && runtime.PresentedReminder.Notice == next.Notice)
            _ = React(next.Key);
        else await RestoreBaseAnimation(current);
    }

    private void ResetPointerArt()
    {
        PointerPhase = PetPointerPhase.None; deferredPointerReaction = null;
    }
}
