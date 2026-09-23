using System.Text.Json;

namespace Unfold.Core;

public sealed partial record AppSettings
{
    // Structural errors used to reject the entire file. Report them to the caller so
    // it can preserve the original before any later save, without writing during Load.
    private static AppSettings ReadFields(byte[] bytes, out bool needsBackup)
    {
        using var document = JsonDocument.Parse(bytes, new() { MaxDepth = CharacterLibrary.JsonOptions.MaxDepth });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Missing settings object.");
        var fields = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject()) fields[property.Name] = property.Value;
        var damaged = false;
        T Read<T>(string name, T fallback)
        {
            if (!fields.TryGetValue(name, out var field)) return fallback;
            try
            {
                var result = field.Deserialize<T>(CharacterLibrary.JsonOptions);
                if (result is not null) return result;
                if (fallback is not null) damaged = true;
            }
            catch (JsonException) { damaged = true; }
            return fallback;
        }
        IReadOnlyList<T> ReadList<T>(string name)
        {
            if (!fields.TryGetValue(name, out var field)) return [];
            if (field.ValueKind != JsonValueKind.Array) { damaged = true; return []; }
            var items = new List<T>();
            foreach (var element in field.EnumerateArray())
            {
                try
                {
                    var item = element.Deserialize<T>(CharacterLibrary.JsonOptions);
                    if (item is not null) items.Add(item);
                    else damaged = true;
                }
                catch (JsonException) { damaged = true; }
            }
            return items;
        }
        var defaults = new AppSettings();
        var value = defaults with
        {
            Theme = Read(nameof(Theme), defaults.Theme),
            IntervalMinutes = Read(nameof(IntervalMinutes), defaults.IntervalMinutes),
            BreakDurationMinutes = Read(nameof(BreakDurationMinutes), defaults.BreakDurationMinutes),
            IdleMinutes = Read(nameof(IdleMinutes), defaults.IdleMinutes),
            SelectedCharacterId = Read(nameof(SelectedCharacterId), defaults.SelectedCharacterId),
            ShowPet = Read(nameof(ShowPet), defaults.ShowPet),
            PetScalePercent = Read(nameof(PetScalePercent), defaults.PetScalePercent),
            BreakRoutineId = Read(nameof(BreakRoutineId), defaults.BreakRoutineId),
            CustomRoutine = Read(nameof(CustomRoutine), defaults.CustomRoutine),
            AdditionalRoutines = ReadList<BreakRoutine>(nameof(AdditionalRoutines)),
            WorkProfiles = ReadList<WorkProfile>(nameof(WorkProfiles)),
            ActiveProfileId = Read(nameof(ActiveProfileId), defaults.ActiveProfileId),
            BubbleDirection = Read(nameof(BubbleDirection), defaults.BubbleDirection),
            SnoozeMinutes = Read(nameof(SnoozeMinutes), defaults.SnoozeMinutes),
            DebugToolsEnabled = Read(nameof(DebugToolsEnabled), defaults.DebugToolsEnabled),
            ReminderSoundsEnabled = Read(nameof(ReminderSoundsEnabled), defaults.ReminderSoundsEnabled),
            ReminderVolumePercent = Read(nameof(ReminderVolumePercent), defaults.ReminderVolumePercent),
            ReminderSoundId = Read(nameof(ReminderSoundId), defaults.ReminderSoundId),
            CompletionSoundId = Read(nameof(CompletionSoundId), defaults.CompletionSoundId),
            ReminderSoundName = Read(nameof(ReminderSoundName), defaults.ReminderSoundName),
            CompletionSoundName = Read(nameof(CompletionSoundName), defaults.CompletionSoundName),
            PetX = Read(nameof(PetX), defaults.PetX),
            PetY = Read(nameof(PetY), defaults.PetY)
        };
        // A broken typed ID must not leave a filename claiming that sound is selected.
        if (value.ReminderSoundId is null) value = value with { ReminderSoundName = null };
        if (value.CompletionSoundId is null) value = value with { CompletionSoundName = null };
        needsBackup = damaged;
        return value;
    }

    private AppSettings RecoverPersonalization(ref bool needsBackup)
    {
        var primary = CustomRoutine;
        if (primary is not null)
        {
            try
            {
                ValidateUserRoutine(primary);
                if (primary.Id != BreakRoutines.CustomId) throw new ArgumentException("Invalid custom routine.");
            }
            catch (ArgumentException) { primary = null; needsBackup = true; }
        }
        var ids = new HashSet<string>(BreakRoutines.All.Select(routine => routine.Id), StringComparer.OrdinalIgnoreCase)
            { BreakRoutines.CustomId };
        var routines = new List<BreakRoutine>();
        foreach (var routine in AdditionalRoutines)
        {
            try
            {
                ValidateUserRoutine(routine);
                if (routines.Count >= MaxAdditionalRoutines || !ids.Add(routine.Id))
                    throw new ArgumentException("Duplicate or excess routine.");
                routines.Add(routine);
            }
            catch (ArgumentException) { needsBackup = true; }
        }
        var recovered = this with { CustomRoutine = primary, AdditionalRoutines = routines };
        var routineIds = BreakRoutines.ForSettings(recovered).Select(routine => routine.Id).ToHashSet(StringComparer.Ordinal);
        var profileIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var profiles = new List<WorkProfile>();
        foreach (var profile in WorkProfiles)
        {
            try
            {
                profile.Validate();
                if (profiles.Count >= MaxWorkProfiles || !routineIds.Contains(profile.RoutineId) || !profileIds.Add(profile.Id))
                    throw new ArgumentException("Duplicate, excess or dangling profile.");
                profiles.Add(profile);
            }
            catch (ArgumentException) { needsBackup = true; }
        }
        return (recovered with { WorkProfiles = profiles }).ValidatePersonalization();
    }
}
