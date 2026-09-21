namespace Unfold.Core;

public enum BubbleDirection { Top, Bottom, Left, Right }
public enum PetNotice { None, Advance, Invitation, Resting, Completed }

/// <summary>The reminder owns break state independently from the pet window that presents it.</summary>
public sealed class PetReminder
{
    public static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(5);
    public PetNotice Notice { get; private set; }
    public BreakSession? Session { get; private set; }
    public int CompletedSeconds { get; private set; }
    public bool HasNotice => Notice != PetNotice.None;
    public TimeSpan? NoticeExpiresAt => Notice is PetNotice.Advance or PetNotice.Completed ? expires : null;
    public event Action<BreakSession>? Started;
    public event Action<BreakSession>? Finished;
    private TimeSpan expires;

    public void ShowAdvance(TimeSpan now)
    {
        if (Session is not null || Notice == PetNotice.Completed) return;
        Notice = PetNotice.Advance; expires = now + NoticeDuration;
    }
    public bool Invite(BreakSession session)
    {
        if (Session is not null || session.State != BreakSessionState.Ready) return false;
        Session = session; Notice = PetNotice.Invitation; return true;
    }
    public void Tick(TimeSpan now)
    {
        Session?.Tick(now);
        if (Notice is PetNotice.Advance or PetNotice.Completed && now >= expires) Notice = PetNotice.None;
    }
    public bool Start(TimeSpan now)
    {
        if (Session is not { } session || !session.Start(now)) return false;
        Notice = PetNotice.Resting; Started?.Invoke(session); return true;
    }
    public bool Complete(TimeSpan now)
    {
        if (Session is not { } session) return false;
        session.Tick(now);
        if (!session.Complete()) return false;
        CompletedSeconds = (int)Math.Ceiling(session.Elapsed.TotalSeconds);
        Session = null; Notice = PetNotice.Completed; expires = now + NoticeDuration;
        Finished?.Invoke(session); return true;
    }
    public bool Snooze()
    {
        if (Session is not { State: BreakSessionState.Ready } session || !session.Snooze()) return false;
        Session = null; Notice = PetNotice.None; Finished?.Invoke(session); return true;
    }
    public void Cancel()
    {
        var session = Session;
        Session = null; Notice = PetNotice.None;
        if (session is not null && session.Skip()) Finished?.Invoke(session);
    }
    public static string TimerText(BreakSession session)
    {
        var overtime = session.Remaining < TimeSpan.Zero;
        var seconds = overtime ? (int)session.Overtime.TotalSeconds : (int)Math.Ceiling(session.Remaining.TotalSeconds);
        return $"{(overtime ? "+" : "")}{seconds / 60:00}:{seconds % 60:00}";
    }
}
