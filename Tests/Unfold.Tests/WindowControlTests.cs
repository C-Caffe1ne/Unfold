using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class WindowControlTests
{
    [AvaloniaFact]
    public void MainWindowUsesNativeDecorationsWithoutDuplicateCaptionControls()
    {
        using var scope = new Scope();
        var window = scope.Window;
        Assert.Equal(WindowDecorations.Full, window.WindowDecorations);
        Assert.Equal(OperatingSystem.IsMacOS(), window.ExtendClientAreaToDecorationsHint);
        Assert.Equal(-1, window.ExtendClientAreaTitleBarHeightHint);
        Assert.True(window.CanResize && window.CanMinimize && window.CanMaximize);
        Assert.DoesNotContain(WindowTransparencyLevel.Transparent, window.TransparencyLevelHint);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name is
            "SettingsWindowControls" or "SettingsWindowControlBar" or "SettingsWindowDragRegion" or
            "SettingsWindowClose" or "SettingsWindowMinimize" or "SettingsWindowMaximize");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloseRequestHidesAndReopensTheSameContentWithoutChangingTheTimer(bool paused)
    {
        using var scope = new Scope();
        var window = scope.Window;
        if (scope.Runtime.Clock.Paused != paused) scope.Runtime.TogglePause();
        var stopped = scope.Runtime.Clock.Stopped;
        var content = window.Content;
        var closed = false;
        window.Closed += (_, _) => closed = true;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            window.Close(); Layout(window);
            Assert.False(window.IsVisible);
            Assert.False(closed);
            Assert.Equal(paused, scope.Runtime.Clock.Paused);
            Assert.Equal(stopped, scope.Runtime.Clock.Stopped);
            window.Show(); Layout(window);
            Assert.True(window.IsVisible);
            Assert.Same(content, window.Content);
            var preferences = Find<Button>(window, "SettingsNavSettings");
            preferences.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window);
            Assert.True(Find<ScrollViewer>(window, "SettingsPreferencesScroll").IsEffectivelyVisible);
        }
    }

    [AvaloniaTheory]
    [InlineData(1120, 800)]
    [InlineData(860, 680)]
    [InlineData(640, 560)]
    public void NativeFrameFillsClientAreaAndKeepsContentBelowDecorationsAcrossStates(int width, int height)
    {
        using var scope = new Scope();
        var window = scope.Window;
        window.Width = width; window.Height = height;
        foreach (var state in new[] { WindowState.Normal, WindowState.Minimized, WindowState.Normal,
            WindowState.Maximized, WindowState.Normal, WindowState.FullScreen, WindowState.Normal })
        {
            // Headless state requests verify app layout/lifecycle, not native pointer input.
            window.WindowState = state; Layout(window);
            Assert.Equal(state, window.WindowState);
            if (state == WindowState.Minimized) continue;
            var surface = Find<Border>(window, "SettingsWindowSurface");
            var frame = Find<Border>(window, "SettingsFrame");
            var inset = window.IsExtendedIntoWindowDecorations ? window.WindowDecorationMargin : default;
            Assert.Equal(window.ClientSize, surface.Bounds.Size);
            Assert.Equal(default, surface.CornerRadius);
            Assert.Equal(default, frame.BorderThickness);
            Assert.Equal(inset, frame.Margin);
            var titleBar = Find<Border>(window, "SettingsNativeTitleBar");
            Assert.Equal(WindowDecorationsElementRole.TitleBar, WindowDecorationProperties.GetElementRole(titleBar));
            Assert.Equal(inset.Top, titleBar.Height);
            Assert.Equal(inset.Top > 0, titleBar.IsVisible);
            var card = Find<Border>(window, "SettingsTimerCard");
            Assert.True(card.TranslatePoint(default, window)!.Value.Y >= inset.Top + 8);
            Assert.Equal(216, card.Bounds.Height);
            foreach (var name in new[] { "SettingsNavTimer", "SettingsNavSettings", "SettingsTheme", "SettingsQuit" })
            {
                var button = Find<Button>(window, name);
                var origin = button.TranslatePoint(default, window)!.Value;
                Assert.InRange(origin.X, inset.Left, window.ClientSize.Width - inset.Right - button.Bounds.Width);
                Assert.InRange(origin.Y, inset.Top, window.ClientSize.Height - inset.Bottom - button.Bounds.Height);
            }
        }
    }

    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(item => item.Name == name);
    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
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
