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
    private readonly TextBlock instruction = new() { TextWrapping = TextWrapping.Wrap, FontSize = 16, MinHeight = 46, Foreground = DesignSystem.Cream, TextAlignment = TextAlignment.Center };
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
        Title = "잠깐의 휴식 · Unfold"; Width = 440; Height = 565; CanResize = false;
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
        var snooze = Ui.Button("5분 뒤에", () => { if (Session.Snooze()) Close(); });
        var skip = Ui.Button("이번 휴식 건너뛰기", () => { if (Session.Skip()) Close(); });
        var secondary = Ui.Row(snooze, skip); secondary.HorizontalAlignment = HorizontalAlignment.Center;
        Ui.Primary(primary); Ui.Quiet(skip);
        var body = Ui.Column(animation, instruction, countdown, progress);
        body.Spacing = 10;
        Content = Ui.Page(this, session.Routine.Name, $"{companionName}와 함께 {session.Routine.DurationSeconds}초 휴식",
            body, Ui.Column(primary, secondary), "잠깐의 여유", inset: 16);
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
            BreakSessionState.Ready => "하던 일은 잠깐 내려놓아도 괜찮아요.",
            BreakSessionState.AwaitingConfirmation => "돌아갈 준비가 됐나요? 필요하면 조금 더 쉬어도 좋아요.",
            _ => Session.CurrentStep.Instruction
        };
        primary.IsEnabled = Session.State is BreakSessionState.Ready or BreakSessionState.AwaitingConfirmation;
        primary.Content = Session.State switch
        {
            BreakSessionState.Ready => $"{Session.Routine.DurationSeconds}초 휴식 시작",
            BreakSessionState.AwaitingConfirmation => "잘 쉬었어요",
            _ => "천천히 쉬어 가세요…"
        };
    }
}
