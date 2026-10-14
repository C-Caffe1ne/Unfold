using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class TimerControls : StackPanel
{
    private readonly Button toggle;
    private readonly Button stop;
    private readonly PlaybackIcon toggleIcon = new(PlaybackGlyph.Pause) { Width = 18, Height = 18 };
    private readonly TextBlock toggleText = new() { Text = L.T("일시정지"), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock stopText = new() { Text = L.T("중지"), VerticalAlignment = VerticalAlignment.Center };
    private string? currentLabel;
    public TimerControls(Action togglePause, Action stop, bool compact = false)
    {
        Orientation = Orientation.Horizontal; Spacing = compact ? 6 : 14;
        toggle = ActionButton(compact ? "PetTimerToggle" : "TimerToggle", L.T("타이머 일시정지"), toggleIcon, toggleText, togglePause);
        toggle.Classes.Add("primary");
        this.stop = ActionButton(compact ? "PetTimerStop" : "TimerStop", L.T("타이머 중지"), new PlaybackIcon(PlaybackGlyph.Stop) { Width = 18, Height = 18 },
            stopText, stop);
        Ui.Danger(this.stop);
        if (compact)
        {
            toggle.Width = this.stop.Width = 44;
            toggle.Padding = this.stop.Padding = new Thickness(0);
            toggleText.IsVisible = false;
            stopText.IsVisible = false;
        }
        Children.Add(toggle); Children.Add(this.stop);
    }
    public void Refresh(StretchClock clock, bool hasActiveReminder = false)
    {
        var label = clock.Stopped ? L.T("타이머 시작") : clock.Paused ? L.T("타이머 계속") : L.T("타이머 일시정지");
        stop.IsEnabled = !clock.Stopped || hasActiveReminder;
        if (label == currentLabel) return;
        currentLabel = label;
        toggleIcon.Glyph = clock.Paused || clock.Stopped ? PlaybackGlyph.Play : PlaybackGlyph.Pause;
        toggleText.Text = clock.Stopped ? L.T("시작") : clock.Paused ? L.T("계속") : L.T("일시정지");
        AutomationProperties.SetName(toggle, label); ToolTip.SetTip(toggle, label);
        // A language change also changes the label, so the stop text follows here.
        stopText.Text = L.T("중지");
        AutomationProperties.SetName(stop, L.T("타이머 중지")); ToolTip.SetTip(stop, L.T("타이머 중지"));
    }
    private static Button ActionButton(string name, string label, PlaybackIcon icon, TextBlock text, Action action)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        row.Children.Add(icon); row.Children.Add(text);
        var button = new Button { Name = name, Classes = { "unfold-action", "timer-control" }, Width = 110, Height = 44,
            Padding = new Thickness(12, 8), FontSize = DesignSystem.Caption,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = row };
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        button.Click += (_, _) => action(); return button;
    }
}
