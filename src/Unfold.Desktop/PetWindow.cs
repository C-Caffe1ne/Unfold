using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using PixelPoint = Avalonia.PixelPoint;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class PetWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly Canvas canvas = new();
    private readonly PetSpeechBubble bubble;
    private readonly Avalonia.Controls.Shapes.Polygon tail = new()
    {
        Name = "PetSpeechTail",
        Fill = DesignSystem.Shell,
        StrokeThickness = 0,
        IsHitTestVisible = false
    };
    private readonly Avalonia.Controls.Shapes.Polyline tailOutline = new()
    {
        Name = "PetSpeechTailOutline",
        Stroke = DesignSystem.Outline,
        StrokeThickness = 1,
        IsHitTestVisible = false
    };
    private PetBubbleLayout layout = PetBubbleLayout.Create(BubbleDirection.Top, false);
    private double layoutScale = 1;
    public PixelPoint PetAnchor => new(Position.X + (int)Math.Round(layout.Pet.X * DesktopScaling), Position.Y + (int)Math.Round(layout.Pet.Y * DesktopScaling));
    private readonly AnimationView animation = new() { Width = DesignSystem.PetBaseSize, Height = DesignSystem.PetBaseSize };
    // Content is the layout canvas that also carries the bubble and tail, and its shape
    // changes with the bubble layout. Diagnostics and tests read playback state and render
    // the pet through this contract instead of casting Content or walking the visual tree.
    internal AnimationView PetView => animation;
    private readonly DispatcherTimer hitTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private Avalonia.PixelPoint? down;
    private IPointer? pressedPointer;
    private Avalonia.PixelPoint origin;
    private bool dragging, clickThrough;
    private bool hoveringPet;
    private bool ShowingHover => hoveringPet && IsVisible && ContextMenu?.IsOpen != true && !runtime.PresentedReminder.HasNotice;
    private long pressedAt;
    private int generation;
    private CancellationTokenSource? reactionCancellation;
    private CharacterPackage? character;
    public PetWindow(AppRuntime runtime)
    {
        this.runtime = runtime;
        Width = Height = 192; CanResize = false; WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent; TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        bubble = new(runtime.StartBreak, runtime.SnoozeBreak, runtime.CompleteBreak);
        canvas.Children.Add(animation); canvas.Children.Add(bubble); canvas.Children.Add(tail); canvas.Children.Add(tailOutline); Content = canvas;
        animation.Completed += PointerClipCompleted;
        bubble.IsVisible = tail.IsVisible = tailOutline.IsVisible = false;
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "설정" }; settings.Click += (_, _) => runtime.ShowSettings();
        var hide = new MenuItem { Header = "펫 숨기기", Name = "HidePet" };
        hide.Click += async (_, _) =>
        {
            try { await runtime.HidePet(); }
            catch (Exception error) { await Ui.Error(this, error); }
        };
        menu.Items.Add(settings); menu.Items.Add(hide); ContextMenu = menu;
        menu.Opened += (_, _) => RefreshSpeech();
        menu.Closed += (_, _) => RefreshSpeech();
        // Squash/bounce changes the animation's hit-test transform. Track hover in
        // the stationary window coordinates so a pose cannot resize the window.
        PointerEntered += (_, e) => UpdateHover(PetPoint(e.GetPosition(this)));
        PointerMoved += (_, e) => UpdateHover(PetPoint(e.GetPosition(this)));
        PointerExited += (_, _) => UpdateHover(null);
        animation.PointerPressed += (_, e) =>
        {
            if (down is not null || !e.GetCurrentPoint(animation).Properties.IsLeftButtonPressed || !animation.OpaqueAt(e.GetPosition(animation))) return;
            down = animation.PointToScreen(e.GetPosition(animation));
            origin = Position; pressedAt = Stopwatch.GetTimestamp(); dragging = false;
            BeginCompanionPress();
            pressedPointer = e.Pointer; e.Pointer.Capture(animation); e.Handled = true;
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
            if (down is null || e.InitialPressMouseButton != MouseButton.Left) return;
            var clicked = !dragging && (HasOriginalBehavior || Stopwatch.GetElapsedTime(pressedAt).TotalSeconds <= 0.22);
            down = null; pressedPointer = null; e.Pointer.Capture(null); ClampPosition(); runtime.SavePosition(PetAnchor);
            var deferred = ReleaseCompanionPress(clicked);
            UpdateHover(PetPoint(e.GetPosition(this))); RefreshSpeech();
            if (clicked && !deferred) await React();
        };
        animation.PointerCaptureLost += (_, _) =>
        {
            if (down is null) return;
            down = null; pressedPointer = null; dragging = false;
            CancelCompanionPress(); UpdateHover(null);
        };
        hitTimer.Tick += (_, _) => { UpdateClickThrough(); TickCompanion(); };
        Opened += (_, _) =>
        {
            if (runtime.DiagnosticMode) { Position = new(-32000, -32000); return; }
            var work = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            Position = runtime.Settings.PetX is int x && runtime.Settings.PetY is int y ? new(x, y) : new(work.Right - (int)(Width * DesktopScaling) - 24, work.Bottom - (int)(Height * DesktopScaling) - 24);
            ClampPosition(); hitTimer.Start();
        };
        Closed += (_, _) => { hoveringPet = false; InvalidatePlayback(); ResetCompanion(); hitTimer.Stop(); animation.Dispose(); };
    }
    public async Task SetCharacter()
    {
        var selected = runtime.Selected;
        if (character == selected) return;
        var current = InvalidatePlayback(); ResetCompanion();
        pointerClips = null;
        IReadOnlyList<AnimationFrame> frames;
        try { frames = await runtime.Clip("idle"); }
        catch (Exception error) when (current != generation &&
            error is IOException or UnauthorizedAccessException or InvalidDataException)
        { return; } // A superseded load must not replace a newer pet with its fallback.
        var loadedPointerClips = await LoadPointerArt(selected);
        if (current != generation) return;
        pointerClips = loadedPointerClips;
        character = selected; ActiveAnimation = "idle";
        if (selected?.HasOriginalBehavior == true && runtime.Reminder.Notice == PetNotice.Resting)
            originalStretchSession = runtime.Reminder.Session;
        animation.SetFrames(frames, true, selected?.Manifest.RenderStyle == "pixel", selected?.HasOriginalBehavior == true);
    }
    internal void ShowReminderFallback(CharacterPackage selected)
    {
        InvalidatePlayback(); ResetCompanion(); character = null; ActiveAnimation = "idle";
        var sprite = selected.Manifest.SpriteSheet;
        var sheet = selected.Sheet;
        var pixels = new uint[sprite.FrameWidth * sprite.FrameHeight];
        for (var row = 0; row < sprite.FrameHeight; row++)
            Array.Copy(sheet.Pixels, row * sheet.Width, pixels, row * sprite.FrameWidth, sprite.FrameWidth);
        animation.SetFrames([new(new(sprite.FrameWidth, sprite.FrameHeight, pixels), TimeSpan.FromSeconds(1))],
            true, selected.Manifest.RenderStyle == "pixel", selected.HasOriginalBehavior);
        // Leave character unset so a later notice/focus request can retry the repaired GIF.
    }
    internal async Task React(string? preferred = null)
    {
        var current = generation;
        string? key = null;
        try
        {
            var selected = runtime.Selected;
            if (!IsVisible) return;
            if (DeferPointerReaction(preferred)) return;
            // Stretch belongs to reminders. A plain click reacts only when the character
            // ships a click clip, and otherwise leaves the idle loop alone — so the early
            // return has to happen before generation moves, or it would cancel a stretch.
            key = preferred ?? (selected?.Manifest.Animations.ContainsKey("click") == true ? "click" : null);
            if (key is null || selected?.Manifest.Animations.ContainsKey(key) != true) return;
            current = InvalidatePlayback();
            var cancellation = reactionCancellation = new CancellationTokenSource();
            var session = runtime.Reminder.Session;
            if (selected.HasOriginalBehavior && key == "stretch" && runtime.Reminder.Notice == PetNotice.Resting)
                originalStretchSession = session;
            ActiveAnimation = key; reacting = true; idleSchedule.Reset();
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Finished() => completed.TrySetResult();
            animation.Completed += Finished;
            try
            {
                var frames = await runtime.Clip(key); if (current != generation) return;
                animation.SetRunning(true);
                animation.SetFrames(frames, false, selected.Manifest.RenderStyle == "pixel", selected.HasOriginalBehavior);
                await completed.Task.WaitAsync(cancellation.Token);
                if (current != generation) return;
                if (selected.HasOriginalBehavior && key == "stretch" && session is not null &&
                    runtime.Reminder.Session == session && runtime.Reminder.Notice == PetNotice.Resting)
                { originalStretchSession = null; walkingSession = session; }
                await RestoreBaseAnimation(current);
            }
            finally
            {
                animation.Completed -= Finished;
                if (reactionCancellation == cancellation) reactionCancellation = null;
                cancellation.Dispose();
            }
        }
        catch (OperationCanceledException) when (current != generation || !IsVisible) { }
        catch (Exception error)
        {
            AppPaths.Log(error);
            if (current != generation || !IsVisible) return;
            reacting = false;
            // Do not recursively replay a stretch that just failed to load.
            if (key == "stretch") originalStretchSession = null;
            await RestoreBaseAnimation(current);
        }
    }
    private int InvalidatePlayback()
    {
        generation++; reactionCancellation?.Cancel(); reactionCancellation = null; reacting = false;
        return generation;
    }
    public void ShowPet()
    {
        MacPetWindow.Apply(this);
        Show(); RefreshSpeech(); lastCompanionTick = Stopwatch.GetTimestamp(); hitTimer.Start(); animation.SetRunning(true);
        if (originalStretchSession is not null && !reacting && !pressed) _ = React("stretch");
    }
    public void HidePet() { hoveringPet = false; InvalidatePlayback(); ResetCompanion(); character = null; Hide(); hitTimer.Stop(); animation.SetRunning(false); RefreshSpeech(); }
    public void ClosePet() { InvalidatePlayback(); ResetCompanion(); hitTimer.Stop(); Close(); }
    internal void FocusReminder() { Activate(); bubble.FocusAction(); }
    public void RefreshSpeech()
    {
        var reminder = runtime.PresentedReminder;
        RefreshCompanionContext();
        var hover = ShowingHover;
        var keepHoverLayout = hover && down is not null && bubble.IsVisible &&
            bubble.Width == DesignSystem.SpeechHoverWidth && layoutScale == DesktopScaling;
        if (hover) bubble.RefreshHover(DateTime.Now, runtime.Clock);
        else bubble.Refresh(reminder, runtime.Settings.SnoozeMinutes);
        // Clock ticks may update text while dragging, but must not flip the
        // bubble's edge placement underneath a captured pointer.
        if (keepHoverLayout) return;
        var petSize = DesignSystem.PetBaseSize * runtime.Settings.PetScalePercent / 100d;
        animation.Width = animation.Height = petSize;
        var expanded = reminder.HasNotice || hover;
        var anchor = PetAnchor;
        var work = Screens.ScreenFromPoint(anchor)?.WorkingArea ?? Screens.Primary?.WorkingArea;
        var next = hover && work is { } hoverArea && !runtime.DiagnosticMode
            ? PetBubbleLayout.CreateHover(runtime.Settings.BubbleDirection, anchor, DesktopScaling, hoverArea, petSize)
            : PetBubbleLayout.Create(runtime.Settings.BubbleDirection, expanded, bubble.Height, petSize, bubble.Width);
        if (layout.Size == next.Size && layout.Pet == next.Pet && layout.Bubble == next.Bubble &&
            bubble.IsVisible == expanded && layoutScale == DesktopScaling) return;
        layoutScale = DesktopScaling; layout = next; Width = layout.Size.Width; Height = layout.Size.Height;
        // Diagnostic minimums follow the live canvas instead of preventing a collapse.
        if (runtime.DiagnosticMode) { MinWidth = Width; MinHeight = Height; }
        canvas.Width = Width; canvas.Height = Height;
        Canvas.SetLeft(animation, layout.Pet.X); Canvas.SetTop(animation, layout.Pet.Y);
        Canvas.SetLeft(bubble, layout.Bubble.X); Canvas.SetTop(bubble, layout.Bubble.Y);
        tail.Points = new Avalonia.Collections.AvaloniaList<Point>(layout.Tail);
        tailOutline.Points = expanded
            ? new Avalonia.Collections.AvaloniaList<Point>([layout.Tail[0], layout.Tail[2], layout.Tail[1]])
            : new Avalonia.Collections.AvaloniaList<Point>();
        bubble.IsVisible = tail.IsVisible = tailOutline.IsVisible = expanded;
        Position = runtime.DiagnosticMode ? new(-32000, -32000) : work is { } area
            ? layout.Position(anchor, DesktopScaling, area) : anchor;
    }

    private Point PetPoint(Point windowPoint) => windowPoint - new Vector(layout.Pet.X, layout.Pet.Y);

    private void UpdateHover(Point? point)
    {
        if (down is not null) return;
        // Enter on painted pixels, then retain hover over the stable pet area.
        // Frame changes and click poses must not close/reopen the native surface.
        var hovered = IsVisible && point is { } local && new Rect(animation.Bounds.Size).Contains(local) &&
            (hoveringPet || animation.OpaqueAt(local));
        if (hoveringPet == hovered) return;
        hoveringPet = hovered;
        // Finish the current pointer dispatch before changing its window coordinates.
        Dispatcher.UIThread.Post(() => { if (IsVisible) RefreshSpeech(); }, DispatcherPriority.Background);
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
        // A click-through window may not receive Enter until the pointer moves again.
        UpdateHover(PetPoint(this.PointToClient(cursor)));
        var onBubble = bubble.IsVisible && bubble.IsHitTestVisible && new Rect(bubble.Bounds.Size).Contains(bubble.PointToClient(cursor));
        var ignore = !onBubble && !animation.OpaqueAt(animation.PointToClient(cursor));
        if (ignore == clickThrough) return;
        if (TryGetPlatformHandle()?.Handle is not nint hwnd || hwnd == 0) return;
        var style = (long)GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, (nint)(ignore ? style | 0x20 : style & ~0x20)); clickThrough = ignore;
    }
}
