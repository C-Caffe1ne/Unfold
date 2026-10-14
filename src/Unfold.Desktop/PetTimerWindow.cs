using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>A small independent native surface below the pet's visible feet.</summary>
internal sealed class PetTimerWindow : Window
{
    internal const double TimerWidth = 208, TimerHeight = 78, PetGap = 8;
    private readonly PetWindow pet;
    private readonly AppRuntime runtime;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly PetTimerReveal reveal = new();
    private readonly TextBlock currentTime = new() { Name = "PetCurrentTime", FontSize = 13, LineHeight = 18,
        Foreground = DesignSystem.Cream, TextAlignment = TextAlignment.Center };
    private readonly AnimatedCountdown countdown = new() { Name = "PetTimerCountdown", FontSize = 20, LineHeight = 26,
        VerticalAlignment = VerticalAlignment.Center };
    private readonly TimerControls controls;
    private readonly Border timerBar;
    private IPointer? interactionPointer;
    private bool pointerOverTimer;
    private Rect? suppressedPetArea;
    private TimeSpan lastTimeRefresh;
    private bool closed;
    internal double HoverOpacity => timerBar.Opacity;
    internal bool IsClickThrough { get; private set; }
    internal bool HoldsPlacement => pointerOverTimer || HasCapture;
    private bool HasCapture => interactionPointer?.Captured is Visual captured && TopLevel.GetTopLevel(captured) == this;

    internal PetTimerWindow(PetWindow pet, AppRuntime runtime)
    {
        this.pet = pet; this.runtime = runtime;
        Name = "PetTimerWindow"; Title = L.T("Unfold · 타이머");
        Width = MinWidth = MaxWidth = TimerWidth;
        Height = MinHeight = MaxHeight = TimerHeight; CanResize = false;
        WindowDecorations = WindowDecorations.None; Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        controls = new(runtime.TogglePause, () => _ = runtime.RequestStop(), compact: true);
        var row = new Grid { ColumnDefinitions = new("*,8,Auto") };
        row.Children.Add(countdown); Grid.SetColumn(controls, 2); row.Children.Add(controls);
        var body = new Grid { RowDefinitions = new("18,4,44") };
        body.Children.Add(currentTime); Grid.SetRow(row, 2); body.Children.Add(row);
        timerBar = new Border { Name = "PetTimerBar", Width = TimerWidth, Height = TimerHeight, Opacity = 0,
            Padding = new Thickness(6, 5), Background = DesignSystem.Shell, BorderBrush = DesignSystem.Outline,
            BorderThickness = new Thickness(1), CornerRadius = DesignSystem.CardRadius, Child = body, ClipToBounds = true };
        Content = timerBar;
        AddHandler(PointerPressedEvent, (_, e) => interactionPointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => interactionPointer = null,
            Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        PointerCaptureLost += (_, _) => interactionPointer = null;
        PointerMoved += (_, _) => UpdateClickThrough();
        Closed += (_, _) => { closed = true; reveal.Reset(); countdown.Dispose(); };
    }

    internal static string FormatLocalTime(DateTimeOffset value) =>
        L.F("{0} {1:00}:{2:00}", value.Hour < 12 ? L.T("오전") : L.T("오후"), value.Hour % 12 == 0 ? 12 : value.Hour % 12, value.Minute);

    internal void Refresh()
    {
        if (closed) return;
        Title = L.T("Unfold · 타이머");
        RefreshTime(); controls.Refresh(runtime.Clock, runtime.Reminder.HasNotice);
        if (!pet.IsVisible || !runtime.Settings.ShowPetTimer)
        {
            reveal.Reset(); pointerOverTimer = false; timerBar.Opacity = 0; countdown.StopAnimation(); Hide(); return;
        }
        Reposition();
        if (!IsVisible) { Show(pet); MacPetWindow.Apply(this); }
        UpdateClickThrough();
    }
    private void RefreshTime()
    {
        var localTime = FormatLocalTime(DateTimeOffset.Now);
        if (currentTime.Text != localTime) currentTime.Text = localTime;
        var active = !runtime.Clock.Paused && !runtime.Clock.Stopped && !runtime.Clock.IdlePaused &&
            runtime.PresentedReminder.Notice is not (PetNotice.Invitation or PetNotice.Resting);
        countdown.AnimationsEnabled = HoverOpacity > 0;
        countdown.UpdateTime(runtime.Clock.Remaining, active && HoverOpacity > 0);
        var state = runtime.Clock.Stopped ? L.T("중지") : runtime.Clock.Paused ? L.T("일시정지") : runtime.Clock.IdlePaused ? L.T("자리 비움") : L.T("진행 중");
        AutomationProperties.SetName(timerBar, TrayReminderStatus.Create(runtime.PresentedReminder.Notice, countdown.Text, state).Status);
    }
    internal void Reposition()
    {
        if (closed || !pet.IsVisible) return;
        var area = pet.Screens.ScreenFromPoint(pet.PetAnchor)?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var direction = pet.Overlay.Direction == BubbleDirection.Left ? BubbleDirection.Right : BubbleDirection.Left;
        Position = runtime.DiagnosticMode ? new(-32000, -32000) :
            PetTimerPlacement.Calculate(pet.InteractiveScreenBounds, pet.DesktopScaling, area, direction);
    }
    internal void UpdateHover(bool petHovered, bool timerHovered, TimeSpan now)
    {
        if (closed || !IsVisible || !pet.IsVisible || !runtime.Settings.ShowPetTimer) return;
        var wasVisible = HoverOpacity > 0;
        reveal.Update(petHovered, timerHovered, now, pet.HasPointerInteraction);
        if (!reveal.IsSuppressed) suppressedPetArea = null;
        pointerOverTimer = timerHovered && reveal.Opacity > 0;
        timerBar.Opacity = reveal.Opacity;
        if (wasVisible != (HoverOpacity > 0)) RefreshTime();
    }
    internal void SuppressForPetInteraction()
    {
        var body = pet.InteractiveScreenBounds;
        suppressedPetArea = new Rect(new Point((body.X - pet.Position.X) / pet.DesktopScaling,
            (body.Y - pet.Position.Y) / pet.DesktopScaling),
            new Size(body.Width / pet.DesktopScaling, body.Height / pet.DesktopScaling));
        reveal.SuppressUntilPointerExit(); pointerOverTimer = false;
        timerBar.Opacity = 0; countdown.AnimationsEnabled = false; countdown.StopAnimation();
        UpdateClickThrough();
    }
    internal bool PointerOverPetForReveal(Point point) => reveal.IsSuppressed && suppressedPetArea is { } area
        ? area.Contains(point) : pet.PointerOverBody(point);
    internal bool AcceptsPointerAt(Point point)
    {
        if (closed || !IsVisible || HoverOpacity <= 0) return false;
        if (HasCapture) return true;
        return controls.Children.OfType<Button>().Any(button => button.IsEffectivelyVisible &&
            button.TranslatePoint(default, this) is { } origin && new Rect(origin, button.Bounds.Size).Contains(point));
    }
    internal void UpdateClickThrough()
    {
        if (closed || !IsVisible || runtime.DiagnosticMode) return;
        Point point, petPoint;
        var hasPoint = OperatingSystem.IsMacOS() ? MacPetWindow.TryGetPointer(this, out point) : WindowsPetWindow.TryGetPointer(this, out point);
        if (!hasPoint) return;
        var hasPetPoint = OperatingSystem.IsMacOS() ? MacPetWindow.TryGetPointer(pet, out petPoint) : WindowsPetWindow.TryGetPointer(pet, out petPoint);
        var keyboard = IsActive && FocusManager?.GetFocusedElement() is Control focused && focused.Classes.Contains(":focus-visible");
        var overTimer = (HoverOpacity > 0 || reveal.IsSuppressed) && new Rect(default, ClientSize).Contains(point);
        UpdateHover(hasPetPoint && PointerOverPetForReveal(petPoint), overTimer || HasCapture || keyboard, elapsed.Elapsed);
        if (elapsed.Elapsed - lastTimeRefresh >= TimeSpan.FromSeconds(1))
        { RefreshTime(); lastTimeRefresh = elapsed.Elapsed; }
        var ignore = !AcceptsPointerAt(point);
        if (OperatingSystem.IsWindows())
        { if (WindowsPetWindow.SetClickThrough(this, ignore)) IsClickThrough = ignore; }
        else if (ignore != IsClickThrough && MacPetWindow.SetClickThrough(this, ignore)) IsClickThrough = ignore;
    }
}

internal static class PetTimerPlacement
{
    internal static PixelPoint Calculate(PixelRect body, double scale, PixelRect area, BubbleDirection preferred = BubbleDirection.Right)
    {
        var width = (int)Math.Ceiling(PetTimerWindow.TimerWidth * scale);
        var height = (int)Math.Ceiling(PetTimerWindow.TimerHeight * scale);
        var gap = (int)Math.Ceiling(PetTimerWindow.PetGap * scale);
        var x = (int)Math.Round(body.X + (body.Width - width) / 2d);
        var y = body.Bottom + gap;
        if (y + height > area.Bottom)
        {
            // Keep the pet anchored at the Dock. Move only this surface if its feet
            // leave no space below, instead of covering the pet or moving it up.
            var left = body.X - gap - width; var right = body.Right + gap;
            var fitsLeft = left >= area.X; var fitsRight = right + width <= area.Right;
            if (fitsLeft || fitsRight)
            {
                x = preferred == BubbleDirection.Left ? (fitsLeft ? left : right) : (fitsRight ? right : left);
                y = (int)Math.Round(body.Y + (body.Height - height) / 2d);
            }
            else y = body.Y - gap - height;
        }
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - height));
        return new(x, y);
    }
}
