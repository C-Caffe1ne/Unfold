using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>A separate native surface for reminders and timer controls; it never moves or arranges the pet.</summary>
internal sealed class PetOverlayWindow : Window
{
    internal const double TimerWidth = 208, TimerHeight = 56, RowGap = 8;
    internal static double SurfaceHeight => new[] { DesignSystem.SpeechAdvanceHeight, DesignSystem.SpeechInvitationHeight,
        DesignSystem.SpeechRestingHeight, DesignSystem.SpeechCompletedHeight }.Max() + RowGap + TimerHeight;
    private readonly PetWindow pet;
    private readonly AppRuntime runtime;
    private readonly Canvas canvas = new() { ClipToBounds = true };
    internal PetSpeechBubble Bubble { get; }
    private readonly Avalonia.Controls.Shapes.Polygon tail = new()
        { Name = "PetSpeechTail", Fill = DesignSystem.Shell, IsHitTestVisible = false };
    private readonly Avalonia.Controls.Shapes.Polyline tailOutline = new()
        { Name = "PetSpeechTailOutline", Stroke = DesignSystem.Outline, StrokeThickness = 1, IsHitTestVisible = false };
    private readonly AnimatedCountdown countdown = new()
        { Name = "PetTimerCountdown", FontSize = 20, LineHeight = 26, VerticalAlignment = VerticalAlignment.Center };
    private readonly TimerControls timerControls;
    private readonly Border timerBar;
    private readonly Button[] buttons;
    private IPointer? interactionPointer;
    private PixelRect? lastArea;
    internal BubbleDirection? Direction { get; private set; }
    internal bool IsClickThrough { get; private set; }
    internal bool HoldsPlacement => (interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this)
        || OwnedWindows.Any(window => window.IsVisible);
    private bool closed;

    internal PetOverlayWindow(PetWindow pet, AppRuntime runtime)
    {
        this.pet = pet; this.runtime = runtime;
        Name = "PetOverlayWindow"; Title = "Unfold · 펫 타이머";
        Width = DesignSystem.SpeechBubbleWidth + DesignSystem.PetBubbleGap; Height = SurfaceHeight;
        CanResize = false; WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Bubble = new(runtime.StartBreak, runtime.SnoozeBreak, runtime.CompleteBreak);
        timerControls = new(runtime.TogglePause, () => _ = runtime.RequestStop(), compact: true);
        var row = new Grid { ColumnDefinitions = new("*,8,Auto") };
        row.Children.Add(countdown); Grid.SetColumn(timerControls, 2); row.Children.Add(timerControls);
        timerBar = new Border { Name = "PetTimerBar", Width = TimerWidth, Height = TimerHeight, Padding = new Thickness(6, 5),
            Background = DesignSystem.Shell, BorderBrush = DesignSystem.Outline, BorderThickness = new Thickness(1),
            CornerRadius = DesignSystem.CardRadius, Child = row, ClipToBounds = true };
        canvas.Children.Add(Bubble); canvas.Children.Add(tail); canvas.Children.Add(tailOutline); canvas.Children.Add(timerBar);
        Content = canvas;
        // Cache only the application controls, excluding generated template descendants.
        buttons = Bubble.GetLogicalDescendants().OfType<Button>().Concat(timerControls.Children.OfType<Button>()).ToArray();
        AddHandler(PointerPressedEvent, (_, e) => interactionPointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => interactionPointer = null,
            Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        PointerMoved += (_, _) => UpdateClickThrough();
        runtime.Changed += Refresh;
        Closed += (_, _) =>
        {
            closed = true; runtime.Changed -= Refresh; countdown.Dispose();
        };
    }

    internal void Refresh()
    {
        if (closed) return;
        var reminder = runtime.PresentedReminder;
        Bubble.Refresh(reminder, runtime.Settings.SnoozeMinutes, runtime.Settings);
        Bubble.IsVisible = tail.IsVisible = tailOutline.IsVisible = reminder.HasNotice;
        Bubble.Opacity = tail.Opacity = tailOutline.Opacity = runtime.Settings.BubbleOpacityPercent / 100d;
        timerBar.IsVisible = runtime.Settings.ShowPetTimer;
        var active = !runtime.Clock.Paused && !runtime.Clock.Stopped && !runtime.Clock.IdlePaused &&
            reminder.Notice is not (PetNotice.Invitation or PetNotice.Resting);
        countdown.UpdateTime(runtime.Clock.Remaining, active);
        timerControls.Refresh(runtime.Clock, runtime.Reminder.HasNotice);
        var clockState = runtime.Clock.Stopped ? "중지" : runtime.Clock.Paused ? "일시정지" : runtime.Clock.IdlePaused ? "자리 비움" : "진행 중";
        AutomationProperties.SetName(timerBar, TrayReminderStatus.Create(reminder.Notice, countdown.Text, clockState).Status);
        if (!pet.IsVisible || (!timerBar.IsVisible && !Bubble.IsVisible))
        {
            countdown.StopAnimation(); Hide(); return;
        }
        Reposition();
        if (!IsVisible)
        {
            Show(pet); MacPetWindow.Apply(this);
        }
        UpdateClickThrough();
    }

    internal void Reposition()
    {
        if (closed || !pet.IsVisible) return;
        var scale = pet.DesktopScaling;
        var anchor = pet.PetAnchor;
        var screen = pet.Screens.ScreenFromPoint(anchor) ?? pet.Screens.Primary;
        var area = screen?.WorkingArea ?? new PixelRect(anchor.X - 1000, anchor.Y - 600, 2400, 1600);
        var placement = PetOverlayPlacement.Calculate(anchor, pet.PetView.Width, scale, area,
            area == lastArea ? Direction : null);
        Direction = placement.Direction; lastArea = area;
        if (runtime.DiagnosticMode)
        {
            MinWidth = placement.Width; MinHeight = SurfaceHeight;
        }
        Width = canvas.Width = placement.Width; Height = canvas.Height = SurfaceHeight;
        var localCenter = (anchor.Y + pet.PetView.Height * scale / 2 - placement.Position.Y) / scale;
        var gap = DesignSystem.PetBubbleGap;
        Bubble.Width = Math.Max(1, Width - gap);
        timerBar.Width = Math.Min(TimerWidth, Width);
        var groupHeight = Bubble.Height + (timerBar.IsVisible ? RowGap + TimerHeight : 0);
        var top = Math.Clamp(localCenter - groupHeight / 2, 0, Math.Max(0, Height - groupHeight));
        Canvas.SetLeft(Bubble, Direction == BubbleDirection.Right ? gap : 0); Canvas.SetTop(Bubble, top);
        Canvas.SetLeft(timerBar, Direction == BubbleDirection.Right ? 0 : Width - timerBar.Width);
        Canvas.SetTop(timerBar, Bubble.IsVisible ? top + Bubble.Height + RowGap :
            Math.Clamp(localCenter - TimerHeight / 2, 0, Height - TimerHeight));
        var center = Math.Clamp(localCenter, top + 12, top + Bubble.Height - 12);
        var edge = Direction == BubbleDirection.Right ? gap : Width - gap;
        var tip = Direction == BubbleDirection.Right ? 0 : Width;
        tail.Points = new AvaloniaList<Point>([new(edge, center - 10), new(edge, center + 10), new(tip, center)]);
        tailOutline.Points = new AvaloniaList<Point>([tail.Points[0], tail.Points[2], tail.Points[1]]);
        // All layout and position updates are confined to this native surface.
        Position = runtime.DiagnosticMode ? new(-32000, -32000) : placement.Position;
    }

    internal bool AcceptsPointerAt(Point point)
    {
        if (!IsVisible) return false;
        if (interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this) return true;
        return buttons.Any(button => button.IsEffectivelyVisible && button.TranslatePoint(default, this) is { } origin &&
            new Rect(origin, button.Bounds.Size).Contains(point));
    }

    internal void UpdateClickThrough()
    {
        if (closed || !IsVisible || runtime.DiagnosticMode) return;
        Point point;
        if (!(OperatingSystem.IsMacOS() ? MacPetWindow.TryGetPointer(this, out point) : WindowsPetWindow.TryGetPointer(this, out point))) return;
        var ignore = !AcceptsPointerAt(point);
        if (OperatingSystem.IsWindows())
        {
            if (WindowsPetWindow.SetClickThrough(this, ignore)) IsClickThrough = ignore;
        }
        else if (ignore != IsClickThrough && MacPetWindow.SetClickThrough(this, ignore)) IsClickThrough = ignore;
    }

    internal void FocusReminder()
    {
        Refresh();
        if (IsVisible && Bubble.IsVisible) { Activate(); Bubble.FocusAction(); }
    }

    internal void PositionDialog(Window dialog)
    {
        // A regular modal can be composited below its floating owner on macOS.
        // Keep the confirmation on the same native level so it covers the controls.
        dialog.Topmost = Topmost;
        var scale = pet.DesktopScaling;
        var area = pet.Screens.ScreenFromPoint(pet.PetAnchor)?.WorkingArea;
        var width = (int)Math.Ceiling(dialog.Width * scale);
        var height = (int)Math.Ceiling(dialog.Height * scale);
        var gap = (int)Math.Ceiling(DesignSystem.PetBubbleGap * scale);
        var x = Direction == BubbleDirection.Left ? pet.PetAnchor.X - gap - width
            : pet.PetAnchor.X + (int)Math.Ceiling(pet.PetView.Width * scale) + gap;
        var y = Position.Y + (int)Math.Round((Height * scale - height) / 2);
        if (area is { } work)
        {
            if (x < work.X || x + width > work.Right)
            {
                var opposite = Direction == BubbleDirection.Left ? pet.PetAnchor.X + (int)Math.Ceiling(pet.PetView.Width * scale) + gap
                    : pet.PetAnchor.X - gap - width;
                if (opposite >= work.X && opposite + width <= work.Right) x = opposite;
            }
            x = Math.Clamp(x, work.X, Math.Max(work.X, work.Right - width));
            y = Math.Clamp(y, work.Y, Math.Max(work.Y, work.Bottom - height));
        }
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Position = new(x, y);
    }
}

internal readonly record struct PetOverlayPlacement(PixelPoint Position, double Width, BubbleDirection Direction)
{
    internal static PetOverlayPlacement Calculate(PixelPoint anchor, double petSize, double scale, PixelRect area,
        BubbleDirection? previous = null)
    {
        var gap = DesignSystem.PetBubbleGap;
        var center = anchor.X + petSize * scale / 2;
        var middle = area.X + area.Width / 2d;
        var direction = center < middle ? BubbleDirection.Right : BubbleDirection.Left;
        if (previous is BubbleDirection.Left or BubbleDirection.Right && Math.Abs(center - middle) <= 24 * scale)
            direction = previous.Value;
        var left = Math.Max(0, (anchor.X - area.X) / scale - gap);
        var right = Math.Max(0, (area.Right - anchor.X) / scale - petSize - gap);
        var maxWidth = DesignSystem.SpeechBubbleWidth + gap;
        if ((direction == BubbleDirection.Left ? left : right) < maxWidth &&
            (direction == BubbleDirection.Left ? right : left) > (direction == BubbleDirection.Left ? left : right))
            direction = direction == BubbleDirection.Left ? BubbleDirection.Right : BubbleDirection.Left;
        var width = Math.Max(1, Math.Min(maxWidth, direction == BubbleDirection.Left ? left : right));
        var x = direction == BubbleDirection.Right ? anchor.X + (int)Math.Ceiling((petSize + gap) * scale)
            : anchor.X - (int)Math.Ceiling((width + gap) * scale);
        var height = (int)Math.Ceiling(PetOverlayWindow.SurfaceHeight * scale);
        var y = (int)Math.Round(anchor.Y + petSize * scale / 2 - height / 2d);
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height));
        return new(new(x, y), width, direction);
    }
}
