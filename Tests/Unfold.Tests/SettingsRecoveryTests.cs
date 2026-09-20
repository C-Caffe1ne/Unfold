using Unfold.Core;

namespace Unfold.Tests;

public class SettingsRecoveryTests
{
    [Fact]
    public void ADamagedFieldRecoversOnItsOwnAndKeepsTheRestOfTheFile()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        foreach (var (json, check) in Cases())
        {
            File.WriteAllText(path, json);
            var value = AppSettings.Load(path);
            check(value);
        }
    }

    private static IEnumerable<(string Json, Action<AppSettings> Check)> Cases() =>
    [
        ("{\"petScalePercent\":55,\"intervalMinutes\":37}", value =>
            { Assert.Equal(100, value.PetScalePercent); Assert.Equal(37, value.IntervalMinutes); }),
        ("{\"intervalMinutes\":3,\"snoozeMinutes\":7}", value =>
            { Assert.Equal(60, value.IntervalMinutes); Assert.Equal(7, value.SnoozeMinutes); }),
        ("{\"breakDurationMinutes\":99,\"idleMinutes\":9}", value =>
            { Assert.Equal(1, value.BreakDurationMinutes); Assert.Equal(9, value.IdleMinutes); }),
        ("{\"idleMinutes\":0,\"snoozeMinutes\":7}", value =>
            { Assert.Equal(5, value.IdleMinutes); Assert.Equal(7, value.SnoozeMinutes); }),
        ("{\"bubbleDirection\":99,\"snoozeMinutes\":7}", value =>
            { Assert.Equal(BubbleDirection.Top, value.BubbleDirection); Assert.Equal(7, value.SnoozeMinutes); }),
        ("{\"snoozeMinutes\":999,\"idleMinutes\":9}", value =>
            { Assert.Equal(5, value.SnoozeMinutes); Assert.Equal(9, value.IdleMinutes); }),
        ("{\"selectedCharacterId\":\"../escape\",\"idleMinutes\":9}", value =>
            { Assert.Equal("default-cat", value.SelectedCharacterId); Assert.Equal(9, value.IdleMinutes); }),
        ("{\"reminderSoundId\":\"nothex\",\"reminderSoundName\":\"내 소리\",\"idleMinutes\":9}", value =>
            { Assert.Null(value.ReminderSoundId); Assert.Null(value.ReminderSoundName); Assert.Equal(9, value.IdleMinutes); })
    ];

    [Fact]
    public void ADamagedScalarDoesNotDiscardTheSavedRoutineLibrary()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        var routine = new BreakRoutine("mine", "내 루틴", [new BreakStep("손을 쉬어요", 20)]);
        new AppSettings { IntervalMinutes = 37, IdleMinutes = 9 }.SaveRoutine(routine).Save(path);
        var original = File.ReadAllText(path);
        var index = original.IndexOf("PetScalePercent", StringComparison.OrdinalIgnoreCase);
        // Guard against a silent no-op if the serialized name ever changes.
        Assert.True(index >= 0, original);
        var damaged = original[..index] + original[index..].Replace("100", "55", StringComparison.Ordinal);
        Assert.NotEqual(original, damaged);
        File.WriteAllText(path, damaged);

        var value = AppSettings.Load(path);
        Assert.Equal(100, value.PetScalePercent);
        Assert.Equal(37, value.IntervalMinutes); Assert.Equal(9, value.IdleMinutes);
        Assert.Equal("내 루틴", Assert.Single(value.AdditionalRoutines).Name);
    }

    [Fact]
    public void SavingStillRefusesAnInvalidValue()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        Assert.Throws<InvalidDataException>(() => new AppSettings { PetScalePercent = 55 }.Save(path));
        Assert.Throws<InvalidDataException>(() => new AppSettings { SnoozeMinutes = 999 }.Save(path));
        Assert.False(File.Exists(path));
    }
}
