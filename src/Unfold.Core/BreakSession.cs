namespace Unfold.Core;

public sealed record BreakStep(string Instruction, int Seconds);
public sealed record BreakRoutine(string Id, string Name, IReadOnlyList<BreakStep> Steps)
{
    public int DurationSeconds => Steps.Sum(step => step.Seconds);
    public override string ToString() => $"{Name} · {DurationSeconds}s";
    public void Validate()
    {
        if (!CharacterLibrary.SafeId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 60 || Steps is null ||
            Steps.Count is < 1 or > 12 || Steps.Any(step => step is null || step.Seconds is < 1 or > 300 ||
                string.IsNullOrWhiteSpace(step.Instruction) || step.Instruction.Length > 180) || DurationSeconds > 600)
            throw new ArgumentException("Use a name, 1–12 short steps, and a total time of at most 10 minutes.");
    }
}

public static class BreakRoutines
{
    public const string DefaultId = "small-reset";
    public const string CustomId = "my-routine";
    public static IReadOnlyList<BreakRoutine> All { get; } = Array.AsReadOnly(new[]
    {
        new BreakRoutine(DefaultId, "Small reset", Array.AsReadOnly(new[]
        {
            new BreakStep("Let go of the mouse and relax your shoulders.", 20),
            new BreakStep("Look away from the screen and take an easy breath.", 20),
            new BreakStep("Move gently in a way that feels comfortable.", 20)
        })),
        new BreakRoutine("look-away", "Look away", Array.AsReadOnly(new[]
        {
            new BreakStep("Let your eyes wander away from the screen.", 20)
        })),
        new BreakRoutine("room-to-move", "Room to move", Array.AsReadOnly(new[]
        {
            new BreakStep("Set your work aside for a moment.", 30),
            new BreakStep("Stand up or change position if that feels comfortable.", 30),
            new BreakStep("Take your time before returning to work.", 30)
        }))
    });
    public static BreakRoutine? Find(string id) => All.FirstOrDefault(routine => routine.Id == id);
    public static IReadOnlyList<BreakRoutine> ForSettings(AppSettings settings) => Array.AsReadOnly(
        All.Concat(settings.CustomRoutine is { } custom ? [custom] : Array.Empty<BreakRoutine>()).Concat(settings.AdditionalRoutines).ToArray());
}

public enum BreakSessionState { Ready, InProgress, AwaitingConfirmation, Completed, Snoozed, Skipped }

/// <summary>Elapsed time is monotonic; sleep and stalled UI gaps cannot complete a break.</summary>
public sealed class BreakSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public BreakRoutine Routine { get; }
    public string CharacterId { get; }
    public string? ProfileId { get; }
    public string? ProfileName { get; }
    public BreakSessionState State { get; private set; }
    public TimeSpan Elapsed { get; private set; }
    public TimeSpan Remaining => TimeSpan.FromSeconds(Routine.DurationSeconds) - Elapsed;
    public bool IsTerminal => State is BreakSessionState.Completed or BreakSessionState.Snoozed or BreakSessionState.Skipped;
    private TimeSpan last;
    public BreakSession(BreakRoutine routine, string characterId, WorkProfile? profile = null)
    {
        routine.Validate();
        if (!CharacterLibrary.SafeId(characterId)) throw new ArgumentException("Invalid character.");
        if (profile is not null)
        {
            profile.Validate();
            if (profile.RoutineId != routine.Id) throw new ArgumentException("The profile must use this routine.");
            ProfileId = profile.Id; ProfileName = profile.Name;
        }
        Routine = routine with { Steps = Array.AsReadOnly(routine.Steps.ToArray()) }; CharacterId = characterId;
    }
    public BreakStep CurrentStep
    {
        get
        {
            var seconds = Elapsed.TotalSeconds;
            foreach (var step in Routine.Steps) { if (seconds < step.Seconds) return step; seconds -= step.Seconds; }
            return Routine.Steps[^1];
        }
    }
    public bool Start(TimeSpan now)
    {
        if (State != BreakSessionState.Ready) return false;
        last = now; State = BreakSessionState.InProgress; return true;
    }
    public void Tick(TimeSpan now)
    {
        if (State != BreakSessionState.InProgress || now < last) return;
        var delta = now - last; last = now;
        if (delta > TimeSpan.FromSeconds(10)) return;
        Elapsed += delta > Remaining ? Remaining : delta;
        if (Remaining == TimeSpan.Zero) State = BreakSessionState.AwaitingConfirmation;
    }
    public bool Complete()
    {
        if (State != BreakSessionState.AwaitingConfirmation) return false;
        State = BreakSessionState.Completed; return true;
    }
    public bool Snooze() => Finish(BreakSessionState.Snoozed);
    public bool Skip() => Finish(BreakSessionState.Skipped);
    private bool Finish(BreakSessionState state)
    {
        if (IsTerminal) return false;
        State = state; return true;
    }
}
