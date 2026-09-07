namespace Unfold.Core;

/// <summary>Monotonic timestamps are injected. Idle, sleep and manual pause never accrue work time.</summary>
public sealed class StretchClock
{
    public TimeSpan Interval { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool Paused { get; private set; }
    public bool IdlePaused { get; private set; }
    private TimeSpan last;
    private bool initialized;
    public StretchClock(TimeSpan interval) { SetInterval(interval); }
    public void SetInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromMinutes(5) || interval > TimeSpan.FromMinutes(240)) throw new ArgumentOutOfRangeException(nameof(interval));
        Interval = Remaining = interval; initialized = false;
    }
    public void TogglePause(TimeSpan now) { Paused = !Paused; last = now; initialized = true; }
    public void Reset(TimeSpan now) { Remaining = Interval; Paused = false; last = now; initialized = true; }
    public void ResumeFromSleep(TimeSpan now) { last = now; initialized = true; }
    public bool Tick(TimeSpan now, TimeSpan idleFor, TimeSpan idleThreshold)
    {
        if (!initialized) { last = now; initialized = true; return false; }
        var elapsed = now - last; last = now;
        IdlePaused = idleFor >= idleThreshold;
        // A stalled dispatcher / suspend gap is not presumed to be active use.
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10) || Paused) return false;
        var idlePart = idleFor > idleThreshold ? idleFor - idleThreshold : TimeSpan.Zero;
        var active = elapsed - (idlePart > elapsed ? elapsed : idlePart);
        Remaining -= active;
        if (Remaining > TimeSpan.Zero) return false;
        Remaining = Interval; return true;
    }
}
