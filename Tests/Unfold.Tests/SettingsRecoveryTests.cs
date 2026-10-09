using System.Text.Json;
using System.Text.Json.Nodes;
using Unfold.Core;

namespace Unfold.Tests;

public class SettingsRecoveryTests
{
    [Theory]
    [InlineData("petScalePercent", "\"broken\"")]
    [InlineData("petScalePercent", "null")]
    [InlineData("petScalePercent", "1e999")]
    [InlineData("showPet", "[]")]
    [InlineData("theme", "{}")]
    [InlineData("bubbleDirection", "\"sideways\"")]
    [InlineData("petX", "\"broken\"")]
    [InlineData("reminderSoundId", "123")]
    public void WrongFieldTypesKeepUnrelatedSettingsAndRequestAnOriginalBackup(string field, string invalid)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        var json = new JsonObject
        {
            ["intervalMinutes"] = 37, ["idleMinutes"] = 9, ["selectedCharacterId"] = "penguin",
            ["debugToolsEnabled"] = true, [field] = JsonNode.Parse(invalid)
        }.ToJsonString();
        File.WriteAllText(path, json);
        var value = AppSettings.Load(path, out var needsBackup);
        Assert.True(needsBackup);
        Assert.Equal(37, value.IntervalMinutes); Assert.Equal(9, value.IdleMinutes);
        Assert.Equal("penguin", value.SelectedCharacterId); Assert.True(value.DebugToolsEnabled);
        Assert.Equal(json, File.ReadAllText(path));
        value.Save(Path.Combine(temp.Path, "recovered.json"));
        _ = AppSettings.Load(Path.Combine(temp.Path, "recovered.json"), out needsBackup);
        Assert.False(needsBackup);
    }







    [Fact]
    public void EveryHealthySettingSurvivesTheTolerantReader()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        var value = new AppSettings { Theme = (AppTheme)1, IntervalMinutes = 37, IdleMinutes = 9,
            BreakDurationMinutes = 3, SelectedCharacterId = "penguin", ShowPet = false, PetScalePercent = 120,
            BubbleDirection = BubbleDirection.Right, SnoozeMinutes = 7, DebugToolsEnabled = true, ReminderSoundsEnabled = false, ReminderVolumePercent = 42,
            ReminderSoundId = new string('a', 64), CompletionSoundId = new string('b', 64),
            ReminderSoundName = "알림.wav", CompletionSoundName = "완료.wav", PetX = -123, PetY = 456 };
        value.Save(path);
        var loaded = AppSettings.Load(path, out var needsBackup);
        Assert.False(needsBackup);
        Assert.Equal(JsonSerializer.Serialize(value, CharacterLibrary.JsonOptions), JsonSerializer.Serialize(loaded, CharacterLibrary.JsonOptions));
        File.WriteAllText(path, "{\"INTERVALMINUTES\":\"37\",\"IdleMinutes\":9}");
        Assert.Equal(37, AppSettings.Load(path).IntervalMinutes);
    }

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
        ("{\"intervalMinutes\":0,\"snoozeMinutes\":7}", value =>
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
    public void ADamagedScalarPreservesOtherPreferences()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        new AppSettings { IntervalMinutes = 37, IdleMinutes = 9, ShowPet = false }.Save(path);
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
