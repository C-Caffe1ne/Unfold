using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class TimerRefinementTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void ShortIntervalsPersistAndReachDueWithoutPrematureWarnings(int minutes)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { IntervalMinutes = minutes };
        settings.Save(path); Assert.Equal(minutes, AppSettings.Load(path).IntervalMinutes);
        var clock = new StretchClock(TimeSpan.FromMinutes(minutes)); clock.Start(TimeSpan.Zero);
        for (var second = 1; second <= minutes * 60; second++)
        {
            Assert.Equal(second == minutes * 60, clock.Tick(TimeSpan.FromSeconds(second), TimeSpan.Zero, TimeSpan.FromMinutes(5)));
            Assert.False(clock.AdvanceWarningDue);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetInterval(TimeSpan.FromSeconds(59)));
        Assert.Throws<InvalidDataException>(() => (settings with { IntervalMinutes = 0 }).Save(path));
        Assert.Throws<InvalidDataException>(() => (settings with { IntervalMinutes = 241 }).Save(path));
    }

    [Fact]
    public void InvitationSnoozesOnceAtThirtySecondsAfterPresentationNotCreation()
    {
        var reminder = new PetReminder(); var session = new BreakSession(BreakRoutines.All[0], "default-cat");
        var finished = 0; reminder.Finished += _ => finished++;
        reminder.Invite(session); reminder.Tick(TimeSpan.FromMinutes(1));
        Assert.Same(session, reminder.Session); Assert.Null(reminder.NoticeExpiresAt);
        reminder.MarkInvitationPresented(TimeSpan.FromMinutes(1));
        reminder.MarkInvitationPresented(TimeSpan.FromSeconds(75));
        Assert.Equal(TimeSpan.FromSeconds(90), reminder.NoticeExpiresAt);
        reminder.Tick(TimeSpan.FromMilliseconds(89999)); Assert.Same(session, reminder.Session);
        reminder.Tick(TimeSpan.FromSeconds(90));
        Assert.Null(reminder.Session); Assert.Null(reminder.NoticeExpiresAt); Assert.False(reminder.HasNotice);
        Assert.Equal(BreakSessionState.Snoozed, session.State); Assert.Equal(1, reminder.ConsecutiveSnoozes);
        reminder.Tick(TimeSpan.FromSeconds(100)); Assert.Equal(1, finished);
        reminder.Invite(new(BreakRoutines.All[0], "default-cat")); reminder.MarkInvitationPresented(TimeSpan.FromSeconds(120));
        Assert.Equal(TimeSpan.FromSeconds(150), reminder.NoticeExpiresAt);
    }

    [Fact]
    public void StartingOrManuallySnoozingCancelsTheInvitationDeadline()
    {
        foreach (var start in new[] { true, false })
        {
            var reminder = new PetReminder(); var session = new BreakSession(BreakRoutines.All[0], "default-cat");
            reminder.Invite(session); reminder.MarkInvitationPresented(TimeSpan.Zero);
            if (start) Assert.True(reminder.Start(TimeSpan.FromSeconds(29)));
            else Assert.True(reminder.Snooze());
            reminder.Tick(TimeSpan.FromSeconds(31));
            Assert.Null(reminder.NoticeExpiresAt);
            Assert.Equal(start ? PetNotice.Resting : PetNotice.None, reminder.Notice);
            Assert.Equal(start ? 0 : 1, reminder.ConsecutiveSnoozes);
        }
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task AutomaticSnoozeUsesConfiguredMinutesAndPreservesManualPause(bool paused)
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        var doc = new SpriteFixture(16, 16); Array.Fill(doc.Pixels, 0xFFFFFFFFu);
        var pack = runtime.Library.InstallFixture(doc); await runtime.Reload(); runtime.Stop();
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = pack.Manifest.Id, ShowPet = false, SnoozeMinutes = 9 });
        runtime.TogglePause(); if (paused) runtime.TogglePause();
        await runtime.ShowReminder(); Layout(scope.Window);
        Assert.True(runtime.ActivePet!.IsVisible);
        var deadline = runtime.Reminder.NoticeExpiresAt; Assert.NotNull(deadline);
        await runtime.ShowReminder(); Assert.Equal(deadline, runtime.Reminder.NoticeExpiresAt);
        runtime.Reminder.Tick(deadline.Value);
        await runtime.UpdateSettings(runtime.Settings);
        Assert.False(runtime.Reminder.HasNotice); Assert.False(runtime.ActivePet.IsVisible);
        Assert.Equal(TimeSpan.FromMinutes(9), runtime.Clock.Remaining); Assert.Equal(paused, runtime.Clock.Paused);
        Assert.Empty(runtime.BreakHistory.Completions); Assert.Equal(0, runtime.CompletionSoundRequests);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task AlreadyExpiredInvitationHidesTemporaryPetInTheSameRefresh(bool paused)
    {
        using var scope = new Scope(); var runtime = scope.Runtime;
        var doc = new SpriteFixture(16, 16); Array.Fill(doc.Pixels, 0xFFFFFFFFu);
        var pack = runtime.Library.InstallFixture(doc); await runtime.Reload();
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = pack.Manifest.Id, ShowPet = false, SnoozeMinutes = 9 });
        runtime.Stop(); runtime.TogglePause(); if (paused) runtime.TogglePause();
        await runtime.ShowReminder();
        Assert.True(runtime.ActivePet!.IsVisible);
        // Model an invitation whose deadline elapsed before the refresh scheduled its timer.
        runtime.Reminder.Cancel();
        var session = new BreakSession(BreakRoutines.All[0], runtime.Settings.SelectedCharacterId);
        Assert.True(runtime.Reminder.Invite(session));
        runtime.Reminder.MarkInvitationPresented(TimeSpan.FromSeconds(-31));
        var refresh = typeof(AppRuntime).GetMethod("RefreshPetNotice",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        refresh.Invoke(runtime, null);
        Assert.Null(runtime.Reminder.Session); Assert.False(runtime.Reminder.HasNotice);
        Assert.False(runtime.ActivePet.IsVisible);
        Assert.Equal(BreakSessionState.Snoozed, session.State);
        Assert.Equal(TimeSpan.FromMinutes(9), runtime.Clock.Remaining);
        Assert.Equal(paused, runtime.Clock.Paused); Assert.False(runtime.Clock.Stopped);
        Assert.Equal(1, runtime.Reminder.ConsecutiveSnoozes);
        Assert.Empty(runtime.BreakHistory.Completions);
    }

    [AvaloniaFact]
    public void HomeAutoSavesAndReloadsOneMinute()
    {
        using var scope = new Scope(); scope.Runtime.Stop();
        var interval = Find<NumericUpDown>(scope.Window, "ReminderInterval");
        Assert.Equal(1, interval.Minimum);
        interval.Value = 1; interval.Text = "1"; Layout(scope.Window);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, scope.Runtime.Settings.IntervalMinutes);
        Assert.Equal(1, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IntervalMinutes);
        Assert.Equal(TimeSpan.FromMinutes(1), scope.Runtime.Clock.Remaining);
    }

    [AvaloniaFact]
    public void SidebarAndSettingsShowTheBuildVersionAndOpenTheExistingUpdater()
    {
        using var scope = new Scope();
        Assert.Equal(AppRelease.DisplayVersion, Find<TextBlock>(scope.Window, "SidebarVersion").Text!.Replace("\n", " "));
        Press(scope.Window, "SettingsNavSettings"); Layout(scope.Window);
        Assert.Equal(AppRelease.DisplayVersion, Find<TextBlock>(scope.Window, "SettingsAppVersion").Text);
        Press(scope.Window, "SettingsCheckUpdates"); Layout(scope.Window);
        Assert.NotNull(scope.Runtime.ActiveUpdate); Assert.True(scope.Runtime.ActiveUpdate.IsVisible);
        Assert.Equal(1, scope.Backend.Checks);
        var update = scope.Runtime.ActiveUpdate;
        Press(scope.Window, "SettingsCheckUpdates"); Assert.Same(update, scope.Runtime.ActiveUpdate);
    }

    [AvaloniaFact]
    public void StopAndQuitStayDangerColoredAcrossThemesAndHover()
    {
        using var scope = new Scope();
        foreach (var theme in DesignSystem.Themes)
        {
            scope.Runtime.SetTheme(theme.Id); Layout(scope.Window);
            foreach (var name in new[] { "TimerStop", "SettingsQuit" })
            {
                var button = Find<Button>(scope.Window, name);
                Assert.Contains("danger", button.Classes);
                Assert.Equal(DesignSystem.Error.Color, ((ISolidColorBrush)button.Foreground!).Color);
                scope.Window.MouseMove(button.TranslatePoint(new(20, 20), scope.Window)!.Value); Layout(scope.Window);
                var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single(c => c.Name == "PART_ContentPresenter");
                Assert.Equal(DesignSystem.Error.Color, ((ISolidColorBrush)presenter.Foreground!).Color);
            }
        }
    }

    [AvaloniaTheory]
    [InlineData("종료")] [InlineData("중지")]
    public async Task DestructiveConfirmationsUseDangerInsteadOfPrimary(string action)
    {
        using var scope = new Scope();
        var result = Ui.Confirm(scope.Window, action, action, action, "취소"); Layout(scope.Window);
        var dialog = scope.Window.OwnedWindows.Single();
        var destructive = dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, action));
        Assert.Contains("danger", destructive.Classes); Assert.DoesNotContain("primary", destructive.Classes);
        dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "취소")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, await result);
    }

    [AvaloniaFact]
    public void RestTimerAnimatesButHidingAndDetachStopItsFrameTimer()
    {
        var reminder = new PetReminder(); reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
        var bubble = new PetSpeechBubble(() => reminder.Start(TimeSpan.Zero), () => { }, () => { }); bubble.Refresh(reminder, 5);
        var window = new Window { Content = bubble, SizeToContent = SizeToContent.WidthAndHeight };
        try
        {
            window.Show(); Layout(window); var text = Find<AnimatedCountdown>(window, "PetBreakTimer");
            Assert.False(text.IsEffectivelyVisible);
            Press(window, "PetBreakStart"); bubble.Refresh(reminder, 5); Layout(window);
            Assert.Equal("01:00", text.Text); Assert.True(text.IsEffectivelyVisible);
            Assert.Equal(40, text.FontSize); Assert.Equal(48, text.LineHeight);
            reminder.Tick(TimeSpan.FromSeconds(1)); bubble.Refresh(reminder, 5);
            Assert.Equal("00:59", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 1, 3, 4 }, text.AnimatedDigitIndices.Order().ToArray());
            Assert.Equal(-1, text.MotionDirection); Assert.Equal(1, text.Opacity);
            bubble.Refresh(reminder, 5);
            Assert.True(text.HasDigitMotion); Assert.Equal(new[] { 1, 3, 4 }, text.AnimatedDigitIndices.Order().ToArray());
            bubble.IsVisible = false; Assert.False(text.HasDigitMotion); Assert.Empty(text.AnimatedDigitIndices);
            bubble.IsVisible = true; bubble.Refresh(reminder, 5);
            reminder.Tick(TimeSpan.FromSeconds(2)); bubble.Refresh(reminder, 5);
            Assert.Equal("00:58", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 4 }, text.AnimatedDigitIndices.ToArray());
            window.Content = null; Assert.False(text.HasDigitMotion); Assert.Empty(text.AnimatedDigitIndices);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RestTimerUsesChangedDigitsForRemainingAndOvertimeAtSecondBoundaries()
    {
        var reminder = new PetReminder(); reminder.Invite(new(BreakRoutines.All[0], "default-cat", durationSeconds: 60));
        reminder.Start(TimeSpan.Zero);
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => { }); bubble.Refresh(reminder, 5);
        var window = new Window { Content = bubble, SizeToContent = SizeToContent.WidthAndHeight };
        try
        {
            window.Show(); Layout(window); var text = Find<AnimatedCountdown>(window, "PetBreakTimer");
            void RefreshAt(double seconds) { reminder.Tick(TimeSpan.FromSeconds(seconds)); bubble.Refresh(reminder, 5); }
            // BreakSession rejects a stalled/suspended gap over ten seconds.
            for (var second = 1; second <= 59; second++) reminder.Tick(TimeSpan.FromSeconds(second));
            bubble.Refresh(reminder, 5); Assert.Equal("00:01", text.Text);
            RefreshAt(59.75); Assert.Equal("00:01", text.Text);
            RefreshAt(60); Assert.Equal("00:00", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 4 }, text.AnimatedDigitIndices.ToArray()); Assert.Equal(-1, text.MotionDirection);
            RefreshAt(60.9); Assert.Equal("+00:00", text.Text);
            RefreshAt(61); Assert.Equal("+00:01", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 5 }, text.AnimatedDigitIndices.ToArray()); Assert.Equal(1, text.MotionDirection);
            RefreshAt(61.9); Assert.Equal("+00:01", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 5 }, text.AnimatedDigitIndices.ToArray());
            RefreshAt(62); Assert.Equal("+00:02", text.Text); Assert.True(text.HasDigitMotion);
            Assert.Equal(new[] { 5 }, text.AnimatedDigitIndices.ToArray()); Assert.Equal(1, text.MotionDirection);
        }
        finally { window.Close(); }
    }

    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Press(Window window, string name) => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new(); private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public UpdateTestBackend Backend { get; } = new();
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root); Runtime = new(lifetime);
            Runtime.SetUpdateBackend(Backend); Window = new(Runtime); lifetime.MainWindow = Window; Window.Show(); Layout(Window);
        }
        public void Dispose() { Window.Dispose(); Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
