namespace Unfold.Core;

/// <summary>Monotonic timestamps are injected. Idle, sleep and manual pause never accrue work time.</summary>
public sealed class StretchClock
{
    public TimeSpan Interval { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool Paused { get; private set; }
    public bool Stopped { get; private set; }
    public bool IdlePaused { get; private set; }
    private TimeSpan last;
    private bool initialized;
    public StretchClock(TimeSpan interval) { SetInterval(interval); }
    public void SetInterval(TimeSpan interval)
    {
        if (interval < TimeSpan.FromMinutes(5) || interval > TimeSpan.FromMinutes(240)) throw new ArgumentOutOfRangeException(nameof(interval));
        Interval = interval; if (!Stopped) Remaining = interval; initialized = false;
    }
    public void Start(TimeSpan now) { if (Stopped) Remaining = Interval; Stopped = false; Paused = false; last = now; initialized = true; }
    public void TogglePause(TimeSpan now)
    {
        if (Paused) Start(now);
        else { Paused = true; last = now; initialized = true; }
    }
    public void Stop(TimeSpan now) { Remaining = TimeSpan.Zero; Stopped = true; Paused = true; last = now; initialized = true; }
    public void Reset(TimeSpan now) { Remaining = Interval; Stopped = false; Paused = true; last = now; initialized = true; }
    public void ScheduleAfterBreak(TimeSpan now, TimeSpan? delay = null)
    {
        var next = delay ?? Interval;
        if (next < TimeSpan.FromSeconds(1) || next > TimeSpan.FromMinutes(240)) throw new ArgumentOutOfRangeException(nameof(delay));
        Remaining = Stopped ? TimeSpan.Zero : next; last = now; initialized = true;
    }
    public bool Tick(TimeSpan now, TimeSpan idleFor, TimeSpan idleThreshold, bool heldForBreak = false)
    {
        if (!initialized) { last = now; initialized = true; return false; }
        var elapsed = now - last; last = now;
        IdlePaused = idleFor >= idleThreshold;
        // A stalled dispatcher / suspend gap is not presumed to be active use.
        if (elapsed < TimeSpan.Zero || elapsed > TimeSpan.FromSeconds(10) || Paused || heldForBreak) return false;
        var idlePart = idleFor > idleThreshold ? idleFor - idleThreshold : TimeSpan.Zero;
        var active = elapsed - (idlePart > elapsed ? elapsed : idlePart);
        Remaining -= active;
        if (Remaining > TimeSpan.Zero) return false;
        Remaining = Interval; return true;
    }
}
