namespace Unfold.Core;

public sealed record WorkProfile(string Id, string Name, int IntervalMinutes, int IdleMinutes, string RoutineId)
{
    public override string ToString() => $"{Name} · {IntervalMinutes}분마다";
    public void Validate()
    {
        if (!CharacterLibrary.SafeId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 60 ||
            IntervalMinutes is < 5 or > 240 || IdleMinutes is < 1 or > 60 || !CharacterLibrary.SafeId(RoutineId))
            throw new ArgumentException("이름을 입력해 주세요. 알림 간격은 5~240분, 자리 비움 기준은 1~60분이어야 해요.");
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

    public AppSettings SaveRoutine(BreakRoutine routine)
    {
        ValidateUserRoutine(routine);
        if (BreakRoutines.Find(routine.Id) is not null) throw new ArgumentException("기본 루틴은 편집할 수 없어요.");
        var result = routine.Id == BreakRoutines.CustomId
            ? this with { CustomRoutine = routine }
            : this with { AdditionalRoutines = AdditionalRoutines.Where(item => item.Id != routine.Id).Append(routine).ToArray() };
        return (result with { BreakRoutineId = routine.Id, ActiveProfileId = null }).ValidatePersonalization();
    }
    public AppSettings RemoveRoutine(string id)
    {
        if (BreakRoutines.Find(id) is not null) throw new ArgumentException("기본 루틴은 삭제할 수 없어요.");
        var usedBy = WorkProfiles.Where(profile => profile.RoutineId == id).Select(profile => profile.Name).ToArray();
        if (usedBy.Length > 0) throw new ArgumentException($"먼저 다음 프로필에서 사용 중인 루틴을 바꿔 주세요: {string.Join(", ", usedBy)}.");
        return (this with
        {
            CustomRoutine = CustomRoutine?.Id == id ? null : CustomRoutine,
            AdditionalRoutines = AdditionalRoutines.Where(routine => routine.Id != id).ToArray(),
            BreakRoutineId = BreakRoutineId == id ? BreakRoutines.DefaultId : BreakRoutineId
        }).ValidatePersonalization();
    }
    public AppSettings ApplyReminder(int interval, int idle, string routineId)
    {
        new WorkProfile("validation", "설정", interval, idle, routineId).Validate();
        if (!BreakRoutines.ForSettings(this).Any(routine => routine.Id == routineId)) throw new ArgumentException("목록에 있는 루틴을 선택해 주세요.");
        return this with { IntervalMinutes = interval, IdleMinutes = idle, BreakRoutineId = routineId, ActiveProfileId = null };
    }
    public AppSettings SaveProfile(WorkProfile profile)
    {
        profile.Validate();
        return (this with
        {
            WorkProfiles = WorkProfiles.Where(item => item.Id != profile.Id).Append(profile).ToArray(),
            ActiveProfileId = ActiveProfileId == profile.Id ? null : ActiveProfileId
        }).ValidatePersonalization();
    }
    public AppSettings RemoveProfile(string id) => this with
    {
        WorkProfiles = Array.AsReadOnly(WorkProfiles.Where(profile => profile.Id != id).ToArray()),
        ActiveProfileId = ActiveProfileId == id ? null : ActiveProfileId
    };
    public AppSettings ApplyProfile(string id)
    {
        var profile = WorkProfiles.FirstOrDefault(item => item.Id == id) ?? throw new ArgumentException("목록에 있는 프로필을 선택해 주세요.");
        return ApplyReminder(profile.IntervalMinutes, profile.IdleMinutes, profile.RoutineId) with { ActiveProfileId = profile.Id };
    }
}
