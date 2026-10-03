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
    private readonly TextBlock toggleText = new() { Text = "일시정지", VerticalAlignment = VerticalAlignment.Center };
    private string? currentLabel;
    public TimerControls(Action togglePause, Action stop)
    {
        Orientation = Orientation.Horizontal; Spacing = 14;
        toggle = ActionButton("TimerToggle", "타이머 일시정지", toggleIcon, toggleText, togglePause);
        toggle.Classes.Add("primary");
        this.stop = ActionButton("TimerStop", "타이머 중지", new PlaybackIcon(PlaybackGlyph.Stop) { Width = 18, Height = 18 },
            new TextBlock { Text = "중지", VerticalAlignment = VerticalAlignment.Center }, stop);
        Children.Add(toggle); Children.Add(this.stop);
    }
    public void Refresh(StretchClock clock, bool hasActiveReminder = false)
    {
        var label = clock.Stopped ? "타이머 시작" : clock.Paused ? "타이머 계속" : "타이머 일시정지";
        stop.IsEnabled = !clock.Stopped || hasActiveReminder;
        if (label == currentLabel) return;
        currentLabel = label;
        toggleIcon.Glyph = clock.Paused || clock.Stopped ? PlaybackGlyph.Play : PlaybackGlyph.Pause;
        toggleText.Text = clock.Stopped ? "시작" : clock.Paused ? "계속" : "일시정지";
        AutomationProperties.SetName(toggle, label); ToolTip.SetTip(toggle, label);
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
