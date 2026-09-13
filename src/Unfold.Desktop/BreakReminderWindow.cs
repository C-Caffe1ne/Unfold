using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class BreakReminderWindow : Window
{
    public BreakSession Session { get; }
    public event Action? Started;
    public event Action<BreakSession>? Finished;
    private readonly AnimationView animation = new() { Width = 168, Height = 168, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock instruction = new() { TextWrapping = TextWrapping.Wrap, FontSize = 16, MinHeight = 46, Foreground = Brushes.White, TextAlignment = TextAlignment.Center };
    private readonly TextBlock countdown = Ui.Text("", 32, Ui.Accent);
    private readonly ProgressBar progress = new() { Height = 6 };
    private readonly Button primary;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Func<TimeSpan> now;

    public BreakReminderWindow(BreakSession session, string companionName, IReadOnlyList<AnimationFrame> idleFrames,
        bool pixel, Func<TimeSpan>? monotonicTime = null)
    {
        Session = session;
        var stopwatch = Stopwatch.StartNew(); now = monotonicTime ?? (() => stopwatch.Elapsed);
        Title = "A small break · Unfold"; Width = 440; Height = 565; CanResize = false;
        Topmost = true; Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        animation.SetFrames(idleFrames, true, pixel);
        countdown.HorizontalAlignment = HorizontalAlignment.Center;
        progress.Maximum = session.Routine.DurationSeconds;
        primary = Ui.Button("", () =>
        {
            if (Session.State == BreakSessionState.Ready)
            {
                Session.Start(now()); Started?.Invoke(); RefreshProgress();
            }
            else if (Session.Complete()) Close();
        });
        primary.HorizontalAlignment = HorizontalAlignment.Stretch; primary.HorizontalContentAlignment = HorizontalAlignment.Center;
        primary.IsDefault = true;
        var snooze = Ui.Button("In 5 minutes", () => { if (Session.Snooze()) Close(); });
        var skip = Ui.Button("Skip this break", () => { if (Session.Skip()) Close(); });
        var secondary = Ui.Row(snooze, skip); secondary.HorizontalAlignment = HorizontalAlignment.Center;
        var title = Ui.Text(session.Routine.Name, 26); title.TextAlignment = TextAlignment.Center;
        title.TextWrapping = TextWrapping.Wrap;
        var subtitle = Ui.Text($"{session.Routine.DurationSeconds} seconds with {companionName}", 14);
        subtitle.TextWrapping = TextWrapping.Wrap; subtitle.TextAlignment = TextAlignment.Center;
        var body = Ui.Column(title, subtitle, animation, instruction, countdown, progress, primary, secondary);
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(28, 22), Child = body } };
        timer.Tick += (_, _) => RefreshProgress();
        Opened += (_, _) => timer.Start();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Session.Skip(); Close(); e.Handled = true; } };
        Closed += (_, _) =>
        {
            timer.Stop(); animation.Dispose(); Session.Skip(); Finished?.Invoke(Session);
        };
        RefreshProgress();
    }
    public void RefreshProgress()
    {
        Session.Tick(now());
        countdown.Text = $"{(int)Math.Ceiling(Session.Remaining.TotalSeconds) / 60:00}:{(int)Math.Ceiling(Session.Remaining.TotalSeconds) % 60:00}";
        progress.Value = Session.Elapsed.TotalSeconds;
        instruction.Text = Session.State switch
        {
            BreakSessionState.Ready => "Your work can wait for a small moment.",
            BreakSessionState.AwaitingConfirmation => "Ready to return? Keep the pause as long as you need.",
            _ => Session.CurrentStep.Instruction
        };
        primary.IsEnabled = Session.State is BreakSessionState.Ready or BreakSessionState.AwaitingConfirmation;
        primary.Content = Session.State switch
        {
            BreakSessionState.Ready => $"Start {Session.Routine.DurationSeconds}-second break",
            BreakSessionState.AwaitingConfirmation => "I'm refreshed",
            _ => "Take your time…"
        };
    }
}
