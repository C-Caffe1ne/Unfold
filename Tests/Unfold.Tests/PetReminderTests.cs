using Unfold.Core;
using Unfold.Desktop;
using Avalonia;

namespace Unfold.Tests;

public class PetReminderTests
{
    [Fact]
    public void EarlyAndLateCompletionsRecordActualTimeOnceAndRetainPlannedTime()
    {
        using var temp = new TempDirectory(); var history = new BreakHistory();
        foreach (var elapsed in new[] { 0, 3, 22 })
        {
            var session = new BreakSession(BreakRoutines.Find("look-away")!, "default-cat");
            session.Start(TimeSpan.Zero);
            for (var i = 1; i <= elapsed; i++) session.Tick(TimeSpan.FromSeconds(i));
            Assert.True(session.Complete()); Assert.True(history.Add(session, DateTimeOffset.Now));
            Assert.False(history.Add(session, DateTimeOffset.Now));
            Assert.Equal(elapsed, history.Completions[^1].ActualSeconds); Assert.Equal(20, history.Completions[^1].Seconds);
        }
        var path = Path.Combine(temp.Path, "history.json"); history.Save(path);
        var loaded = BreakHistory.Load(path); Assert.Equal(25, loaded.ForDay(DateTimeOffset.Now).Seconds);
        Assert.Contains("actual_seconds", System.Text.Encoding.UTF8.GetString(loaded.Review(DateOnly.FromDateTime(DateTime.Now)).Csv()));
    }

    [Fact]
    public void OvertimeCapsAtSixtyMinutesAndStillRequiresExplicitCompletion()
    {
        var model = new PetReminder(); var session = new BreakSession(BreakRoutines.Find("look-away")!, "default-cat");
        model.Invite(session); model.Start(TimeSpan.Zero);
        for (var i = 10; i <= 4000; i += 10) model.Tick(TimeSpan.FromSeconds(i));
        Assert.Equal("+60:00", PetReminder.TimerText(session)); Assert.Equal(TimeSpan.FromSeconds(3620), session.Elapsed);
        Assert.Equal(BreakSessionState.AwaitingConfirmation, session.State); Assert.Same(session, model.Session);
        var finished = 0; model.Finished += _ => finished++;
        Assert.True(model.Complete(TimeSpan.FromSeconds(4000))); Assert.False(model.Complete(TimeSpan.FromSeconds(4000)));
        Assert.Equal(1, finished); Assert.Equal(PetNotice.Completed, model.Notice);
        model.Tick(TimeSpan.FromSeconds(4015)); Assert.False(model.HasNotice);
    }

    [Fact]
    public void NoticesDoNotReplaceSessionsAndSnoozeAndCancelNeverComplete()
    {
        var model = new PetReminder(); model.ShowAdvance(TimeSpan.Zero);
        Assert.Equal(PetNotice.Advance, model.Notice); model.Tick(TimeSpan.FromSeconds(15)); Assert.False(model.HasNotice);
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); Assert.True(model.Invite(session));
        model.ShowAdvance(TimeSpan.FromSeconds(20)); Assert.Equal(PetNotice.Invitation, model.Notice);
        Assert.False(model.Invite(new(BreakRoutines.All[0], "default-cat")));
        Assert.True(model.Snooze()); Assert.Equal(BreakSessionState.Snoozed, session.State); Assert.Null(model.Session);
        var next = new BreakSession(BreakRoutines.All[0], "default-cat"); model.Invite(next); model.Start(TimeSpan.Zero);
        Assert.False(model.Snooze()); model.Cancel(); Assert.Equal(BreakSessionState.Skipped, next.State);
    }

    [Fact]
    public void FiveMinuteWarningFiresOncePerCycleAndNeverDuringPauseOrSnoozeUnderFiveMinutes()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(6)); clock.Start(TimeSpan.Zero);
        var warnings = 0; var due = 0;
        for (var i = 1; i <= 360; i++)
        {
            if (clock.Tick(TimeSpan.FromSeconds(i), TimeSpan.Zero, TimeSpan.FromMinutes(5))) due++;
            if (clock.AdvanceWarningDue) { warnings++; Assert.Equal(60, i); }
        }
        Assert.Equal(1, warnings); Assert.Equal(1, due);
        clock.ScheduleAfterBreak(TimeSpan.FromSeconds(360), TimeSpan.FromMinutes(3));
        clock.TogglePause(TimeSpan.FromSeconds(360)); clock.Tick(TimeSpan.FromSeconds(361), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Assert.False(clock.AdvanceWarningDue); clock.Start(TimeSpan.FromSeconds(361));
        clock.Tick(TimeSpan.FromSeconds(362), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.False(clock.AdvanceWarningDue);
        clock.ScheduleAfterBreak(TimeSpan.FromSeconds(362));
        for (var i = 363; i <= 422; i++) clock.Tick(TimeSpan.FromSeconds(i), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Assert.True(clock.AdvanceWarningDue);
    }

    [Theory]
    [InlineData(BubbleDirection.Top, 1)] [InlineData(BubbleDirection.Bottom, 2)]
    [InlineData(BubbleDirection.Left, 1.5)] [InlineData(BubbleDirection.Right, 2)]
    public void BubbleFitsNegativeOriginMonitorAtEdges(BubbleDirection direction, double scale)
    {
        var work = new PixelRect(-2560, -100, 2560, 1440); var layout = PetBubbleLayout.Create(direction, true);
        foreach (var anchor in new[] { new Avalonia.PixelPoint(work.X, work.Y), new(work.Right - 192, work.Bottom - 192) })
        {
            var position = layout.Position(anchor, scale, work);
            Assert.True(position.X >= work.X && position.Y >= work.Y);
            Assert.True(position.X + layout.Size.Width * scale <= work.Right);
            Assert.True(position.Y + layout.Size.Height * scale <= work.Bottom);
        }
    }

    [Fact]
    public void NewSettingsDefaultAndValidateReminderPreferences()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json"); File.WriteAllText(path, "{}");
        var old = AppSettings.Load(path); Assert.Equal(BubbleDirection.Top, old.BubbleDirection); Assert.Equal(5, old.SnoozeMinutes);
        Assert.Equal(1, old.BreakDurationMinutes);
        var updated = old with { BubbleDirection = BubbleDirection.Left, BubbleCollapsed = true, SnoozeMinutes = 60, ReminderSoundsEnabled = false };
        updated.Save(path); var loaded = AppSettings.Load(path);
        Assert.Equal(BubbleDirection.Left, loaded.BubbleDirection); Assert.True(loaded.BubbleCollapsed);
        Assert.Equal(60, loaded.SnoozeMinutes); Assert.False(loaded.ReminderSoundsEnabled);
        Assert.Throws<InvalidDataException>(() => (old with { SnoozeMinutes = 0 }).Save(path));
        Assert.Throws<InvalidDataException>(() => (old with { BreakDurationMinutes = 11 }).Save(path));
        Assert.Throws<InvalidDataException>(() => (old with { ReminderSoundId = "../escape" }).Save(path));
        Assert.Throws<InvalidDataException>(() => (old with { BubbleDirection = (BubbleDirection)99 }).Save(path));
    }
}
