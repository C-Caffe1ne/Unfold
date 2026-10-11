using System.Diagnostics;
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
    private readonly Canvas canvas = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    internal PetOverlayWindow Overlay { get; }
    public PixelPoint PetAnchor => Position;
    private readonly AnimationView animation = new() { Width = DesignSystem.PetBaseSize, Height = DesignSystem.PetBaseSize };
    // This surface contains only the pet. Overlay changes must never arrange or
    // resize its native backing surface, even for a single intermediate frame.
    internal AnimationView PetView => animation;
    private readonly DispatcherTimer hitTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Avalonia.PixelPoint? down;
    private IPointer? pressedPointer;
    private Avalonia.PixelPoint dragAnchor;
    private bool dragging, clickThrough;
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
        canvas.Children.Add(animation); Content = canvas;
        Overlay = new(this, runtime);
        PositionChanged += (_, _) => Overlay.Reposition();
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) Overlay.Refresh(); };
        animation.Completed += PointerClipCompleted;
        animation.PlaybackFailed += _ => { InvalidatePlayback(); ResetCompanion(); if (runtime.Selected is { } selected) ShowReminderFallback(selected); };
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "설정" }; settings.Click += (_, _) => runtime.ShowSettings();
        var hide = new MenuItem { Header = "펫 숨기기", Name = "HidePet" };
        hide.Click += async (_, _) =>
        {
            try { await runtime.HidePet(); }
            catch (Exception error) { await Ui.Error(this, error); }
        };
        menu.Items.Add(settings); menu.Items.Add(hide); ContextMenu = menu;
        menu.Opened += (_, _) => { RefreshSpeech(); UpdateClickThrough(); };
        menu.Closed += (_, _) => { RefreshSpeech(); UpdateClickThrough(); };
        PointerMoved += (_, _) => UpdateClickThrough();
        AddHandler(PointerPressedEvent, (_, e) => interactionPointer = e.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => interactionPointer = null, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        animation.PointerPressed += (_, e) =>
        {
            if (down is not null || !e.GetCurrentPoint(animation).Properties.IsLeftButtonPressed || !animation.OpaqueAt(e.GetPosition(animation), includeEdgeTolerance: false)) return;
            down = PointerScreenPosition(e);
            dragAnchor = PetAnchor; pressedAt = Stopwatch.GetTimestamp(); dragging = false;
            BeginCompanionPress();
            if (!HasOriginalBehavior) _ = React("pointerDown");
            pressedPointer = e.Pointer; e.Pointer.Capture(animation); e.Handled = true;
        };
        animation.PointerMoved += (_, e) =>
        {
            if (down is not { } start) return;
            var current = PointerScreenPosition(e); var delta = current - start;
            if (Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y) >= 5) dragging = true;
            if (dragging)
            {
                StartPointerHold();
                Position = new(dragAnchor.X + delta.X, dragAnchor.Y + delta.Y);
            }
        };
        animation.PointerReleased += async (_, e) =>
        {
            if (down is null || e.InitialPressMouseButton != MouseButton.Left) return;
            var clicked = !dragging && Stopwatch.GetElapsedTime(pressedAt).TotalSeconds < PetPose.LiftDelay;
            down = null; pressedPointer = null; e.Pointer.Capture(null); ClampPosition(); runtime.SavePosition(PetAnchor);
            var handled = ReleaseCompanionPress(clicked);
            RefreshSpeech();
            if (!HasOriginalBehavior && runtime.Selected?.Manifest.Animations.ContainsKey("pointerUp") == true) await React("pointerUp");
            else
            {
                if (!HasOriginalBehavior && ActiveAnimation == "pointerDown") await RestoreBaseAnimation(InvalidatePlayback());
                if (clicked && !handled) await React();
            }
        };
        animation.PointerCaptureLost += (_, _) =>
        {
            if (down is null) return;
            down = null; pressedPointer = null; dragging = false;
            CancelCompanionPress();
            if (!HasOriginalBehavior && ActiveAnimation == "pointerDown") _ = RestoreBaseAnimation(InvalidatePlayback());
        };
        hitTimer.Tick += (_, _) => { UpdateClickThrough(); TickCompanion(); Overlay.UpdateClickThrough(); };
        Opened += (_, _) =>
        {
            if (runtime.DiagnosticMode) { Position = new(-32000, -32000); return; }
            var work = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            Position = runtime.Settings.PetX is int x && runtime.Settings.PetY is int y ? new(x, y) : new(work.Right - (int)(Width * DesktopScaling) - 24, work.Bottom - (int)(Height * DesktopScaling) - 24);
            ClampPosition(); hitTimer.Start();
        };
        Closed += (_, _) => { Overlay.Close(); InvalidatePlayback(); ResetCompanion(); hitTimer.Stop(); animation.Dispose(); };
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
        animation.SetFrames(frames, selected?.Manifest.Animations["idle"].Loop ?? true, selected?.Manifest.RenderStyle == "pixel", selected?.HasOriginalBehavior == true, selected?.Manifest.Animations["idle"].PingPong == true);
    }
    internal void ShowReminderFallback(CharacterPackage selected)
    {
        InvalidatePlayback(); ResetCompanion(); character = null; ActiveAnimation = "idle";
        animation.SetFrames([new(selected.StillImage, TimeSpan.FromSeconds(1))],
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
            if (!IsVisible || preferred == "hover") return; // Older packs may still contain this retired event.
            if (DeferPointerReaction(preferred)) return;
            // Stretch belongs to reminders. A plain click reacts only when the character
            // ships a click clip, and otherwise leaves the idle loop alone — so the early
            // return has to happen before generation moves, or it would cancel a stretch.
            key = preferred ?? (selected?.Manifest.Animations.ContainsKey("click") == true ? "click" : null);
            if (key == "stretch" && selected?.IsGlb == true && !selected.Manifest.Animations.ContainsKey("stretch") &&
                selected.Manifest.Animations.ContainsKey("walk") && runtime.Reminder.Notice == PetNotice.Resting)
            {
                walkingSession = runtime.Reminder.Session;
                await RestoreBaseAnimation(InvalidatePlayback()); return;
            }
            if (key is null || selected?.Manifest.Animations.ContainsKey(key) != true) return;
            current = InvalidatePlayback();
            var cancellation = reactionCancellation = new CancellationTokenSource();
            var session = runtime.Reminder.Session;
            if (selected.HasOriginalBehavior && key == "stretch" && runtime.Reminder.Notice == PetNotice.Resting)
                originalStretchSession = session;
            ActiveAnimation = key; reacting = true; idleSchedule.Reset();
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Finished() => completed.TrySetResult();
            try
            {
                var frames = await runtime.Clip(key); if (current != generation) return;
                animation.SetRunning(true);
                var definition = selected.Manifest.Animations[key];
                // Only this reaction's frames may complete its wait; the old clip
                // can finish while the new clip is still decoding.
                animation.Completed += Finished;
                animation.SetFrames(frames, (selected.IsGlb || !selected.HasOriginalBehavior) && definition.Loop, selected.Manifest.RenderStyle == "pixel", selected.HasOriginalBehavior, definition.PingPong);
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
        Show(); RefreshSpeech(); UpdateClickThrough(); lastCompanionTick = Stopwatch.GetTimestamp(); hitTimer.Start(); animation.SetRunning(true);
        if (originalStretchSession is not null && !reacting && !pressed) _ = React("stretch");
    }
    public void HidePet() { InvalidatePlayback(); ResetCompanion(); character = null; Hide(); hitTimer.Stop(); animation.SetRunning(false); RefreshSpeech(); }
    public void ClosePet() { InvalidatePlayback(); ResetCompanion(); hitTimer.Stop(); Close(); }
    internal void FocusReminder() => Overlay.FocusReminder();
    public void RefreshSpeech()
    {
        RefreshCompanionContext();
        var petSize = DesignSystem.PetBaseSize * runtime.Settings.PetScalePercent / 100d;
        if (Width != petSize || Height != petSize)
        {
            if (runtime.DiagnosticMode) MinWidth = MinHeight = petSize;
            Width = Height = canvas.Width = canvas.Height = animation.Width = animation.Height = petSize;
        }
        Overlay.Refresh();
    }

    private PixelPoint PointerScreenPosition(PointerEventArgs e)
    {
        // The undecorated window origin is stable across sprite transforms. Keep
        // drag distances in desktop pixels even if a queued bubble layout runs.
        var point = e.GetPosition(this);
        return new(Position.X + (int)Math.Round(point.X * DesktopScaling),
            Position.Y + (int)Math.Round(point.Y * DesktopScaling));
    }

    internal static PixelRect PlacementArea(PixelRect bounds, PixelRect workingArea, bool macOS)
    {
        if (!macOS) return workingArea;
        // A user-positioned pet may overlap the Dock. Keep the menu bar excluded,
        // but do not treat its transparent speech window as part of the pet.
        var top = Math.Clamp(workingArea.Y, bounds.Y, bounds.Bottom);
        return new(bounds.X, top, bounds.Width, bounds.Bottom - top);
    }

    private void ClampPosition()
    {
        var screen = (OperatingSystem.IsMacOS() ? Screens.ScreenFromPoint(PetAnchor) : null)
            ?? Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null) return;
        // Clamp only the pet. The independent overlay fits itself to the work area.
        var area = PlacementArea(screen.Bounds, screen.WorkingArea, OperatingSystem.IsMacOS());
        var pet = PetBubbleLayout.Create(runtime.Settings.BubbleDirection, false, petSize: animation.Width);
        var anchor = PetAnchor;
        Position += pet.Position(anchor, DesktopScaling, area) - anchor;
    }
}
