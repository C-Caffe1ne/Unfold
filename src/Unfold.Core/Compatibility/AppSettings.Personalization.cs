namespace Unfold.Core;

// Compatibility only: retain saved routine/profile data without restoring authoring UI.
public sealed record WorkProfile(string Id, string Name, int IntervalMinutes, int IdleMinutes, string RoutineId)
{
    public override string ToString() => $"{Name} · {IntervalMinutes}분마다";
    public void Validate()
    {
        if (!CharacterLibrary.SafeId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 60 ||
            IntervalMinutes is < 1 or > 240 || IdleMinutes is < 1 or > 60 || !CharacterLibrary.SafeId(RoutineId))
            throw new ArgumentException("이름을 입력해 주세요. 스트레칭 시간은 1~240분, 자리 비움 시간은 1~60분이어야 해요.");
    }
}

public sealed partial record AppSettings
{
    public const int MaxAdditionalRoutines = 19;
    public const int MaxWorkProfiles = 10;

    public AppSettings ValidatePersonalization()
    {
        if (AdditionalRoutines is null || WorkProfiles is null || AdditionalRoutines.Count > MaxAdditionalRoutines || WorkProfiles.Count > MaxWorkProfiles)
            throw new ArgumentException("내 루틴은 최대 20개, 업무 프로필은 최대 10개까지 저장할 수 있어요.");
        var ids = new HashSet<string>(BreakRoutines.All.Select(routine => routine.Id), StringComparer.OrdinalIgnoreCase) { BreakRoutines.CustomId };
        if (CustomRoutine is { } primary)
        {
            ValidateUserRoutine(primary);
            if (primary.Id != BreakRoutines.CustomId) throw new ArgumentException("내 루틴 정보를 확인해 주세요.");
        }
        foreach (var routine in AdditionalRoutines)
        {
            ValidateUserRoutine(routine);
            if (!ids.Add(routine.Id)) throw new ArgumentException("같은 루틴이 중복되어 있어요.");
        }
        var routineIds = BreakRoutines.ForSettings(this).Select(routine => routine.Id).ToHashSet(StringComparer.Ordinal);
        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in WorkProfiles)
        {
            if (profile is null) throw new ArgumentException("업무 프로필이 없어요.");
            profile.Validate();
            if (!profileIds.Add(profile.Id) || !routineIds.Contains(profile.RoutineId))
                throw new ArgumentException("프로필이 중복되었거나 연결된 루틴이 없어요.");
        }
        return this with
        {
            CustomRoutine = CustomRoutine is { } custom ? Snapshot(custom) : null,
            AdditionalRoutines = AdditionalRoutines.Count == 0 ? Array.Empty<BreakRoutine>() : Array.AsReadOnly(AdditionalRoutines.Select(Snapshot).ToArray()),
            WorkProfiles = WorkProfiles.Count == 0 ? Array.Empty<WorkProfile>() : Array.AsReadOnly(WorkProfiles.ToArray())
        };
    }
    private static void ValidateUserRoutine(BreakRoutine? routine)
    {
        if (routine is null) throw new ArgumentException("루틴이 없어요.");
        routine.Validate();
        if (routine.Steps.Count > 3) throw new ArgumentException("내 루틴은 최대 3단계로 만들어 주세요.");
    }
    private static BreakRoutine Snapshot(BreakRoutine routine) => routine with { Steps = Array.AsReadOnly(routine.Steps.ToArray()) };
}
