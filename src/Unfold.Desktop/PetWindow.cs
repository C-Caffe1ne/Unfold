using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using PixelPoint = Avalonia.PixelPoint;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PetWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly Canvas canvas = new();
    private readonly PetSpeechBubble bubble;
    private readonly Avalonia.Controls.Shapes.Polygon tail = new() { Fill = DesignSystem.Shell, Stroke = DesignSystem.Outline, StrokeThickness = 1, IsHitTestVisible = false };
    private readonly MenuItem fold = new();
    // A folded bubble leaves the pet with no sign that a break is waiting. The badge is the
    // quietest signal that still says so, and it never takes a pointer: the pet stays draggable
    // and click-through keeps following the sprite's opaque pixels.
    private readonly Avalonia.Controls.Shapes.Ellipse badgeMark = new()
    {
        Name = "PetReminderBadgeMark", Width = 10, Height = 10,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Border badge = new()
    {
        Name = "PetReminderBadge", Width = 22, Height = 22, CornerRadius = new(11),
        Background = DesignSystem.Shell, BorderBrush = DesignSystem.Outline, BorderThickness = new(1),
        IsHitTestVisible = false, IsVisible = false
    };
    private ReminderBadge badgeState = ReminderBadge.None;
    public ReminderBadge BadgeState => badgeState;
    private PetBubbleLayout layout = PetBubbleLayout.Create(BubbleDirection.Top, false);
    private double layoutScale = 1;
    public PixelPoint PetAnchor => new(Position.X + (int)Math.Round(layout.Pet.X * DesktopScaling), Position.Y + (int)Math.Round(layout.Pet.Y * DesktopScaling));
    private readonly AnimationView animation = new() { Width = 192, Height = 192 };
    // Content is the layout canvas that also carries the bubble and tail, and its shape
    // changes with the bubble layout. Diagnostics and tests read playback state and render
    // the pet through this contract instead of casting Content or walking the visual tree.
    internal AnimationView PetView => animation;
    private readonly DispatcherTimer hitTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private Avalonia.PixelPoint? down;
    private Avalonia.PixelPoint origin;
    private bool dragging, clickThrough;
    private long pressedAt;
    private int generation;
    private CharacterPackage? character;
    public PetWindow(AppRuntime runtime)
    {
        this.runtime = runtime;
        Width = Height = 192; CanResize = false; WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        bubble = new(runtime.StartBreak, runtime.SnoozeBreak, runtime.CompleteBreak);
        badge.Child = badgeMark;
        canvas.Children.Add(animation); canvas.Children.Add(bubble); canvas.Children.Add(tail); canvas.Children.Add(badge); Content = canvas;
        bubble.IsVisible = tail.IsVisible = false;
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "설정" }; settings.Click += (_, _) => runtime.ShowSettings();
        fold.Name = "PetToggleSpeech"; fold.Click += async (_, _) => await runtime.ToggleBubble();
        menu.Opening += (_, _) => fold.Header = runtime.Settings.BubbleCollapsed ? "말풍선 펼치기" : "말풍선 접기";
        menu.Items.Add(fold); menu.Items.Add(settings); ContextMenu = menu;
        animation.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(animation).Properties.IsLeftButtonPressed || !animation.OpaqueAt(e.GetPosition(animation))) return;
            down = animation.PointToScreen(e.GetPosition(animation)); origin = Position; pressedAt = Stopwatch.GetTimestamp(); dragging = false;
            e.Pointer.Capture(animation); e.Handled = true;
        };
        animation.PointerMoved += (_, e) =>
        {
            if (down is not { } start) return;
            var current = animation.PointToScreen(e.GetPosition(animation)); var delta = current - start;
            if (Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y) >= 5) dragging = true;
            if (dragging) Position = new(origin.X + delta.X, origin.Y + delta.Y);
        };
        animation.PointerReleased += async (_, e) =>
        {
            if (down is null) return;
            var clicked = !dragging && Stopwatch.GetElapsedTime(pressedAt).TotalSeconds <= 0.22;
            down = null; e.Pointer.Capture(null); ClampPosition(); runtime.SavePosition(PetAnchor);
            if (clicked) await React();
        };
        animation.PointerCaptureLost += (_, _) => { down = null; };
        hitTimer.Tick += (_, _) => UpdateClickThrough();
        Opened += (_, _) =>
        {
            if (runtime.DiagnosticMode) { Position = new(-32000, -32000); return; }
            var work = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            Position = runtime.Settings.PetX is int x && runtime.Settings.PetY is int y ? new(x, y) : new(work.Right - (int)(Width * DesktopScaling) - 24, work.Bottom - (int)(Height * DesktopScaling) - 24);
            ClampPosition(); hitTimer.Start();
        };
        Closed += (_, _) => { hitTimer.Stop(); animation.Dispose(); };
    }
    public async Task SetCharacter()
    {
        var selected = runtime.Selected;
        if (character == selected) return;
        var current = ++generation; var frames = await runtime.Clip("idle");
        if (current != generation) return;
        character = selected; animation.SetFrames(frames, true, selected?.Manifest.RenderStyle == "pixel");
    }
    internal async Task React(string? preferred = null)
    {
        try
        {
            var selected = runtime.Selected;
            // Stretch belongs to reminders. A plain click reacts only when the character
            // ships a click clip, and otherwise leaves the idle loop alone — so the early
            // return has to happen before generation moves, or it would cancel a stretch.
            var key = preferred ?? (selected?.Manifest.Animations.ContainsKey("click") == true ? "click" : null);
            if (key is null || selected?.Manifest.Animations.ContainsKey(key) != true) return;
            var current = ++generation;
            var frames = await runtime.Clip(key); if (current != generation) return;
            animation.SetFrames(frames, false, selected?.Manifest.RenderStyle == "pixel");
            var duration = frames.Sum(f => f.Duration.TotalMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(duration, 200, 10000)));
            if (current == generation) { character = null; await SetCharacter(); }
        }
        catch (Exception error) { AppPaths.Log(error); }
    }
    public void ShowPet() { Show(); RefreshSpeech(); hitTimer.Start(); animation.SetRunning(true); }
    public void HidePet() { generation++; character = null; Hide(); hitTimer.Stop(); animation.SetRunning(false); }
    public void ClosePet() { generation++; hitTimer.Stop(); Close(); }
    public void RefreshSpeech()
    {
        bubble.Refresh(runtime.Reminder, runtime.Settings.SnoozeMinutes);
        fold.Header = runtime.Settings.BubbleCollapsed ? "말풍선 펼치기" : "말풍선 접기";
        var expanded = runtime.Reminder.HasNotice && !runtime.Settings.BubbleCollapsed;
        var next = PetBubbleLayout.Create(runtime.Settings.BubbleDirection, expanded);
        // The badge tracks the reminder even when the layout is unchanged, so it has to be
        // updated ahead of the early return below.
        RefreshBadge(next.Pet);
        if (layout.Size == next.Size && layout.Pet == next.Pet && bubble.IsVisible == expanded && layoutScale == DesktopScaling) return;
        var anchor = PetAnchor;
        layoutScale = DesktopScaling; layout = next; Width = layout.Size.Width; Height = layout.Size.Height;
        // Diagnostic minimums follow the live canvas instead of preventing a collapse.
        if (runtime.DiagnosticMode) { MinWidth = Width; MinHeight = Height; }
        canvas.Width = Width; canvas.Height = Height;
        Canvas.SetLeft(animation, layout.Pet.X); Canvas.SetTop(animation, layout.Pet.Y);
        Canvas.SetLeft(bubble, layout.Bubble.X); Canvas.SetTop(bubble, layout.Bubble.Y);
        tail.Points = new Avalonia.Collections.AvaloniaList<Point>(layout.Tail);
        bubble.IsVisible = tail.IsVisible = expanded;
        var work = Screens.ScreenFromPoint(anchor)?.WorkingArea ?? Screens.Primary?.WorkingArea;
        Position = runtime.DiagnosticMode ? new(-32000, -32000) : work is { } area
            ? layout.Position(anchor, DesktopScaling, area) : anchor;
    }

    private void RefreshBadge(Point pet)
    {
        var state = TrayReminderStatus.BadgeFor(runtime.Reminder.Notice, runtime.Settings.BubbleCollapsed);
        if (state != badgeState)
        {
            badgeState = state;
            // Waiting draws an open ring, resting a filled dot. Shape carries the difference so
            // the two states stay apart without colour vision or a caption.
            badgeMark.Fill = state == ReminderBadge.Resting ? DesignSystem.Cream : Brushes.Transparent;
            badgeMark.Stroke = state == ReminderBadge.Waiting ? DesignSystem.Warning : Brushes.Transparent;
            badgeMark.StrokeThickness = state == ReminderBadge.Waiting ? 2 : 0;
            badge.IsVisible = state != ReminderBadge.None;
            AutomationProperties.SetName(badge, state switch
            { ReminderBadge.Waiting => "휴식 대기 중", ReminderBadge.Resting => "휴식 중", _ => "" });
        }
        Canvas.SetLeft(badge, pet.X + animation.Width - badge.Width - 6); Canvas.SetTop(badge, pet.Y + 6);
    }

    private void ClampPosition()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary; if (screen is null) return;
        var work = screen.WorkingArea; var width = (int)(Width * DesktopScaling); var height = (int)(Height * DesktopScaling);
        Position = new(Math.Clamp(Position.X, work.X, Math.Max(work.X, work.Right - width)), Math.Clamp(Position.Y, work.Y, Math.Max(work.Y, work.Bottom - height)));
    }
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLong(nint hwnd, int index, nint value);
    private void UpdateClickThrough()
    {
        if (!OperatingSystem.IsWindows() || down is not null) return;
        if (!GetCursorPos(out var point)) return;
        var cursor = new PixelPoint(point.X, point.Y);
        var onBubble = bubble.IsVisible && new Rect(bubble.Bounds.Size).Contains(bubble.PointToClient(cursor));
        var ignore = !onBubble && !animation.OpaqueAt(animation.PointToClient(cursor));
        if (ignore == clickThrough) return;
        if (TryGetPlatformHandle()?.Handle is not nint hwnd || hwnd == 0) return;
        var style = (long)GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, (nint)(ignore ? style | 0x20 : style & ~0x20)); clickThrough = ignore;
    }
}
