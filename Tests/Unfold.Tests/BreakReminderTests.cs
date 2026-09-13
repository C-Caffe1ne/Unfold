using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class BreakReminderTests
{
    private static Button Button(Window window, string content) => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, content));
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static BreakReminderWindow Window(BreakSession session, Func<TimeSpan> now) => new(session, "Mochi",
        [new(new(2, 2, [0xFFFFAA00, 0, 0, 0xFFFFAA00]), TimeSpan.FromMilliseconds(100))], true, now);
    [AvaloniaFact]
    public void RealButtonsStartAndConfirmExactlyOneSession()
    {
        var now = TimeSpan.Zero; var session = new BreakSession(BreakRoutines.Find("look-away")!, "default-cat");
        var window = Window(session, () => now); var started = 0; var finished = 0;
        window.Started += () => started++; window.Finished += _ => finished++;
        window.Show(); Dispatcher.UIThread.RunJobs();
        Click(Button(window, "Start 20-second break"));
        Assert.Equal(1, started); Assert.Equal(BreakSessionState.InProgress, session.State);
        Assert.False(Button(window, "Take your time…").IsEnabled);
        now = TimeSpan.FromSeconds(10); window.RefreshProgress();
        now = TimeSpan.FromSeconds(20); window.RefreshProgress();
        Assert.Equal(0, finished); Assert.Equal(BreakSessionState.AwaitingConfirmation, session.State);
        Click(Button(window, "I'm refreshed"));
        Assert.Equal(1, finished); Assert.Equal(BreakSessionState.Completed, session.State);
    }
    [AvaloniaFact]
    public void SnoozeAndWindowCloseRemainDistinctOutcomes()
    {
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); var window = Window(session, () => TimeSpan.Zero);
        window.Show(); Dispatcher.UIThread.RunJobs(); Click(Button(window, "In 5 minutes"));
        Assert.Equal(BreakSessionState.Snoozed, session.State);
        var second = new BreakSession(BreakRoutines.All[0], "default-cat"); var other = Window(second, () => TimeSpan.Zero);
        other.Show(); other.Close(); Assert.Equal(BreakSessionState.Skipped, second.State);
    }
    [AvaloniaFact]
    public void ReminderControlsFitAndRenderAtDefaultSize()
    {
        var session = new BreakSession(BreakRoutines.All[0], "default-cat"); var window = Window(session, () => TimeSpan.Zero);
        window.Show(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var image = window.CaptureRenderedFrame(); Assert.NotNull(image);
        foreach (var button in window.GetVisualDescendants().OfType<Button>())
        {
            var point = button.TranslatePoint(default, window)!.Value;
            Assert.True(point.X >= 0 && point.X + button.Bounds.Width <= window.ClientSize.Width);
            Assert.True(point.Y >= 0 && point.Y + button.Bounds.Height <= window.ClientSize.Height);
        }
        window.Close();
    }
}
