using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class TimerControls : StackPanel
{
    private static readonly Geometry PauseIcon = Geometry.Parse("M 6,4 H 10 V 20 H 6 Z M 14,4 H 18 V 20 H 14 Z");
    private static readonly Geometry PlayIcon = Geometry.Parse("M 7,4 L 21,12 L 7,20 Z");
    private static readonly Geometry StopIcon = Geometry.Parse("M 5,5 H 19 V 19 H 5 Z");
    private readonly Button toggle;
    private string? currentLabel;
    public TimerControls(Action togglePause, Action stop)
    {
        Orientation = Orientation.Horizontal; Spacing = 8;
        toggle = IconButton("TimerToggle", "타이머 일시정지", PauseIcon, togglePause);
        Children.Add(toggle); Children.Add(IconButton("TimerStop", "타이머 정지", StopIcon, stop));
    }
    public void Refresh(StretchClock clock)
    {
        var label = clock.Stopped ? "타이머 시작" : clock.Paused ? "타이머 계속" : "타이머 일시정지";
        if (label == currentLabel) return;
        currentLabel = label; ((PathIcon)toggle.Content!).Data = clock.Paused ? PlayIcon : PauseIcon;
        AutomationProperties.SetName(toggle, label); ToolTip.SetTip(toggle, label);
    }
    private static Button IconButton(string name, string label, Geometry icon, Action action)
    {
        var button = new Button { Name = name, Classes = { "unfold-action" }, Width = 44, Height = 44, Padding = new Thickness(10),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new PathIcon { Width = 20, Height = 20, Data = icon, Foreground = DesignSystem.Cream } };
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        button.Click += (_, _) => action(); return button;
    }
}
