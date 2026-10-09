using System.Text.Json;

namespace Unfold.Core;

public sealed partial record AppSettings
{
    // Structural errors used to reject the entire file. Report them to the caller so
    // it can preserve the original before any later save, without writing during Load.
    private static AppSettings ReadFields(byte[] bytes, out bool needsBackup, out bool needsMigration)
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
            BubbleOpacityPercent = Read(nameof(BubbleOpacityPercent), defaults.BubbleOpacityPercent),
            AdvanceDialogue = Read(nameof(AdvanceDialogue), defaults.AdvanceDialogue),
            InvitationDialogue = Read(nameof(InvitationDialogue), defaults.InvitationDialogue),
            RestingDialogue = Read(nameof(RestingDialogue), defaults.RestingDialogue),
            OvertimeDialogue = Read(nameof(OvertimeDialogue), defaults.OvertimeDialogue),
            CompletedDialogue = Read(nameof(CompletedDialogue), defaults.CompletedDialogue),
            BubbleDirection = Read(nameof(BubbleDirection), defaults.BubbleDirection),
            SnoozeMinutes = Read(nameof(SnoozeMinutes), defaults.SnoozeMinutes),
            DebugToolsEnabled = Read(nameof(DebugToolsEnabled), defaults.DebugToolsEnabled),
            ReminderSoundsEnabled = Read(nameof(ReminderSoundsEnabled), defaults.ReminderSoundsEnabled),
            ReminderVolumePercent = Read(nameof(ReminderVolumePercent), defaults.ReminderVolumePercent),
            ReminderSoundVolumePercent = Read(nameof(ReminderSoundVolumePercent), defaults.ReminderSoundVolumePercent),
            CompletionSoundVolumePercent = Read(nameof(CompletionSoundVolumePercent), defaults.CompletionSoundVolumePercent),
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
        // Removed authoring fields are ignored, even when malformed. Startup rewrites only current preferences.
        needsMigration = new[] { "breakRoutineId", "customRoutine", "additionalRoutines", "workProfiles", "activeProfileId" }.Any(fields.ContainsKey);
        return value;
    }

}
