using System.Globalization;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[CollectionDefinition("Timer settings", DisableParallelization = true)]
public sealed class TimerSettingsCollection;

[Collection("Timer settings")]
public class TimerControlTests
{
    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    private static void Press(Window window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void OpenSettings(Window window) { Press(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs(); }
    private static void OpenTimer(Window window) { Press(window, "SettingsNavTimer"); Dispatcher.UIThread.RunJobs(); }
    [AvaloniaFact]
    public async Task HomeTimingAutoSavesAndRejectsInvalidRawInputWithoutASaveButton()
    {
        using var scope = new SettingsScope(); scope.Runtime.Stop();
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<Button>(), b => b.Name == "ApplyHomeTimingSettings");
        var rest = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "BreakDurationMinutes");
        rest.Value = 3; Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Runtime.Settings.BreakDurationMinutes == 3);
        Assert.Equal(3, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).BreakDurationMinutes);
        rest.Text = "invalid"; Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, scope.Runtime.Settings.BreakDurationMinutes);
        rest.Value = 4; rest.Text = "4"; Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Runtime.Settings.BreakDurationMinutes == 4);
        Assert.Equal(4, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).BreakDurationMinutes);
    }
    [Fact]
    public void ResetRestoresTheIntervalAndWaitsForExplicitResume()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(25));
        clock.Tick(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        clock.Reset(TimeSpan.FromSeconds(3));
        Assert.True(clock.Paused);
        for (var i = 4; i < 40; i++) Assert.False(clock.Tick(TimeSpan.FromSeconds(i), TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        Assert.Equal(TimeSpan.FromMinutes(25), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(40)); clock.Tick(TimeSpan.FromSeconds(41), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(1499), clock.Remaining);
    }
    [AvaloniaFact]
    public async Task IntervalAutoSavesOnlyWhileStoppedAndRejectsRunningOrPausedChanges()
    {
        using var scope = new SettingsScope();
        var interval = TimingInput(scope.Window, "ReminderInterval");
        Assert.False(interval.IsEnabled); Assert.Equal(1, interval.Increment);
        var remaining = scope.Runtime.Clock.Remaining;
        interval.Value = 61; Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(60, interval.Value);
        Assert.False(scope.Runtime.Clock.Paused); Assert.False(scope.Runtime.Clock.Stopped);
        Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
        Press(scope.Window, "TimerToggle");
        Assert.False(interval.IsEnabled);
        interval.Value = 59; Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(60, interval.Value);
        Assert.True(scope.Runtime.Clock.Paused); Assert.False(scope.Runtime.Clock.Stopped);
        Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
        Press(scope.Window, "TimerStop"); Assert.True(interval.IsEnabled);
        interval.Value = 62; Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Runtime.Settings.IntervalMinutes == 62);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.Equal(TimeSpan.FromMinutes(62), scope.Runtime.Clock.Remaining);
        Assert.Equal(62, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IntervalMinutes);
    }
    [Fact]
    public void StopPauseAndResetHaveDistinctResumeBehavior()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(25)); clock.Start(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5)); clock.TogglePause(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(1497), clock.Remaining); Assert.False(clock.Stopped);
        clock.Stop(TimeSpan.FromSeconds(4)); Assert.True(clock.Stopped); Assert.True(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(25), clock.Remaining);
        clock.SetInterval(TimeSpan.FromMinutes(30)); clock.ScheduleAfterBreak(TimeSpan.FromSeconds(5));
        Assert.True(clock.Stopped); Assert.Equal(TimeSpan.FromMinutes(30), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(6)); Assert.False(clock.Stopped); Assert.False(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), clock.Remaining);
        clock.Stop(TimeSpan.FromSeconds(7)); clock.Reset(TimeSpan.FromSeconds(8));
        Assert.False(clock.Stopped); Assert.True(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), clock.Remaining);
    }
    [AvaloniaFact]
    public void TypedIntervalAutoSavesAndKeepsTheNewValueStopped()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerStop");
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        var input = interval.GetVisualDescendants().OfType<TextBox>().Single();
        input.Focus(); input.SelectAll(); scope.Window.KeyTextInput("35");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(35, scope.Runtime.Settings.IntervalMinutes);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.Equal(TimeSpan.FromMinutes(35), scope.Runtime.Clock.Remaining);
    }
    [AvaloniaFact]
    public void HomeAndSettingsAutoSaveTheirOwnTimeValues()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerStop");
        var controls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        controls.Single(input => input.Name == "ReminderInterval").Value = 30;
        controls.Single(input => input.Name == "BreakDurationMinutes").Value = 4;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(30, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(4, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
        OpenSettings(scope.Window);
        var settingsControls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        settingsControls.Single(input => input.Name == "ReminderIdle").Value = 12;
        settingsControls.Single(input => input.Name == "SnoozeMinutes").Value = 9;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, scope.Runtime.Settings.IdleMinutes); Assert.Equal(9, scope.Runtime.Settings.SnoozeMinutes);
        Assert.Equal(12, settingsControls.Single(input => input.Name == "ReminderIdle").Value);
    }
    [AvaloniaFact]
    public async Task RuntimeIntervalChangesKeepRunningPausedAndBreakStates()
    {
        using var scope = new SettingsScope();
        var interval = TimingInput(scope.Window, "ReminderInterval");
        var rest = TimingInput(scope.Window, "BreakDurationMinutes");
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 30 });
        Assert.False(interval.IsEnabled); Assert.False(rest.IsEnabled); Assert.Equal(30, interval.Value);
        Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
        Assert.False(scope.Runtime.Clock.Paused);
        Press(scope.Window, "TimerToggle");
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 31 });
        Assert.False(interval.IsEnabled); Assert.False(rest.IsEnabled); Assert.Equal(31, interval.Value);
        Assert.True(scope.Runtime.Clock.Paused);
        var session = new BreakSession(BreakRoutines.All[0], "default-cat");
        scope.Runtime.Reminder.Invite(session); scope.Runtime.StartBreak();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 32 });
        Assert.False(interval.IsEnabled); Assert.False(rest.IsEnabled); Assert.Equal(32, interval.Value);
        Assert.Same(session, scope.Runtime.Reminder.Session);
        Assert.Empty(scope.Runtime.BreakHistory.Completions);
    }
    [AvaloniaFact]
    public async Task RapidIntervalChangesPersistTheLatestValueAndStopAnimatesBackToTheInterval()
    {
        using var scope = new SettingsScope(); scope.Runtime.Stop();
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        foreach (var value in new[] { 61, 62, 45, 99, 100, 240, 1, 35 }) { interval.Value = value; Dispatcher.UIThread.RunJobs(); }
        await Until(() => scope.Runtime.Settings.IntervalMinutes == 35);
        Assert.Equal(35, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IntervalMinutes);
        scope.Runtime.Clock.Start(TimeSpan.Zero);
        scope.Runtime.Clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        var countdown = scope.Window.GetVisualDescendants().OfType<AnimatedCountdown>().Single();
        Assert.Equal("34:57", countdown.Text);
        Press(scope.Window, "TimerStop");
        Assert.Equal("35:00", countdown.Text); Assert.True(countdown.HasDigitMotion);
        Assert.Equal(1, countdown.MotionDirection); Assert.True(scope.Runtime.Clock.Stopped);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        Assert.True(countdown.HasDigitMotion);
    }
    [AvaloniaFact]
    public void IconButtonsControlTheClockHaveLabelsAndFitAtMinimumSize()
    {
        using var scope = new SettingsScope(); scope.Window.Width = scope.Window.MinWidth; scope.Window.Height = scope.Window.MinHeight;
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("진행 중", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerStateText").Text);
        Assert.Equal(DesignSystem.Success, scope.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.Name == "TimerStateIndicator").Fill);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Stretch now"));
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<Button>(), button => button.Name == "TimerReset");
        foreach (var name in new[] { "TimerToggle", "TimerStop" })
        {
            var button = Button(scope.Window, name);
            Assert.Equal(name == "TimerStop" ? PlaybackGlyph.Stop : PlaybackGlyph.Pause,
                button.GetVisualDescendants().OfType<PlaybackIcon>().Single().Glyph);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))); Assert.NotNull(ToolTip.GetTip(button));
            var position = button.TranslatePoint(default, scope.Window)!.Value;
            Assert.True(position.X >= 0 && position.X + button.Bounds.Width <= scope.Window.ClientSize.Width);
            Assert.True(position.Y >= 0 && position.Y + button.Bounds.Height <= scope.Window.ClientSize.Height);
        }
        Press(scope.Window, "TimerToggle"); Assert.True(scope.Runtime.Clock.Paused);
        Assert.Equal(PlaybackGlyph.Play, Button(scope.Window, "TimerToggle").GetVisualDescendants().OfType<PlaybackIcon>().Single().Glyph);
        Assert.StartsWith("일시정지", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerStateText").Text);
        Assert.Equal(DesignSystem.Warning, scope.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.Name == "TimerStateIndicator").Fill);
        Assert.Equal("타이머 계속", AutomationProperties.GetName(Button(scope.Window, "TimerToggle")));
        Press(scope.Window, "TimerStop"); Assert.True(scope.Runtime.Clock.Stopped);
        Assert.Equal(scope.Runtime.Clock.Interval, scope.Runtime.Clock.Remaining);
        Assert.Equal("60:00", scope.Window.GetVisualDescendants().OfType<AnimatedCountdown>().Single(text => text.Name == "TimerCountdown").Text);
        Assert.StartsWith("중지됨", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerStateText").Text);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "TimerStateDetail");
        Assert.Equal(DesignSystem.Stopped, scope.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.Name == "TimerStateIndicator").Fill);
        Assert.Equal("타이머 시작", AutomationProperties.GetName(Button(scope.Window, "TimerToggle")));
        var play = Button(scope.Window, "TimerToggle"); play.Focus();
        scope.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        scope.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.False(scope.Runtime.Clock.Paused); Assert.Equal(scope.Runtime.Clock.Interval, scope.Runtime.Clock.Remaining);
        Assert.Equal(PlaybackGlyph.Pause, play.GetVisualDescendants().OfType<PlaybackIcon>().Single().Glyph);
    }
    [AvaloniaFact]
    public void CenteredPlayerKeepsButtonSurfacesSteadyAndDimsOnlyTheInactiveCountdown()
    {
        using var scope = new SettingsScope();
        scope.Window.Width = scope.Window.MinWidth; scope.Window.Height = scope.Window.MinHeight;
        Dispatcher.UIThread.RunJobs(); scope.Window.UpdateLayout();
        var countdown = scope.Window.GetVisualDescendants().OfType<AnimatedCountdown>().Single();
        var badge = scope.Window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "TimerStateBadge");
        var play = Button(scope.Window, "TimerToggle");
        var presenter = play.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>()
            .Single(control => control.Name == "PART_ContentPresenter");
        var bounds = play.Bounds;
        var point = play.TranslatePoint(new Point(10, 10), scope.Window)!.Value;
        scope.Window.MouseMove(point); Dispatcher.UIThread.RunJobs();
        Assert.Equal(DesignSystem.Accent, presenter.Background);
        scope.Window.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
        Assert.Equal(DesignSystem.Accent, presenter.Background); Assert.Equal(bounds, play.Bounds);
        scope.Window.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
        Assert.True(scope.Runtime.Clock.Paused);
        Assert.Equal(.55, countdown.Opacity); Assert.Equal(1, badge.Opacity);
        Assert.False(countdown.HasDigitMotion);
        Assert.Contains(play.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "계속");
        Press(scope.Window, "TimerStop");
        Assert.Equal("60:00", countdown.Text); Assert.Equal(.55, countdown.Opacity);
        Assert.False(Button(scope.Window, "TimerStop").IsEnabled);
        Assert.Contains(play.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "시작");
        Press(scope.Window, "TimerToggle");
        Assert.Equal(1, countdown.Opacity); Assert.True(Button(scope.Window, "TimerStop").IsEnabled);
        Assert.Contains(play.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "일시정지");
        foreach (var palette in DesignSystem.Themes)
        {
            scope.Runtime.SetTheme(palette.Id);
            Press(scope.Window, "TimerToggle"); Assert.Equal(.55, countdown.Opacity);
            Assert.Equal(DesignSystem.Cream, countdown.Foreground);
            Press(scope.Window, "TimerToggle"); Assert.Equal(1, countdown.Opacity);
        }
    }
    [AvaloniaFact]
    public async Task StoppedTimerStillAllowsCancellingAnActiveBreak()
    {
        using var scope = new SettingsScope();
        Press(scope.Window, "TimerStop");
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(Button(scope.Window, "TimerStop").IsEnabled);
        scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        Assert.NotNull(scope.Runtime.ActiveReminder); Assert.True(Button(scope.Window, "TimerStop").IsEnabled);
        Press(scope.Window, "TimerStop");
        Assert.Null(scope.Runtime.ActiveReminder); Assert.True(scope.Runtime.Clock.Stopped);
        Assert.False(Button(scope.Window, "TimerStop").IsEnabled);
    }
    [AvaloniaTheory]
    [InlineData("running", false)]
    [InlineData("paused", false)]
    [InlineData("idle", false)]
    [InlineData("invitation", false)]
    [InlineData("resting", false)]
    [InlineData("stopped", true)]
    [InlineData("stopped-invitation", false)]
    [InlineData("stopped-resting", false)]
    public async Task HomeTimingInputsOnlyEnableWhenStoppedWithoutAnActiveBreak(string state, bool editable)
    {
        using var scope = new SettingsScope();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = false });
        if (state.StartsWith("stopped", StringComparison.Ordinal)) scope.Runtime.Stop();
        if (state == "paused") scope.Runtime.TogglePause();
        if (state == "idle")
        {
            scope.Runtime.Clock.Start(TimeSpan.Zero);
            scope.Runtime.Clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(5));
        }
        if (state.Contains("invitation", StringComparison.Ordinal) || state.Contains("resting", StringComparison.Ordinal))
            scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
        if (state.Contains("resting", StringComparison.Ordinal)) scope.Runtime.StartBreak();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        Dispatcher.UIThread.RunJobs();
        var interval = TimingInput(scope.Window, "ReminderInterval");
        var rest = TimingInput(scope.Window, "BreakDurationMinutes");
        Assert.Equal(editable, interval.IsEnabled); Assert.Equal(editable, rest.IsEnabled);
        if (editable) return;
        var settingsFile = Path.Combine(scope.Root, "settings.json");
        var saved = File.ReadAllText(settingsFile);
        var remaining = scope.Runtime.Clock.Remaining;
        var session = scope.Runtime.ActiveReminder;
        interval.Value = 37; interval.Text = "37";
        rest.Value = 4; rest.Text = "4";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, interval.Value); Assert.Equal(1, rest.Value);
        Assert.Equal("60", interval.Text); Assert.Equal("1", rest.Text);
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(1, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.Equal(saved, File.ReadAllText(settingsFile)); Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
        Assert.Same(session, scope.Runtime.ActiveReminder);
    }

    [AvaloniaFact]
    public async Task LockedHomeTimingRestoresRawTextAndNullableValuesWithoutSaving()
    {
        using var scope = new SettingsScope();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        var path = Path.Combine(scope.Root, "settings.json"); var saved = File.ReadAllText(path);
        foreach (var (name, expected) in new[] { ("ReminderInterval", 60), ("BreakDurationMinutes", 1) })
        {
            var input = TimingInput(scope.Window, name);
            input.Value = null; Dispatcher.UIThread.RunJobs(); Assert.Equal(expected, input.Value);
            foreach (var text in new[] { "", "invalid", "2.5", "9" })
            {
                input.Text = text; Dispatcher.UIThread.RunJobs();
                Assert.Equal(expected, input.Value); Assert.Equal(expected.ToString(CultureInfo.CurrentCulture), input.Text);
            }
        }
        Assert.Equal(saved, File.ReadAllText(path));
        Assert.False(scope.Runtime.Clock.Stopped); Assert.False(scope.Runtime.Clock.Paused);
        Assert.Empty(scope.Window.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task StartingBeforeQueuedHomeTimingSaveDiscardsBothEdits()
    {
        using var scope = new SettingsScope(); scope.Runtime.Stop();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        var path = Path.Combine(scope.Root, "settings.json"); var saved = File.ReadAllText(path);
        var interval = TimingInput(scope.Window, "ReminderInterval");
        var rest = TimingInput(scope.Window, "BreakDurationMinutes");
        interval.Value = 37; rest.Value = 4;
        // Start synchronously before the dispatcher processes the automatic save.
        scope.Runtime.TogglePause();
        Dispatcher.UIThread.RunJobs();
        Assert.False(scope.Runtime.Clock.Stopped); Assert.False(scope.Runtime.Clock.Paused);
        Assert.False(interval.IsEnabled); Assert.False(rest.IsEnabled);
        Assert.Equal(60, interval.Value); Assert.Equal(1, rest.Value);
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(1, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.Equal(TimeSpan.FromMinutes(60), scope.Runtime.Clock.Remaining);
        Assert.Equal(saved, File.ReadAllText(path)); Assert.Empty(scope.Window.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task HomeTimingRemainsLockedWhenStoppedWithoutAccountAccess()
    {
        using var scope = new SettingsScope(); scope.Runtime.Stop();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = false });
        scope.Runtime.AccountServiceFactory = () => new FakeAccountService();
        scope.Runtime.SetUpdateBackend(new UpdateTestBackend { CheckResult = null });
        await scope.Runtime.Start(true);
        Dispatcher.UIThread.RunJobs();
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(scope.Runtime.AccessAllowed);
        var path = Path.Combine(scope.Root, "settings.json"); var saved = File.ReadAllText(path);
        var interval = TimingInput(scope.Window, "ReminderInterval");
        var rest = TimingInput(scope.Window, "BreakDurationMinutes");
        Assert.False(interval.IsEnabled); Assert.False(rest.IsEnabled);
        interval.Value = 37; rest.Text = "4"; Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, interval.Value); Assert.Equal(1, rest.Value);
        Assert.Equal("60", interval.Text); Assert.Equal("1", rest.Text);
        Assert.Equal(saved, File.ReadAllText(path)); Assert.Empty(scope.Window.OwnedWindows);
    }

    private static NumericUpDown TimingInput(Window window, string name) =>
        window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == name);

    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 200 && !ready(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(ready());
    }
    private sealed class SettingsScope : IDisposable
    {
        private readonly TempDirectory temporary = new();
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temporary.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public SettingsScope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime) { ConfirmActionOverride = (_, _, _) => Task.FromResult(0) };
            Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temporary.Dispose();
        }
    }
}
