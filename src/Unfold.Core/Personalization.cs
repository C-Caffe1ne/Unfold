namespace Unfold.Core;

public sealed record WorkProfile(string Id, string Name, int IntervalMinutes, int IdleMinutes, string RoutineId)
{
    public override string ToString() => $"{Name} · every {IntervalMinutes} min";
    public void Validate()
    {
        if (!CharacterLibrary.SafeId(Id) || string.IsNullOrWhiteSpace(Name) || Name.Length > 60 ||
            IntervalMinutes is < 5 or > 240 || IdleMinutes is < 1 or > 60 || !CharacterLibrary.SafeId(RoutineId))
            throw new ArgumentException("Use a name, a 5–240 minute interval, and a 1–60 minute away threshold.");
    }
}

public sealed partial record AppSettings
{
    public const int MaxAdditionalRoutines = 19;
    public const int MaxWorkProfiles = 10;

    public AppSettings ValidatePersonalization()
    {
        if (AdditionalRoutines is null || WorkProfiles is null || AdditionalRoutines.Count > MaxAdditionalRoutines || WorkProfiles.Count > MaxWorkProfiles)
            throw new ArgumentException("Keep up to 20 custom routines and 10 work profiles.");
        var ids = new HashSet<string>(BreakRoutines.All.Select(routine => routine.Id), StringComparer.OrdinalIgnoreCase) { BreakRoutines.CustomId };
        if (CustomRoutine is { } primary)
        {
            ValidateUserRoutine(primary);
            if (primary.Id != BreakRoutines.CustomId) throw new ArgumentException("Invalid personal routine ID.");
        }
        foreach (var routine in AdditionalRoutines)
        {
            ValidateUserRoutine(routine);
            if (!ids.Add(routine.Id)) throw new ArgumentException("Each routine needs its own ID.");
        }
        var routineIds = BreakRoutines.ForSettings(this).Select(routine => routine.Id).ToHashSet(StringComparer.Ordinal);
        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in WorkProfiles)
        {
            if (profile is null) throw new ArgumentException("Missing work profile.");
            profile.Validate();
            if (!profileIds.Add(profile.Id) || !routineIds.Contains(profile.RoutineId))
                throw new ArgumentException("Each work profile needs its own ID and an existing routine.");
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
        if (routine is null) throw new ArgumentException("Missing routine.");
        routine.Validate();
        if (routine.Steps.Count > 3) throw new ArgumentException("Use up to three steps in a custom routine.");
    }
    private static BreakRoutine Snapshot(BreakRoutine routine) => routine with { Steps = Array.AsReadOnly(routine.Steps.ToArray()) };

    public AppSettings SaveRoutine(BreakRoutine routine)
    {
        ValidateUserRoutine(routine);
        if (BreakRoutines.Find(routine.Id) is not null) throw new ArgumentException("Built-in routines cannot be edited.");
        var result = routine.Id == BreakRoutines.CustomId
            ? this with { CustomRoutine = routine }
            : this with { AdditionalRoutines = AdditionalRoutines.Where(item => item.Id != routine.Id).Append(routine).ToArray() };
        return (result with { BreakRoutineId = routine.Id, ActiveProfileId = null }).ValidatePersonalization();
    }
    public AppSettings RemoveRoutine(string id)
    {
        if (BreakRoutines.Find(id) is not null) throw new ArgumentException("Built-in routines cannot be removed.");
        var usedBy = WorkProfiles.Where(profile => profile.RoutineId == id).Select(profile => profile.Name).ToArray();
        if (usedBy.Length > 0) throw new ArgumentException($"Change the routine in these profiles first: {string.Join(", ", usedBy)}.");
        return (this with
        {
            CustomRoutine = CustomRoutine?.Id == id ? null : CustomRoutine,
            AdditionalRoutines = AdditionalRoutines.Where(routine => routine.Id != id).ToArray(),
            BreakRoutineId = BreakRoutineId == id ? BreakRoutines.DefaultId : BreakRoutineId
        }).ValidatePersonalization();
    }
    public AppSettings ApplyReminder(int interval, int idle, string routineId)
    {
        new WorkProfile("validation", "Settings", interval, idle, routineId).Validate();
        if (!BreakRoutines.ForSettings(this).Any(routine => routine.Id == routineId)) throw new ArgumentException("Choose an existing routine.");
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
        var profile = WorkProfiles.FirstOrDefault(item => item.Id == id) ?? throw new ArgumentException("Choose an existing profile.");
        return ApplyReminder(profile.IntervalMinutes, profile.IdleMinutes, profile.RoutineId) with { ActiveProfileId = profile.Id };
    }
}
