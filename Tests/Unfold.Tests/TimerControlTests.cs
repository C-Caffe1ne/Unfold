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
    public void RunningTimerLocksIntervalAndPausedTimerAppliesOneMinuteStepsExplicitly()
    {
        using var scope = new SettingsScope();
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        Assert.False(interval.IsEnabled); Assert.Equal(1, interval.Increment);
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes);
        Press(scope.Window, "TimerToggle");
        Assert.True(interval.IsEnabled); Assert.Equal(60, interval.Value);
        interval.Value = 61; Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes);
        Press(scope.Window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(61, scope.Runtime.Settings.IntervalMinutes);
        Assert.Equal(TimeSpan.FromMinutes(61), scope.Runtime.Clock.Interval);
        Assert.Equal(TimeSpan.FromMinutes(61), scope.Runtime.Clock.Remaining);
        Assert.Equal(61, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IntervalMinutes);
        Press(scope.Window, "TimerStop"); interval.Value = 62;
        Press(scope.Window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(62, scope.Runtime.Settings.IntervalMinutes);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.Equal(TimeSpan.FromMinutes(62), scope.Runtime.Clock.Remaining);
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
    public void TypedIntervalWaitsForApplyAndKeepsTheNewValuePaused()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerToggle");
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        var input = interval.GetVisualDescendants().OfType<TextBox>().Single();
        input.Focus(); input.SelectAll(); scope.Window.KeyTextInput("35");
        scope.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        scope.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Dispatcher.UIThread.RunJobs();
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes);
        Press(scope.Window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(35, scope.Runtime.Settings.IntervalMinutes);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(35), scope.Runtime.Clock.Remaining);
    }
    [AvaloniaFact]
    public void HomeAndSettingsApplyPersistTheirOwnTimeValues()
    {
        using var scope = new SettingsScope(); Press(scope.Window, "TimerToggle");
        var controls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        controls.Single(input => input.Name == "ReminderInterval").Value = 30;
        controls.Single(input => input.Name == "BreakDurationMinutes").Value = 4;
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Press(scope.Window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(30, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(4, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
        OpenSettings(scope.Window);
        var settingsControls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        settingsControls.Single(input => input.Name == "ReminderIdle").Value = 12;
        settingsControls.Single(input => input.Name == "SnoozeMinutes").Value = 9;
        Press(scope.Window, "SavePreferences"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, scope.Runtime.Settings.IdleMinutes); Assert.Equal(9, scope.Runtime.Settings.SnoozeMinutes);
        Assert.Equal(12, settingsControls.Single(input => input.Name == "ReminderIdle").Value);
    }
    [AvaloniaFact]
    public async Task RuntimeRejectsIntervalChangesWhileRunningAndAllowsThemWhilePaused()
    {
        using var scope = new SettingsScope();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => scope.Runtime.UpdateSettings(
            scope.Runtime.Settings with { IntervalMinutes = 30 }));
        Assert.Contains("일시정지하거나 중지", error.Message);
        Assert.Equal(60, scope.Runtime.Settings.IntervalMinutes);

        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IdleMinutes = 12 });
        Assert.Equal(12, scope.Runtime.Settings.IdleMinutes);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings.SaveProfile(
            new("same-interval", "같은 간격", 60, 12, BreakRoutines.DefaultId)));
        await Assert.ThrowsAsync<ArgumentException>(() => scope.Runtime.UpdateSettings(
            scope.Runtime.Settings.ApplyProfile("same-interval")));
        Assert.Null(scope.Runtime.Settings.ActiveProfileId);
        Press(scope.Window, "TimerToggle");
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { IntervalMinutes = 30 });
        Assert.Equal(30, scope.Runtime.Settings.IntervalMinutes);
        Assert.True(scope.Runtime.Clock.Paused);
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
            var button = Button(scope.Window, name); Assert.IsType<PathIcon>(button.Content);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))); Assert.NotNull(ToolTip.GetTip(button));
            var position = button.TranslatePoint(default, scope.Window)!.Value;
            Assert.True(position.X >= 0 && position.X + button.Bounds.Width <= scope.Window.ClientSize.Width);
            Assert.True(position.Y >= 0 && position.Y + button.Bounds.Height <= scope.Window.ClientSize.Height);
        }
        Press(scope.Window, "TimerToggle"); Assert.True(scope.Runtime.Clock.Paused);
        Assert.StartsWith("일시정지", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerStateText").Text);
        Assert.Equal(DesignSystem.Warning, scope.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.Name == "TimerStateIndicator").Fill);
        Assert.Equal("타이머 계속", AutomationProperties.GetName(Button(scope.Window, "TimerToggle")));
        Press(scope.Window, "TimerStop"); Assert.True(scope.Runtime.Clock.Stopped);
        Assert.Equal(scope.Runtime.Clock.Interval, scope.Runtime.Clock.Remaining);
        Assert.Equal("60:00", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerCountdown").Text);
        Assert.StartsWith("중지됨", scope.Window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "TimerStateText").Text);
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "TimerStateDetail");
        Assert.Equal(DesignSystem.Stopped, scope.Window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(dot => dot.Name == "TimerStateIndicator").Fill);
        Assert.Equal("타이머 시작", AutomationProperties.GetName(Button(scope.Window, "TimerToggle")));
        var play = Button(scope.Window, "TimerToggle"); play.Focus();
        scope.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        scope.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Assert.False(scope.Runtime.Clock.Paused); Assert.Equal(scope.Runtime.Clock.Interval, scope.Runtime.Clock.Remaining);
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
            Runtime = new(new ClassicDesktopStyleApplicationLifetime());
            Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose()
        {
            Window.HideToTray(); Runtime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temporary.Dispose();
        }
    }
}
