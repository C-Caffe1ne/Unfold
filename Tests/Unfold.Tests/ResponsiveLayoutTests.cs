using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class ResponsiveLayoutTests
{
    [AvaloniaFact]
    public void CompactDashboardScrollsToEveryCardAndPreservesEditedControls()
    {
        using var scope = new Scope(); var window = scope.Window;
        Assert.Equal(640, window.MinWidth); Assert.Equal(560, window.MinHeight);
        var rest = Find<NumericUpDown>(window, "BreakDurationMinutes"); rest.Value = 4;
        var timer = Find<Control>(window, "SettingsTimerCard");
        var companion = Find<Control>(window, "SettingsCompanionCard");
        var timing = Find<Control>(window, "SettingsHomeTimingCard");
        foreach (var width in new[] { 640, 760, 859, 860, 1120, 640 })
        {
            window.Width = width; window.Height = width < 860 ? 560 : 800; Layout(window);
            var scroll = Find<ScrollViewer>(window, "SettingsDashboardScroll"); scroll.ScrollToHome(); Layout(window);
            Assert.Same(rest, Find<NumericUpDown>(window, "BreakDurationMinutes")); Assert.Equal(4, rest.Value);
            AssertNoHorizontalOverflow(window);
            var timerPoint = timer.TranslatePoint(default, window)!.Value;
            var companionPoint = companion.TranslatePoint(default, window)!.Value;
            var timingPoint = timing.TranslatePoint(default, window)!.Value;
            if (width < 860)
            {
                Assert.Equal(timerPoint.X, timingPoint.X);
                Assert.True(timingPoint.Y >= companionPoint.Y + companion.Bounds.Height);
                Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
                var preview = Find<ScrollViewer>(window, "CompanionPreviewScroll");
                Assert.True(preview.Extent.Height <= preview.Viewport.Height + 1);
                rest.BringIntoView(); Layout(window); AssertInWindow(rest, window);
                scroll.ScrollToEnd(); Layout(window); AssertInWindow(Find<Button>(window, "SettingsOpenReview"), window);
            }
            else
            {
                Assert.Equal(timerPoint.Y, timingPoint.Y);
                Assert.True(timingPoint.X > timerPoint.X + timer.Bounds.Width);
            }
            AssertInWindow(Find<Button>(window, "SettingsQuit"), window);
        }
        window.Width = 1120; window.Height = 800; Layout(window);
        var editor = rest.GetVisualDescendants().OfType<TextBox>().First(); editor.Focus(); Layout(window);
        Assert.True(editor.IsFocused);
        window.Width = 640; window.Height = 560; Layout(window);
        Assert.True(editor.IsFocused); AssertInWindow(rest, window);
    }

    [AvaloniaFact]
    public void AllTabsFitSmallWindowsAndResizingPreservesPreferencesAndPetDrafts()
    {
        using var scope = new Scope(); var window = scope.Window;
        var links = Find<StackPanel>(window, "SettingsNavigationLinks");
        Assert.Equal(new[] { "SettingsNavTimer", "SettingsNavPacks", "SettingsNavReview", "SettingsNavSettings" },
            links.Children.OfType<Button>().Select(button => button.Name).ToArray());
        Assert.Equal(2, Find<Grid>(window, "SettingsNavigationRail").Children.Count);
        Press(window, "SettingsNavSettings");
        var volume = Find<Slider>(window, "ReminderVolumePercent"); volume.Value = 37;
        foreach (var width in new[] { 640, 760, 860, 1120, 640 })
        {
            window.Width = width; window.Height = 560; Layout(window);
            Assert.Same(volume, Find<Slider>(window, "ReminderVolumePercent")); Assert.Equal(37, volume.Value);
            Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
            AssertNoHorizontalOverflow(window);
            AssertInWindow(Find<Button>(window, "SavePreferences"), window);
            var scroll = Find<ScrollViewer>(window, "SettingsPreferencesScroll");
            scroll.ScrollToEnd(); Layout(window); AssertInWindow(Find<CheckBox>(window, "DebugToolsEnabled"), window);
        }
        Press(window, "SettingsNavReview"); AssertNoHorizontalOverflow(window);
        AssertInWindow(Find<Button>(window, "ReviewExport"), window);
        Press(window, "SettingsNavPacks"); AssertNoHorizontalOverflow(window);
        AssertInWindow(Find<Button>(window, "InstallPetPack"), window);
        Find<TabControl>(window, "PetManagementTabs").SelectedIndex = 1; Layout(window);
        var name = Find<TextBox>(window, "CustomPetName"); name.Text = "작성 중인 펫";
        foreach (var width in new[] { 640, 860, 1120, 640 })
        {
            window.Width = width; Layout(window); AssertNoHorizontalOverflow(window);
            Assert.Same(name, Find<TextBox>(window, "CustomPetName")); Assert.Equal("작성 중인 펫", name.Text);
            var scroll = Find<ScrollViewer>(window, "PageBodyScroll"); scroll.ScrollToEnd(); Layout(window);
            AssertInWindow(Find<Button>(window, "CustomPetFile_click"), window);
        }
    }

    private static void AssertNoHorizontalOverflow(Window window)
    {
        foreach (var scroll in window.GetVisualDescendants().OfType<ScrollViewer>().Where(item => item.IsEffectivelyVisible && item.Bounds.Width > 0))
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"{scroll.Name}: {scroll.Extent.Width} > {scroll.Viewport.Width}");
    }
    private static void AssertInWindow(Control control, Window window)
    {
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.True(point.X >= 0 && point.X + control.Bounds.Width <= window.ClientSize.Width + 1, control.Name);
        Assert.True(point.Y >= 0 && point.Y + control.Bounds.Height <= window.ClientSize.Height + 1, control.Name);
    }
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window); }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private readonly AppRuntime runtime;
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            runtime = new(lifetime); Window = new(runtime); Window.Show(); Layout(Window);
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
