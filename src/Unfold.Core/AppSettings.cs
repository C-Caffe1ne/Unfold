using System.Text.Json;

namespace Unfold.Core;

public sealed partial record AppSettings
{
    public int IntervalMinutes { get; init; } = 60;
    public int IdleMinutes { get; init; } = 5;
    public string SelectedCharacterId { get; init; } = "default-cat";
    public bool ShowPet { get; init; } = true;
    public string BreakRoutineId { get; init; } = BreakRoutines.DefaultId;
    public BreakRoutine? CustomRoutine { get; init; }
    public IReadOnlyList<BreakRoutine> AdditionalRoutines { get; init; } = [];
    public IReadOnlyList<WorkProfile> WorkProfiles { get; init; } = [];
    public string? ActiveProfileId { get; init; }
    public BubbleDirection BubbleDirection { get; init; } = BubbleDirection.Top;
    public bool BubbleCollapsed { get; init; }
    public int SnoozeMinutes { get; init; } = 5;
    public bool ReminderSoundsEnabled { get; init; } = true;
    public string? ReminderSoundId { get; init; }
    public string? CompletionSoundId { get; init; }
    public int? PetX { get; init; }
    public int? PetY { get; init; }
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<AppSettings>(ImageCodec.ReadBounded(path, 256 * 1024), CharacterLibrary.JsonOptions) ?? throw new InvalidDataException("Missing settings.");
        Validate(value);
        if (value.CustomRoutine is { } custom)
        {
            try
            {
                custom.Validate();
                if (custom.Id != BreakRoutines.CustomId || custom.Steps.Count > 3) throw new ArgumentException("Invalid custom routine.");
                value = value with { CustomRoutine = custom with { Steps = Array.AsReadOnly(custom.Steps.ToArray()) } };
            }
            catch (ArgumentException) { value = value with { CustomRoutine = null }; }
        }
        try { value = value.ValidatePersonalization(); }
        catch (ArgumentException error) { throw new InvalidDataException("Invalid routine library or work profiles.", error); }
        if (!BreakRoutines.ForSettings(value).Any(routine => routine.Id == value.BreakRoutineId))
            value = value with { BreakRoutineId = BreakRoutines.DefaultId };
        if (value.ActiveProfileId is { } active && !value.WorkProfiles.Any(profile => profile.Id == active &&
            profile.IntervalMinutes == value.IntervalMinutes && profile.IdleMinutes == value.IdleMinutes && profile.RoutineId == value.BreakRoutineId))
            value = value with { ActiveProfileId = null };
        return value;
    }
    public void Save(string path)
    {
        Validate(this);
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, CharacterLibrary.JsonOptions));
    }
    private static bool ValidSoundId(string? id) => id is null || (id.Length == 64 && id.All(c => char.IsAsciiHexDigit(c)));
    private static void Validate(AppSettings value)
    {
        if (value.IntervalMinutes is < 5 or > 240 || value.IdleMinutes is < 1 or > 60 || !CharacterLibrary.SafeId(value.SelectedCharacterId))
            throw new InvalidDataException("Invalid settings values.");
        if (!Enum.IsDefined(value.BubbleDirection) || value.SnoozeMinutes is < 1 or > 60 ||
            !ValidSoundId(value.ReminderSoundId) || !ValidSoundId(value.CompletionSoundId))
            throw new InvalidDataException("Invalid reminder settings.");
    }
}
