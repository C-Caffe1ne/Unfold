using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class NotificationLayoutTests
{
    [AvaloniaTheory]
    [InlineData(760, false)]
    [InlineData(468, true)]
    [InlineData(340, true)]
    public void NotificationEditorsAlignRightAndCompactVolumeFitsNarrowCards(double cardWidth, bool stacked)
    {
        using var scope = new Scope(); var window = scope.Window;
        var card = Find<Border>(window, "SettingsNotificationCard"); card.Width = cardWidth; Layout(window);
        var direction = Find<ComboBox>(window, "BubbleDirection");
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        var volumeControls = Find<Grid>(window, "ReminderVolumeControls");
        AssertRightAligned(Find<Grid>(window, "BubbleDirectionRow"), direction);
        AssertRightAligned(Find<Grid>(window, "ReminderVolumeRow"), volumeControls);
        Assert.Equal(stacked ? 2 : 0, Grid.GetRow(direction));
        Assert.Equal(stacked ? 2 : 0, Grid.GetRow(volumeControls));
        Assert.InRange(volume.Bounds.Width, 200, 240);
        var value = Find<TextBlock>(window, "ReminderVolumeValue");
        var sliderOrigin = volume.TranslatePoint(default, volumeControls)!.Value;
        var valueOrigin = value.TranslatePoint(default, volumeControls)!.Value;
        Assert.True(valueOrigin.X >= sliderOrigin.X + volume.Bounds.Width);
        Assert.True(valueOrigin.X + value.Bounds.Width <= volumeControls.Bounds.Width + 1);
        foreach (var (rowName, buttonName) in new[] { ("DueSoundRow", "ResetDueSound"), ("CompletionSoundRow", "ResetCompletionSound") })
            AssertRightAligned(Find<Grid>(window, rowName), Find<Button>(window, buttonName));
        foreach (var control in card.GetVisualDescendants().OfType<Control>().Where(item =>
            item.IsEffectivelyVisible && item is Button or ComboBox or Slider))
        {
            var origin = control.TranslatePoint(default, card)!.Value;
            Assert.True(origin.X >= 0);
            Assert.True(origin.X + control.Bounds.Width <= card.Bounds.Width + 1);
        }
    }

    [AvaloniaFact]
    public void ResizingNotificationRowsPreservesDraftAndReturnsToTheWideLayout()
    {
        using var scope = new Scope(); var window = scope.Window;
        var card = Find<Border>(window, "SettingsNotificationCard");
        var direction = Find<ComboBox>(window, "BubbleDirection");
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        direction.SelectedItem = BubbleDirection.Right; volume.Value = 42;
        foreach (var width in new[] { 468, 760, 340, 760 })
        {
            card.Width = width; Layout(window);
            Assert.Equal(BubbleDirection.Right, direction.SelectedItem); Assert.Equal(42, volume.Value);
            Assert.Equal("42%", Find<TextBlock>(window, "ReminderVolumeValue").Text);
            Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
            Assert.Equal(width < 528 ? 2 : 0, Grid.GetRow(direction));
            AssertRightAligned(Find<Grid>(window, "BubbleDirectionRow"), direction);
        }
        Assert.Equal(BubbleDirection.Top, scope.Runtime.Settings.BubbleDirection);
        Assert.Equal(100, scope.Runtime.Settings.ReminderVolumePercent);
    }

    private static void AssertRightAligned(Control row, Control editor)
    {
        var origin = editor.TranslatePoint(default, row)!.Value;
        Assert.InRange(Math.Abs(row.Bounds.Width - origin.X - editor.Bounds.Width), 0, 1);
    }
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
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
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window);
            Find<Button>(Window, "SettingsNavSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(Window);
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
