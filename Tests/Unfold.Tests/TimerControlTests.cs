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
        using var scope = new SettingsScope();
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
    public async Task IntervalAutoSavesWhileRunningPausedOrStoppedAndKeepsEachState()
    {
        using var scope = new SettingsScope();
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        var countdown = scope.Window.GetVisualDescendants().OfType<AnimatedCountdown>().Single();
        Assert.True(interval.IsEnabled); Assert.Equal(1, interval.Increment);
        interval.Value = 61; Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Runtime.Settings.IntervalMinutes == 61);
        Assert.False(scope.Runtime.Clock.Paused); Assert.False(scope.Runtime.Clock.Stopped);
        Assert.Equal("61:00", countdown.Text); Assert.True(countdown.HasDigitMotion);
        Assert.Equal(1, countdown.MotionDirection);
        Press(scope.Window, "TimerToggle");
        interval.Value = 59; Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Runtime.Settings.IntervalMinutes == 59);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal("59:00", countdown.Text);
        Assert.True(countdown.HasDigitMotion); Assert.Equal(-1, countdown.MotionDirection);
        Press(scope.Window, "TimerStop"); interval.Value = 62; Dispatcher.UIThread.RunJobs();
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
    public void TypedIntervalAutoSavesAndKeepsTheNewValuePaused()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerToggle");
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        var input = interval.GetVisualDescendants().OfType<TextBox>().Single();
        input.Focus(); input.SelectAll(); scope.Window.KeyTextInput("35");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(35, scope.Runtime.Settings.IntervalMinutes);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(35), scope.Runtime.Clock.Remaining);
    }
    [AvaloniaFact]
    public void HomeAndSettingsAutoSaveTheirOwnTimeValues()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerToggle");
        var controls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        controls.Single(input => input.Name == "ReminderInterval").Value = 30;
        controls.Single(input => input.Name == "BreakDurationMinutes").Value = 4;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(30, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(4, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
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
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 30 });
        Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
        Assert.False(scope.Runtime.Clock.Paused);
        Press(scope.Window, "TimerToggle");
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 31 });
        Assert.True(scope.Runtime.Clock.Paused);
        var session = new BreakSession(BreakRoutines.All[0], "default-cat");
        scope.Runtime.Reminder.Invite(session); scope.Runtime.StartBreak();
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 32 });
        Assert.Same(session, scope.Runtime.Reminder.Session);
        Assert.Empty(scope.Runtime.BreakHistory.Completions);
    }
    [AvaloniaFact]
    public async Task RapidIntervalChangesPersistTheLatestValueAndStopAnimatesBackToTheInterval()
    {
        using var scope = new SettingsScope();
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
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 200 && !ready(); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.True(ready());
    }
    private sealed class SettingsScope : IDisposable
    {
        private readonly TempDirectory temporary = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temporary.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public SettingsScope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(new ClassicDesktopStyleApplicationLifetime()) { ConfirmActionOverride = (_, _, _) => Task.FromResult(0) };
            Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose()
        {
            Window.HideToTray(); Runtime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temporary.Dispose();
        }
    }
}
