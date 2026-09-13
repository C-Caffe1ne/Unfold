using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class BreakSessionTests
{
    private static BreakSession ShortBreak() => new(BreakRoutines.Find("look-away")!, "default-cat");
    [Fact]
    public void TimeAloneNeverRecordsCompletionAndActionsAreTerminal()
    {
        var session = ShortBreak(); var history = new BreakHistory();
        Assert.False(session.Complete()); session.Tick(TimeSpan.FromHours(1)); Assert.Equal(TimeSpan.Zero, session.Elapsed);
        Assert.True(session.Start(TimeSpan.Zero)); Assert.False(session.Start(TimeSpan.Zero));
        session.Tick(TimeSpan.FromSeconds(10)); Assert.False(session.Complete());
        session.Tick(TimeSpan.FromSeconds(20)); Assert.Equal(BreakSessionState.AwaitingConfirmation, session.State);
        Assert.False(history.Add(session, DateTimeOffset.Now));
        Assert.True(session.Complete()); Assert.True(history.Add(session, DateTimeOffset.Now));
        Assert.False(session.Complete()); Assert.False(session.Snooze()); Assert.False(session.Skip());
        Assert.False(history.Add(session, DateTimeOffset.Now)); Assert.Single(history.Completions);
    }
    [Fact]
    public void SleepAndBackwardClockDoNotFinishABreak()
    {
        var session = ShortBreak(); session.Start(TimeSpan.FromSeconds(5));
        session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(9));
        Assert.Equal(TimeSpan.FromSeconds(5), session.Elapsed);
        session.Tick(TimeSpan.FromHours(1)); Assert.Equal(TimeSpan.FromSeconds(5), session.Elapsed);
        session.Tick(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(5));
        Assert.Equal(TimeSpan.FromSeconds(10), session.Elapsed); Assert.Equal(BreakSessionState.InProgress, session.State);
    }
    [Fact]
    public void RoutineAdvancesThroughOrderedSteps()
    {
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); session.Start(TimeSpan.Zero);
        Assert.Equal(session.Routine.Steps[0], session.CurrentStep);
        session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20));
        Assert.Equal(session.Routine.Steps[1], session.CurrentStep);
        session.Tick(TimeSpan.FromSeconds(30)); session.Tick(TimeSpan.FromSeconds(40));
        Assert.Equal(session.Routine.Steps[2], session.CurrentStep);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void SnoozeAndSkipNeverBecomeCompletions(bool snooze)
    {
        var session = ShortBreak(); session.Start(TimeSpan.Zero);
        Assert.True(snooze ? session.Snooze() : session.Skip()); session.Tick(TimeSpan.FromSeconds(10));
        Assert.False(session.Complete()); Assert.False(new BreakHistory().Add(session, DateTimeOffset.Now));
    }
    [Fact]
    public void HeldClockAndSnoozePreserveWorkIntervalAndManualPause()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(45)); clock.Start(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(5), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        clock.Tick(TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.FromMinutes(5), true);
        Assert.Equal(TimeSpan.FromMinutes(45) - TimeSpan.FromSeconds(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(10)); clock.ScheduleAfterBreak(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5));
        Assert.True(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(45), clock.Interval);
        clock.Tick(TimeSpan.FromSeconds(15), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(15)); clock.Tick(TimeSpan.FromSeconds(20), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(295), clock.Remaining);
        clock.ScheduleAfterBreak(TimeSpan.FromSeconds(20)); Assert.Equal(clock.Interval, clock.Remaining);
    }
    [Fact]
    public void ConfirmedHistoryRoundTripsAndKeepsTheCompletionLocalDay()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "break-history.json");
        var date = new DateTimeOffset(2026, 9, 13, 0, 5, 0, TimeSpan.FromHours(9));
        var history = new BreakHistory(); var session = ShortBreak(); session.Start(TimeSpan.Zero);
        session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); session.Complete();
        history.Add(session, date); history.Save(file);
        var reloaded = BreakHistory.Load(file);
        Assert.Equal(new BreakSummary(1, 20), reloaded.ForDay(date));
        Assert.Equal(new BreakSummary(0, 0), reloaded.ForDay(date.AddDays(-1)));
        Assert.Equal(session.Id, Assert.Single(reloaded.Completions).SessionId);
    }
    [Fact]
    public void CorruptHistoryIsRejectedWithoutOverwritingIt()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "break-history.json");
        File.WriteAllText(file, "{broken");
        Assert.Throws<JsonException>(() => BreakHistory.Load(file)); Assert.Equal("{broken", File.ReadAllText(file));
        File.WriteAllText(file, "{\"version\":99,\"completions\":[]}");
        Assert.Throws<InvalidDataException>(() => BreakHistory.Load(file));
    }
    [Fact]
    public void OlderSettingsGainDefaultRoutineAndUnknownRoutineFallsBack()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"intervalMinutes\":45}");
        Assert.Equal(BreakRoutines.DefaultId, AppSettings.Load(file).BreakRoutineId);
        (new AppSettings { BreakRoutineId = "future-routine" }).Save(file);
        Assert.Equal(BreakRoutines.DefaultId, AppSettings.Load(file).BreakRoutineId);
    }
    [Fact]
    public void CustomRoutineSurvivesReloadAndAnOpenSessionKeepsItsSteps()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        var steps = new[] { new BreakStep("My own pause", 30), new BreakStep("Move at my pace", 45) };
        var custom = new BreakRoutine(BreakRoutines.CustomId, "After writing", steps);
        (new AppSettings { CustomRoutine = custom, BreakRoutineId = custom.Id }).Save(file);
        var loaded = AppSettings.Load(file);
        Assert.Equal(custom.Id, loaded.BreakRoutineId); Assert.Equal(75, loaded.CustomRoutine!.DurationSeconds);
        Assert.Equal("After writing", loaded.CustomRoutine.Name); Assert.Equal(4, BreakRoutines.ForSettings(loaded).Count);
        var session = new BreakSession(custom, "default-cat"); steps[0] = new BreakStep("Changed later", 1);
        Assert.Equal(75, session.Routine.DurationSeconds); Assert.Equal("My own pause", session.CurrentStep.Instruction);
    }
    [Fact]
    public void InvalidCustomRoutineFallsBackWithoutDiscardingOtherSettings()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"intervalMinutes\":45,\"breakRoutineId\":\"my-routine\",\"customRoutine\":{\"id\":\"my-routine\",\"name\":\"Bad\",\"steps\":[{\"instruction\":\"Pause\",\"seconds\":-1}]}}");
        var loaded = AppSettings.Load(file);
        Assert.Equal(45, loaded.IntervalMinutes); Assert.Null(loaded.CustomRoutine); Assert.Equal(BreakRoutines.DefaultId, loaded.BreakRoutineId);
    }
}
