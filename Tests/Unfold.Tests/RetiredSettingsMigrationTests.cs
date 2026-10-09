using System.Text;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class RetiredSettingsMigrationTests
{
    internal const string LegacyJson = """
        {"intervalMinutes":37,"breakDurationMinutes":7,"idleMinutes":9,"showPet":false,
         "petScalePercent":120,"petX":-120,"petY":80,"snoozeMinutes":11,
         "breakRoutineId":"writing","customRoutine":{"id":"my-routine","name":"내 루틴","steps":[{"instruction":"쉬기","seconds":30}]},
         "additionalRoutines":[{"id":"writing","name":"글쓰기 휴식","steps":[{"instruction":"쉬기","seconds":20}]}],
         "workProfiles":[{"id":"focus","name":"집중 업무","intervalMinutes":37,"idleMinutes":9,"routineId":"writing"}],
         "activeProfileId":"focus"}
        """;
    internal static readonly string[] RetiredKeys = ["breakRoutineId", "customRoutine", "additionalRoutines", "workProfiles", "activeProfileId"];

    [Fact]
    public void SavingLegacySettingsRemovesRetiredFieldsWithoutLosingCurrentPreferences()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, LegacyJson);
        var loaded = AppSettings.Load(file, out var needsBackup);
        Assert.False(needsBackup);
        loaded.Save(file);
        AssertCurrentValues(AppSettings.Load(file)); AssertRetiredFieldsAbsent(file);
        Assert.Single(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void MalformedRetiredFieldsDoNotDamageCurrentPreferencesOrRequireBackup()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, """{"intervalMinutes":37,"idleMinutes":9,"workProfiles":"removed","additionalRoutines":42,"customRoutine":false,"activeProfileId":[]}""");
        var settings = AppSettings.Load(file, out var needsBackup);
        Assert.False(needsBackup); Assert.Equal(37, settings.IntervalMinutes); Assert.Equal(9, settings.IdleMinutes);
        settings.Save(file); AssertRetiredFieldsAbsent(file);
    }

    [AvaloniaFact]
    public void StartupMigratesLegacySettingsOnceWithoutAnOriginalBackup()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        try
        {
            var file = Path.Combine(temp.Path, "settings.json"); File.WriteAllText(file, LegacyJson);
            using var lifetime = new ClassicDesktopStyleApplicationLifetime();
            using (var runtime = new AppRuntime(lifetime)) { AssertCurrentValues(runtime.Settings); AssertRetiredFieldsAbsent(file); }
            Assert.Single(Directory.GetFiles(temp.Path));
            var bytes = File.ReadAllBytes(file);
            using var restarted = new AppRuntime(lifetime); Assert.Equal(bytes, File.ReadAllBytes(file));
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [Fact]
    public void ExistingHistoryKeepsRoutineAndProfileSnapshotsThroughLoadSaveAndCsv()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "history.json");
        File.WriteAllText(file, """
            {"version":1,"completions":[{"sessionId":"a57483ac-7c80-4384-bd35-491d955e4e66","completedAt":"2026-10-09T00:00:00+09:00",
            "routineId":"writing","routineName":"글쓰기 휴식","profileId":"focus","profileName":"집중 업무","seconds":60,"actualSeconds":42,"characterId":"default-cat"}]}
            """);
        var history = BreakHistory.Load(file); history.Save(file);
        var entry = Assert.Single(BreakHistory.Load(file).Completions);
        Assert.Equal("글쓰기 휴식", entry.RoutineName); Assert.Equal("focus", entry.ProfileId); Assert.Equal("집중 업무", entry.ProfileName);
        Assert.Equal(42, entry.ActualSeconds);
        var csv = Encoding.UTF8.GetString(history.Review(new(2026, 10, 9)).Csv());
        Assert.Contains("글쓰기 휴식", csv); Assert.Contains("집중 업무", csv);
    }

    private static void AssertCurrentValues(AppSettings settings)
    {
        Assert.Equal(37, settings.IntervalMinutes); Assert.Equal(7, settings.BreakDurationMinutes); Assert.Equal(9, settings.IdleMinutes);
        Assert.Equal(120, settings.PetScalePercent); Assert.Equal(-120, settings.PetX); Assert.Equal(80, settings.PetY);
        Assert.Equal(11, settings.SnoozeMinutes); Assert.False(settings.ShowPet);
    }
    internal static void AssertRetiredFieldsAbsent(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(file));
        foreach (var key in RetiredKeys) Assert.False(document.RootElement.TryGetProperty(key, out _), key);
    }
}
