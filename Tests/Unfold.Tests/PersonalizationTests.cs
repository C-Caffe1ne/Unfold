using System.Text;
using Unfold.Core;

namespace Unfold.Tests;

public class PersonalizationTests
{
    private static BreakRoutine Routine(string id = "writing", string name = "Writing pause") => new(id, name, [new("Rest my hands", 20)]);
    private static BreakSession Completed(BreakRoutine? routine = null, WorkProfile? profile = null)
    {
        var session = new BreakSession(routine ?? Routine(), "default-cat", profile); session.Start(TimeSpan.Zero);
        session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); Assert.True(session.Complete()); return session;
    }
    [Fact]
    public void LegacyRoutineAndPreferencesSurviveAddingAndApplyingProfiles()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        new AppSettings { CustomRoutine = Routine(BreakRoutines.CustomId), BreakRoutineId = BreakRoutines.CustomId, ShowPet = false, PetX = -120 }.Save(file);
        var settings = AppSettings.Load(file).SaveRoutine(Routine("drawing", "Drawing pause"));
        settings = settings.SaveProfile(new("creative", "Creative work", 45, 3, "drawing")).ApplyProfile("creative"); settings.Save(file);
        var loaded = AppSettings.Load(file);
        Assert.NotNull(loaded.CustomRoutine); Assert.Single(loaded.AdditionalRoutines); Assert.Single(loaded.WorkProfiles);
        Assert.Equal("creative", loaded.ActiveProfileId); Assert.Equal(45, loaded.IntervalMinutes); Assert.Equal(3, loaded.IdleMinutes);
        Assert.Equal("drawing", loaded.BreakRoutineId); Assert.False(loaded.ShowPet); Assert.Equal(-120, loaded.PetX);
    }
    [Fact]
    public void SavingSettingsSnapshotsExternalRoutineCollections()
    {
        var steps = new[] { new BreakStep("Original", 20) };
        var settings = new AppSettings().SaveRoutine(new("writing", "Writing", steps));
        steps[0] = new("Changed outside", 1);
        Assert.Equal("Original", Assert.Single(settings.AdditionalRoutines).Steps[0].Instruction);
    }
    [Fact]
    public void RoutineDeletionProtectsProfileReferencesAndFallsBackWhenUnreferenced()
    {
        var settings = new AppSettings().SaveRoutine(Routine()).SaveProfile(new("work", "Work", 30, 5, "writing"));
        Assert.Throws<ArgumentException>(() => settings.RemoveRoutine("writing"));
        var removed = settings.RemoveProfile("work").RemoveRoutine("writing");
        Assert.Empty(removed.AdditionalRoutines); Assert.Equal(BreakRoutines.DefaultId, removed.BreakRoutineId);
        Assert.Throws<ArgumentException>(() => settings.RemoveRoutine(BreakRoutines.DefaultId));
    }
    [Fact]
    public void EditingOrDeletingActiveProfileKeepsAppliedValuesAndClearsItsLabel()
    {
        var profile = new WorkProfile("focus", "Focus", 90, 10, BreakRoutines.DefaultId);
        var settings = new AppSettings().SaveProfile(profile).ApplyProfile(profile.Id);
        var edited = settings.SaveProfile(profile with { IntervalMinutes = 30 });
        Assert.Null(edited.ActiveProfileId); Assert.Equal(90, edited.IntervalMinutes);
        var removed = settings.RemoveProfile(profile.Id); Assert.Null(removed.ActiveProfileId); Assert.Equal(90, removed.IntervalMinutes);
        Assert.Null(settings.ApplyReminder(45, 5, BreakRoutines.DefaultId).ActiveProfileId);
    }
    [Fact]
    public void InvalidLibraryAndDanglingProfileAreRejectedWithoutChangingFile()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        var json = "{\"workProfiles\":[{\"id\":\"work\",\"name\":\"Work\",\"intervalMinutes\":30,\"idleMinutes\":5,\"routineId\":\"missing\"}]}";
        File.WriteAllText(file, json); Assert.Throws<InvalidDataException>(() => AppSettings.Load(file)); Assert.Equal(json, File.ReadAllText(file));
        Assert.Throws<ArgumentException>(() => (new AppSettings { AdditionalRoutines = [Routine("WRITING"), Routine("writing")] }).ValidatePersonalization());
        Assert.Throws<ArgumentException>(() => new AppSettings().SaveRoutine(Routine(BreakRoutines.DefaultId)));
        Assert.Throws<ArgumentException>(() => (new AppSettings { AdditionalRoutines = Enumerable.Range(0, 20).Select(i => Routine($"routine-{i}")).ToArray() }).ValidatePersonalization());
    }
    [Fact]
    public void SessionAndHistoryKeepProfileAndRoutineNamesAfterEdits()
    {
        var routine = Routine(); var profile = new WorkProfile("focus", "Focus", 45, 5, routine.Id);
        var session = Completed(routine, profile); var settings = new AppSettings().SaveRoutine(routine).SaveProfile(profile);
        settings = settings.SaveRoutine(routine with { Name = "Renamed" }).SaveProfile(profile with { Name = "New profile name" });
        var history = new BreakHistory(); history.Add(session, DateTimeOffset.Now);
        var entry = Assert.Single(history.Completions);
        Assert.Equal("Writing pause", entry.RoutineName); Assert.Equal("Focus", entry.ProfileName); Assert.Equal("focus", entry.ProfileId);
        Assert.Equal("Renamed", Assert.Single(settings.AdditionalRoutines).Name);
    }
    [Fact]
    public void SevenDayReviewIncludesEmptyDaysAndUsesCompletionLocalDate()
    {
        var end = new DateTimeOffset(2026, 9, 13, 0, 5, 0, TimeSpan.FromHours(9)); var history = new BreakHistory();
        foreach (var days in new[] { -7, -6, -2, 0, 1 }) history.Add(Completed(), end.AddDays(days));
        var review = history.Review(DateOnly.FromDateTime(end.Date));
        Assert.Equal(7, review.Days.Count); Assert.Equal(3, review.Entries.Count); Assert.Equal(60, review.TotalSeconds);
        Assert.Equal(new DateOnly(2026, 9, 7), review.StartDay); Assert.Equal(3, review.DaysWithBreaks);
        Assert.Equal(1, review.Days[^1].Count); Assert.Equal(0, review.Days[1].Count);
    }
    [Fact]
    public void ReviewExportQuotesNamesNeutralizesFormulasAndKeepsUnicode()
    {
        var history = new BreakHistory(); var date = DateTimeOffset.Now;
        history.Add(Completed(Routine(name: "=작업, \"쉼\"")), date);
        var bytes = history.Review(DateOnly.FromDateTime(date.Date)).Csv();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'=작업, \"\"쉼\"\"\"", text); Assert.Contains("planned_seconds", text);
        Assert.Equal(3, text.Split("\r\n").Length);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }
    [Fact]
    public void LongKoreanNamesRoundTripAtTheHistoryRecordLimit()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "history.json");
        var routine = Routine(name: new string('쉼', 60)); var profile = new WorkProfile("focus", new string('일', 60), 45, 5, routine.Id);
        var history = new BreakHistory(); var date = DateTimeOffset.Now;
        for (var i = 0; i < BreakHistory.MaxRecords; i++) history.Add(Completed(routine, profile), date.AddSeconds(i));
        history.Save(file); Assert.Equal(BreakHistory.MaxRecords, BreakHistory.Load(file).Completions.Count);
    }
    [Fact]
    public void OlderHistoryStillLoadsAndExportsItsStableRoutineId()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "history.json");
        File.WriteAllText(file, "{\"version\":1,\"completions\":[{\"sessionId\":\"a57483ac-7c80-4384-bd35-491d955e4e66\",\"completedAt\":\"2026-09-13T00:00:00+09:00\",\"routineId\":\"my-routine\",\"seconds\":20,\"characterId\":\"default-cat\"}]}");
        var history = BreakHistory.Load(file); Assert.Null(Assert.Single(history.Completions).ProfileId);
        var csv = Encoding.UTF8.GetString(history.Review(new(2026, 9, 13)).Csv());
        Assert.Contains("\"my-routine\",\"my-routine\",20", csv);
    }
}
