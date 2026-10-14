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
    internal static double SurfaceHeight => new[] { DesignSystem.SpeechAdvanceHeight, DesignSystem.SpeechInvitationHeight,
        DesignSystem.SpeechRestingHeight, DesignSystem.SpeechCompletedHeight }.Max();
    private readonly PetWindow pet;
    private readonly AppRuntime runtime;
    private readonly Canvas canvas = new() { ClipToBounds = true };
    internal PetSpeechBubble Bubble { get; }
    internal PetTimerWindow Timer { get; }
    private readonly Avalonia.Controls.Shapes.Polygon tail = new()
        { Name = "PetSpeechTail", Fill = DesignSystem.Shell, IsHitTestVisible = false };
    private readonly Avalonia.Controls.Shapes.Polyline tailOutline = new()
        { Name = "PetSpeechTailOutline", Stroke = DesignSystem.Outline, StrokeThickness = 1, IsHitTestVisible = false };
    private readonly Button[] buttons;
    private IPointer? interactionPointer;
    private PixelRect? lastArea;
    internal BubbleDirection? Direction { get; private set; }
    internal bool IsClickThrough { get; private set; }
    internal bool HoldsPlacement => Timer.HoldsPlacement || (interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this)
        || OwnedWindows.Any(window => window.IsVisible);
    private bool closed;

    internal PetOverlayWindow(PetWindow pet, AppRuntime runtime)
    {
        this.pet = pet; this.runtime = runtime;
        Name = "PetOverlayWindow"; Title = L.T("Unfold · 펫 타이머");
        Width = DesignSystem.SpeechBubbleWidth + DesignSystem.PetBubbleGap; Height = SurfaceHeight;
        CanResize = false; WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Bubble = new(runtime.StartBreak, runtime.SnoozeBreak, runtime.CompleteBreak);
        Timer = new(pet, runtime);
        canvas.Children.Add(Bubble); canvas.Children.Add(tail); canvas.Children.Add(tailOutline);
        Content = canvas;
        // Cache only the application controls, excluding generated template descendants.
        buttons = Bubble.GetLogicalDescendants().OfType<Button>().ToArray();
        AddHandler(PointerPressedEvent, (_, e) => interactionPointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => interactionPointer = null,
            Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        PointerMoved += (_, _) => UpdateClickThrough();
        runtime.Changed += Refresh;
        Closed += (_, _) =>
        {
            closed = true; runtime.Changed -= Refresh; Timer.Close();
        };
    }

    internal void Refresh()
    {
        if (closed) return;
        Title = L.T("Unfold · 펫 타이머");
        var reminder = runtime.PresentedReminder;
        Bubble.Refresh(reminder, runtime.Settings.SnoozeMinutes, runtime.Settings);
        Bubble.IsVisible = tail.IsVisible = tailOutline.IsVisible = reminder.HasNotice;
        Bubble.Opacity = tail.Opacity = tailOutline.Opacity = runtime.Settings.BubbleOpacityPercent / 100d;
        Timer.Refresh();
        if (!pet.IsVisible || !Bubble.IsVisible)
        {
            Hide(); return;
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
        var body = pet.InteractiveScreenBounds;
        var placement = PetOverlayPlacement.Calculate(body, scale, area,
            area == lastArea ? Direction : null);
        Direction = placement.Direction; lastArea = area;
        Timer.Reposition();
        var timerRect = new PixelRect(Timer.Position, new PixelSize((int)Math.Ceiling(Timer.Width * scale), (int)Math.Ceiling(Timer.Height * scale)));
        placement = PetOverlayPlacement.Calculate(body, scale, area, Direction,
            !runtime.DiagnosticMode && Timer.IsVisible ? timerRect : null);
        if (runtime.DiagnosticMode)
        {
            MinWidth = placement.Width; MinHeight = SurfaceHeight;
        }
        Width = canvas.Width = placement.Width; Height = canvas.Height = SurfaceHeight;
        var localCenter = (body.Y + body.Height / 2d - placement.Position.Y) / scale;
        var gap = DesignSystem.PetBubbleGap;
        Bubble.Width = Math.Max(1, Width - gap);
        var top = Math.Clamp(localCenter - Bubble.Height / 2, 0, Math.Max(0, Height - Bubble.Height));
        Canvas.SetLeft(Bubble, Direction == BubbleDirection.Right ? gap : 0); Canvas.SetTop(Bubble, top);
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
        Timer.UpdateClickThrough();
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
        BubbleDirection? previous = null) => Calculate(new PixelRect(anchor,
            new PixelSize((int)Math.Ceiling(petSize * scale), (int)Math.Ceiling(petSize * scale))), scale, area, previous);

    internal static PetOverlayPlacement Calculate(PixelRect body, double scale, PixelRect area,
        BubbleDirection? previous = null, PixelRect? timer = null)
    {
        const double gap = 4;
        var center = body.X + body.Width / 2d;
        var middle = area.X + area.Width / 2d;
        var direction = center < middle ? BubbleDirection.Right : BubbleDirection.Left;
        if (previous is BubbleDirection.Left or BubbleDirection.Right && Math.Abs(center - middle) <= 24 * scale)
            direction = previous.Value;
        var left = Math.Max(0, (body.X - area.X) / scale - gap);
        var right = Math.Max(0, (area.Right - body.Right) / scale - gap);
        var maxWidth = DesignSystem.SpeechBubbleWidth + DesignSystem.PetBubbleGap;
        if ((direction == BubbleDirection.Left ? left : right) < maxWidth &&
            (direction == BubbleDirection.Left ? right : left) > (direction == BubbleDirection.Left ? left : right))
            direction = direction == BubbleDirection.Left ? BubbleDirection.Right : BubbleDirection.Left;
        var width = Math.Max(1, Math.Min(maxWidth, direction == BubbleDirection.Left ? left : right));
        var x = direction == BubbleDirection.Right ? body.Right + (int)Math.Ceiling(gap * scale)
            : body.X - (int)Math.Ceiling((width + gap) * scale);
        var height = (int)Math.Ceiling(PetOverlayWindow.SurfaceHeight * scale);
        var y = (int)Math.Round(body.Y + body.Height / 2d - height / 2d);
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height));
        if (timer is { } timerRect && new PixelRect(x, y, (int)Math.Ceiling(width * scale), height).Intersects(timerRect))
        {
            var above = timerRect.Y - height - (int)Math.Ceiling(8 * scale);
            if (above >= area.Y) y = Math.Min(y, above);
            else
            {
                // Small pets at the top edge can have a wider timer than their
                // body. Preserve both controls without covering either one.
                if (direction == BubbleDirection.Right)
                {
                    x = Math.Max(x, timerRect.Right + (int)Math.Ceiling(gap * scale));
                    width = Math.Max(1, Math.Min(width, (area.Right - x) / scale));
                }
                else
                {
                    var edge = Math.Min(body.X, timerRect.X) - (int)Math.Ceiling(gap * scale);
                    width = Math.Max(1, Math.Min(width, (edge - area.X) / scale));
                    x = edge - (int)Math.Ceiling(width * scale);
                }
            }
        }
        return new(new(x, y), width, direction);
    }
}
