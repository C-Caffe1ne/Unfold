namespace Unfold.Desktop;

/// <summary>Monotonic reveal timing, independent of the work clock and pet playback.</summary>
internal sealed class PetTimerReveal
{
    internal static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(180);
    private TimeSpan transitionAt;
    private double from, target;
    private bool suppressed;
    internal double Opacity { get; private set; }
    internal bool IsSuppressed => suppressed;

    internal void Update(bool petHovered, bool timerHovered, TimeSpan now, bool petInteracting = false)
    {
        if (petInteracting) { SuppressUntilPointerExit(); return; }
        if (suppressed)
        {
            if (!petHovered && !timerHovered) suppressed = false;
            return;
        }
        Advance(now);
        Transition(petHovered || timerHovered ? 1 : 0, now);
        Advance(now);
    }
    private void Transition(double value, TimeSpan now)
    {
        if (target == value) return;
        from = Opacity; target = value; transitionAt = now;
    }
    private void Advance(TimeSpan now)
    {
        var progress = Math.Clamp((now - transitionAt).TotalMilliseconds / FadeDuration.TotalMilliseconds, 0, 1);
        // Smoothstep preserves continuity when the cursor returns during a fade.
        var eased = progress * progress * (3 - 2 * progress);
        Opacity = from + (target - from) * eased;
    }
    internal void Reset()
    {
        from = target = Opacity = 0; transitionAt = default; suppressed = false;
    }
    internal void SuppressUntilPointerExit() { Reset(); suppressed = true; }
}
