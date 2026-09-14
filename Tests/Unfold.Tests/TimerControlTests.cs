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
    public void ChangingTheReminderIntervalUpdatesSettingsAndTheClockWithoutApply()
    {
        using var scope = new SettingsScope();
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        interval.Value = 25; Dispatcher.UIThread.RunJobs();
        Assert.Equal(25, scope.Runtime.Settings.IntervalMinutes);
        Assert.Equal(TimeSpan.FromMinutes(25), scope.Runtime.Clock.Interval);
        Assert.Equal(TimeSpan.FromMinutes(25), scope.Runtime.Clock.Remaining);
        Assert.Equal(25, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).IntervalMinutes);
    }
    [Fact]
    public void StopPauseAndResetHaveDistinctResumeBehavior()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(25)); clock.Start(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5)); clock.TogglePause(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(1497), clock.Remaining); Assert.False(clock.Stopped);
        clock.Stop(TimeSpan.FromSeconds(4)); Assert.True(clock.Stopped); Assert.True(clock.Paused); Assert.Equal(TimeSpan.Zero, clock.Remaining);
        clock.SetInterval(TimeSpan.FromMinutes(30)); clock.ScheduleAfterBreak(TimeSpan.FromSeconds(5));
        Assert.True(clock.Stopped); Assert.Equal(TimeSpan.Zero, clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(6)); Assert.False(clock.Stopped); Assert.False(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), clock.Remaining);
        clock.Stop(TimeSpan.FromSeconds(7)); clock.Reset(TimeSpan.FromSeconds(8));
        Assert.False(clock.Stopped); Assert.True(clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), clock.Remaining);
    }
    [AvaloniaFact]
    public void TypedIntervalCommitsWithEnterAndResetKeepsTheNewValuePaused()
    {
        using var scope = new SettingsScope();
        var interval = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().Single(input => input.Name == "ReminderInterval");
        var input = interval.GetVisualDescendants().OfType<TextBox>().Single();
        input.Focus(); input.SelectAll(); scope.Window.KeyTextInput("35");
        scope.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        scope.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Dispatcher.UIThread.RunJobs();
        Assert.Equal(35, scope.Runtime.Settings.IntervalMinutes);
        Press(scope.Window, "TimerReset");
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(35), scope.Runtime.Clock.Remaining);
    }
    [AvaloniaFact]
    public void IntervalChangesPreservePauseAndDoNotApplyUncommittedOtherFields()
    {
        using var scope = new SettingsScope(); scope.Runtime.Reset();
        var controls = scope.Window.GetVisualDescendants().OfType<NumericUpDown>().ToArray();
        controls.Single(input => input.Name == "ReminderIdle").Value = 12;
        controls.Single(input => input.Name == "ReminderInterval").Value = 30;
        Assert.Equal(30, scope.Runtime.Settings.IntervalMinutes); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(TimeSpan.FromMinutes(30), scope.Runtime.Clock.Remaining);
        Assert.Equal(12, controls.Single(input => input.Name == "ReminderIdle").Value);
    }
    [AvaloniaFact]
    public void IconButtonsControlTheClockHaveLabelsAndFitAtMinimumSize()
    {
        using var scope = new SettingsScope(); scope.Window.Width = scope.Window.MinWidth; scope.Window.Height = scope.Window.MinHeight;
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Stretch now"));
        foreach (var name in new[] { "TimerToggle", "TimerStop", "TimerReset" })
        {
            var button = Button(scope.Window, name); Assert.IsType<PathIcon>(button.Content);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))); Assert.NotNull(ToolTip.GetTip(button));
            var position = button.TranslatePoint(default, scope.Window)!.Value;
            Assert.True(position.X >= 0 && position.X + button.Bounds.Width <= scope.Window.ClientSize.Width);
            Assert.True(position.Y >= 0 && position.Y + button.Bounds.Height <= scope.Window.ClientSize.Height);
        }
        Press(scope.Window, "TimerToggle"); Assert.True(scope.Runtime.Clock.Paused);
        Assert.Equal("타이머 계속", AutomationProperties.GetName(Button(scope.Window, "TimerToggle")));
        Press(scope.Window, "TimerStop"); Assert.True(scope.Runtime.Clock.Stopped);
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
