using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class WindowControlTests
{
    [AvaloniaFact]
    public void WindowButtonsMinimizeMaximizeRestoreAndCloseWithoutStoppingTheTimer()
    {
        using var scope = new Scope();
        var window = scope.Window;
        Assert.Equal(WindowDecorations.BorderOnly, window.WindowDecorations);
        Assert.True(window.ExtendClientAreaToDecorationsHint);
        Assert.True(window.CanResize);
        Press(window, "SettingsWindowMinimize");
        Assert.Equal(WindowState.Minimized, window.WindowState);
        window.WindowState = WindowState.Normal;
        Press(window, "SettingsWindowMaximize");
        Assert.Equal(WindowState.Maximized, window.WindowState);
        Assert.Equal(new CornerRadius(0), Find<Border>(window, "SettingsWindowSurface").CornerRadius);
        Assert.Equal("복원", AutomationProperties.GetName(Find<Button>(window, "SettingsWindowMaximize")));
        Press(window, "SettingsWindowMaximize");
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(DesignSystem.FrameRadius, Find<Border>(window, "SettingsWindowSurface").CornerRadius);
        Assert.Equal("최대화", AutomationProperties.GetName(Find<Button>(window, "SettingsWindowMaximize")));
        var paused = scope.Runtime.Clock.Paused;
        var stopped = scope.Runtime.Clock.Stopped;
        Press(window, "SettingsWindowClose");
        Assert.False(window.IsVisible);
        Assert.Equal(paused, scope.Runtime.Clock.Paused);
        Assert.Equal(stopped, scope.Runtime.Clock.Stopped);
        window.Show(); Layout(window);
        Assert.True(window.IsVisible);
        Assert.Equal(3, Find<StackPanel>(window, "SettingsWindowControls").Children.Count);
        window.CanResize = false;
        Assert.False(Find<Button>(window, "SettingsWindowMaximize").IsEnabled);
        window.CanMinimize = false;
        Assert.False(Find<Button>(window, "SettingsWindowMinimize").IsEnabled);
    }

    [AvaloniaFact]
    public void WindowButtonsAndDragRegionStayAboveTheExistingPageAtBothWindowSizes()
    {
        using var scope = new Scope();
        var window = scope.Window;
        foreach (var size in new[] { new Size(1120, 800), new Size(640, 560) })
        {
            window.Width = size.Width; window.Height = size.Height; Layout(window);
            var region = Find<Border>(window, "SettingsWindowDragRegion");
            var point = region.TranslatePoint(new Point(200, 12), window)!.Value;
            Assert.Same(region, window.InputHitTest(point));
            var nav = Find<Button>(window, "SettingsNavTimer");
            Assert.True(nav.TranslatePoint(default, window)!.Value.Y > point.Y);
            foreach (var name in new[] { "SettingsWindowClose", "SettingsWindowMinimize", "SettingsWindowMaximize" })
            {
                var button = Find<Button>(window, name);
                var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
                var hit = Assert.IsAssignableFrom<Control>(window.InputHitTest(center));
                Assert.True(ReferenceEquals(hit, button) || hit.GetVisualAncestors().Contains(button));
            }
        }
    }

    [AvaloniaFact]
    public void RoundedOuterSurfaceReplacesTheInnerFrameAndKeepsContentBounds()
    {
        using var scope = new Scope();
        var window = scope.Window;
        var surface = Find<Border>(window, "SettingsWindowSurface");
        var frame = Find<Border>(window, "SettingsFrame");
        Assert.True(surface.ClipToBounds);
        Assert.Equal(DesignSystem.FrameRadius, surface.CornerRadius);
        Assert.Equal(new Thickness(0), frame.BorderThickness);
        Assert.Null(frame.BorderBrush);
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680), new Size(640, 560) })
        {
            window.Width = size.Width; window.Height = size.Height; Layout(window);
            Assert.Equal(window.ClientSize, surface.Bounds.Size);
            var card = Find<Border>(window, "SettingsTimerCard");
            Assert.Equal(31, card.TranslatePoint(default, window)!.Value.Y);
            Assert.Equal(216, card.Bounds.Height);
        }
    }

    [AvaloniaFact]
    public void MacHeaderDragMovesTheWindowAndStopsOnReleaseAndCaptureLoss()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS uses managed dragging.");
        using var scope = new Scope();
        var window = scope.Window;
        var region = Find<Border>(window, "SettingsWindowDragRegion");
        var point = region.TranslatePoint(new Point(220, 12), window)!.Value;
        window.Position = new PixelPoint(100, 200);
        var initial = window.Position;
        IPointer? pointer = null;
        region.AddHandler(InputElement.PointerPressedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, true);
        window.MouseDown(point, MouseButton.Left);
        window.MouseMove(point + new Vector(80, 40)); Layout(window);
        Assert.Equal(new PixelPoint(initial.X + (int)Math.Round(80 * window.DesktopScaling),
            initial.Y + (int)Math.Round(40 * window.DesktopScaling)), window.Position);
        var moved = window.Position;
        window.MouseUp(point, MouseButton.Left);
        window.MouseMove(point + new Vector(30, 20)); Layout(window);
        Assert.Equal(moved, window.Position);
        point += new Vector(140, 0);
        window.MouseDown(point, MouseButton.Left);
        Assert.NotNull(pointer); pointer.Capture(null);
        window.MouseMove(point + new Vector(30, 20), RawInputModifiers.LeftMouseButton); Layout(window);
        Assert.Equal(moved, window.Position);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Right);
        window.MouseMove(point + new Vector(30, 20), RawInputModifiers.RightMouseButton); Layout(window);
        Assert.Equal(moved, window.Position);
        window.MouseUp(point, MouseButton.Right);
    }

    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(item => item.Name == name);
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window); }
    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        // Hit testing uses the rendered scene, which can lag a headless layout pass.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime) { ConfirmActionOverride = (_, _, _) => Task.FromResult(0) };
            Window = new(Runtime); Window.Show(); Layout(Window);
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
