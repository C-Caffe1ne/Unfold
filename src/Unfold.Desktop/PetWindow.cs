using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PetWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly AnimationView animation = new() { Width = 192, Height = 192 };
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
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; Content = animation;
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "Settings" }; settings.Click += (_, _) => runtime.ShowSettings();
        var stretch = new MenuItem { Header = "Stretch now" }; stretch.Click += async (_, _) => await runtime.ShowReminder();
        menu.Items.Add(settings); menu.Items.Add(stretch); ContextMenu = menu;
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
            down = null; e.Pointer.Capture(null); ClampPosition(); runtime.SavePosition(Position);
            if (clicked) await React();
        };
        animation.PointerCaptureLost += (_, _) => { down = null; };
        hitTimer.Tick += (_, _) => UpdateClickThrough();
        Opened += (_, _) =>
        {
            if (runtime.DiagnosticMode) { Position = new(-32000, -32000); return; }
            var work = Screens.Primary?.WorkingArea ?? new PixelRect(0, 0, 1280, 720);
            Position = runtime.Settings.PetX is int x && runtime.Settings.PetY is int y ? new(x, y) : new(work.Right - (int)(Width * RenderScaling) - 24, work.Bottom - (int)(Height * RenderScaling) - 24);
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
            if (key is null) return;
            var current = ++generation;
            var frames = await runtime.Clip(key); if (current != generation) return;
            animation.SetFrames(frames, false, selected?.Manifest.RenderStyle == "pixel");
            var duration = frames.Sum(f => f.Duration.TotalMilliseconds);
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp(duration, 200, 10000)));
            if (current == generation) { character = null; await SetCharacter(); }
        }
        catch (Exception error) { AppPaths.Log(error); }
    }
    public void ShowPet() { Show(); hitTimer.Start(); animation.SetRunning(true); }
    public void HidePet() { Hide(); hitTimer.Stop(); animation.SetRunning(false); }
    public void ClosePet() { hitTimer.Stop(); Close(); }
    private void ClampPosition()
    {
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary; if (screen is null) return;
        var work = screen.WorkingArea; var width = (int)(Width * RenderScaling); var height = (int)(Height * RenderScaling);
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
        var ignore = !animation.OpaqueAt(animation.PointToClient(new Avalonia.PixelPoint(point.X, point.Y)));
        if (ignore == clickThrough) return;
        if (TryGetPlatformHandle()?.Handle is not nint hwnd || hwnd == 0) return;
        var style = (long)GetWindowLong(hwnd, -20);
        SetWindowLong(hwnd, -20, (nint)(ignore ? style | 0x20 : style & ~0x20)); clickThrough = ignore;
    }
}
