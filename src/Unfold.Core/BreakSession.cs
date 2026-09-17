namespace Unfold.Core;

public sealed record BreakStep(string Instruction, int Seconds);
public sealed record BreakRoutine(string Id, string Name, IReadOnlyList<BreakStep> Steps)
{
    public int DurationSeconds => Steps.Sum(step => step.Seconds);
    public override string ToString() => $"{Name} · {DurationSeconds}초";
    public void Validate()
    {
        if (!CharacterLibrary.SafeId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 60 || Steps is null ||
            Steps.Count is < 1 or > 12 || Steps.Any(step => step is null || step.Seconds is < 1 or > 300 ||
                string.IsNullOrWhiteSpace(step.Instruction) || step.Instruction.Length > 180) || DurationSeconds > 600)
            throw new ArgumentException("이름과 단계별 안내를 입력해 주세요. 각 단계는 1~300초, 전체 시간은 10분 이내여야 해요.");
    }
}

public static class BreakRoutines
{
    public const string DefaultId = "small-reset";
    public const string CustomId = "my-routine";
    public static IReadOnlyList<BreakRoutine> All { get; } = Array.AsReadOnly(new[]
    {
        new BreakRoutine(DefaultId, "잠깐의 여유", Array.AsReadOnly(new[]
        {
            new BreakStep("마우스에서 손을 떼고 어깨의 힘을 풀어 보세요.", 20),
            new BreakStep("화면에서 눈을 떼고 편안하게 숨을 쉬어 보세요.", 20),
            new BreakStep("편안한 범위에서 몸을 가볍게 움직여 보세요.", 20)
        })),
        new BreakRoutine("look-away", "눈 쉬어 주기", Array.AsReadOnly(new[]
        {
            new BreakStep("화면에서 벗어나 다른 곳을 바라보세요.", 20)
        })),
        new BreakRoutine("room-to-move", "몸 풀어 주기", Array.AsReadOnly(new[]
        {
            new BreakStep("하던 일을 잠시 내려놓아 보세요.", 30),
            new BreakStep("편하다면 일어나거나 자세를 바꿔 보세요.", 30),
            new BreakStep("충분히 쉬고 천천히 작업으로 돌아가세요.", 30)
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
    public int DurationSeconds { get; }
    public string CharacterId { get; }
    public string? ProfileId { get; }
    public string? ProfileName { get; }
    public BreakSessionState State { get; private set; }
    public TimeSpan Elapsed { get; private set; }
    public static readonly TimeSpan MaximumOvertime = TimeSpan.FromMinutes(60);
    public TimeSpan Overtime => Elapsed > TimeSpan.FromSeconds(DurationSeconds) ? -Remaining : TimeSpan.Zero;
    public TimeSpan Remaining => TimeSpan.FromSeconds(DurationSeconds) - Elapsed;
    public bool IsTerminal => State is BreakSessionState.Completed or BreakSessionState.Snoozed or BreakSessionState.Skipped;
    private TimeSpan last;
    public BreakSession(BreakRoutine routine, string characterId, WorkProfile? profile = null, int? durationSeconds = null)
    {
        routine.Validate();
        DurationSeconds = durationSeconds ?? routine.DurationSeconds;
        if (DurationSeconds is < 1 or > 600) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
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
            var seconds = Elapsed.TotalSeconds * Routine.DurationSeconds / DurationSeconds;
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
        if (State is not (BreakSessionState.InProgress or BreakSessionState.AwaitingConfirmation) || now < last) return;
        var delta = now - last; last = now;
        if (delta > TimeSpan.FromSeconds(10)) return;
        var available = TimeSpan.FromSeconds(DurationSeconds) + MaximumOvertime - Elapsed;
        Elapsed += delta > available ? available : delta;
        if (Remaining <= TimeSpan.Zero) State = BreakSessionState.AwaitingConfirmation;
    }
    public bool Complete()
    {
        if (State is not (BreakSessionState.InProgress or BreakSessionState.AwaitingConfirmation)) return false;
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
