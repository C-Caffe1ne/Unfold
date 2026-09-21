using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class SettingsPreferencesTests
{
    [AvaloniaFact]
    public void VolumeIsDraftedPreviewedCancelledAndSavedWithTheOtherNotificationSettings()
    {
        using var scope = new Scope(); var window = scope.Window;
        var volume = Find<Slider>(window, "ReminderVolumePercent");
        var label = Find<TextBlock>(window, "ReminderVolumeValue");
        var save = Find<Button>(window, "SavePreferences");
        Assert.Equal(0, volume.Minimum); Assert.Equal(100, volume.Maximum); Assert.Equal(100, volume.Value);
        Assert.Equal("알림 소리 크기, 퍼센트", AutomationProperties.GetName(volume));
        Assert.False(save.IsEnabled);
        var previews = new List<(ReminderSound Sound, int Volume)>();
        window.PlaySoundPreview = (sound, settings, _) =>
        { previews.Add((sound, settings.ReminderVolumePercent)); return Task.CompletedTask; };
        volume.Value = 37;
        Assert.Equal("37%", label.Text); Assert.True(save.IsEnabled);
        Assert.Equal(100, scope.Runtime.Settings.ReminderVolumePercent);
        Press(window, "PreviewDueSound"); Press(window, "PreviewCompletionSound");
        Assert.Equal(new[] { (ReminderSound.Due, 37), (ReminderSound.Completed, 37) }, previews);
        Assert.Equal(100, scope.Runtime.Settings.ReminderVolumePercent);
        Press(window, "SettingsNavTimer"); Press(window, "SettingsNavSettings");
        Assert.Equal(37, volume.Value);
        Press(window, "CancelPreferences");
        Assert.Equal(100, volume.Value); Assert.Equal("100%", label.Text); Assert.False(save.IsEnabled);
        volume.Value = 0; Press(window, "SavePreferences");
        Assert.False(save.IsEnabled);
        Assert.Equal(0, scope.Runtime.Settings.ReminderVolumePercent);
        Assert.Equal(0, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).ReminderVolumePercent);
        volume.Value = 100; Press(window, "CancelPreferences");
        Assert.Equal(0, volume.Value); Assert.Equal("0%", label.Text); Assert.False(save.IsEnabled);
    }

    [AvaloniaFact]
    public void SaveTracksTheWholeFormAndCancelRestoresTheLastSave()
    {
        using var scope = new Scope(); var window = scope.Window;
        var save = Find<Button>(window, "SavePreferences");
        var direction = Find<ComboBox>(window, "BubbleDirection");
        var sounds = Find<CheckBox>(window, "ReminderSoundsEnabled");
        var idle = Find<NumericUpDown>(window, "ReminderIdle");
        var snooze = Find<NumericUpDown>(window, "SnoozeMinutes");
        Assert.False(save.IsEnabled);
        direction.SelectedItem = BubbleDirection.Right; Assert.True(save.IsEnabled);
        direction.SelectedItem = BubbleDirection.Top; Assert.False(save.IsEnabled);
        sounds.IsChecked = false; idle.Value = 8; snooze.Value = 12;
        Assert.True(save.IsEnabled); Assert.True(scope.Runtime.Settings.ReminderSoundsEnabled);
        Press(window, "SavePreferences");
        Assert.False(save.IsEnabled);
        var persisted = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.False(persisted.ReminderSoundsEnabled); Assert.Equal(8, persisted.IdleMinutes); Assert.Equal(12, persisted.SnoozeMinutes);
        idle.Value = 20; direction.SelectedItem = BubbleDirection.Bottom; sounds.IsChecked = true;
        Press(window, "SettingsNavTimer"); Press(window, "SettingsNavSettings");
        Assert.Equal(20, idle.Value); Assert.True(save.IsEnabled);
        Press(window, "CancelPreferences");
        Assert.Equal(8, idle.Value); Assert.Equal(12, snooze.Value); Assert.False(sounds.IsChecked);
        Assert.Equal(BubbleDirection.Top, direction.SelectedItem); Assert.False(save.IsEnabled);
        Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
    }

    [AvaloniaFact]
    public void SaveFailurePreservesBothSectionsAndAllowsRetry()
    {
        using var scope = new Scope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "settings.json"); Directory.CreateDirectory(path);
        Find<ComboBox>(window, "BubbleDirection").SelectedItem = BubbleDirection.Left;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 17;
        Find<Slider>(window, "ReminderVolumePercent").Value = 23;
        Press(window, "SavePreferences");
        Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.True(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        Assert.Contains("저장하지 못했어요", Find<TextBlock>(window, "PreferencesStatus").Text);
        Assert.Equal(BubbleDirection.Top, scope.Runtime.Settings.BubbleDirection);
        Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Assert.Equal(100, scope.Runtime.Settings.ReminderVolumePercent);
        Assert.Equal(23, Find<Slider>(window, "ReminderVolumePercent").Value);
        Assert.Equal(17, Find<NumericUpDown>(window, "ReminderIdle").Value);
        Assert.Equal(BubbleDirection.Left, Find<ComboBox>(window, "BubbleDirection").SelectedItem);
        Directory.Delete(path); Press(window, "SavePreferences");
        Assert.Equal(17, AppSettings.Load(path).IdleMinutes);
        Assert.Equal(23, AppSettings.Load(path).ReminderVolumePercent);
        Assert.Equal(BubbleDirection.Left, scope.Runtime.Settings.BubbleDirection);
        Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
    }

    [AvaloniaFact]
    public void InvalidRawNumberTextCannotSaveAndCancelRepairsTheEditor()
    {
        using var scope = new Scope(); var window = scope.Window;
        var idle = Find<NumericUpDown>(window, "ReminderIdle");
        Find<ComboBox>(window, "BubbleDirection").SelectedItem = BubbleDirection.Left;
        foreach (var invalid in new[] { "", "abc", "2.5" })
        {
            idle.Text = invalid; Layout(window);
            Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
            Press(window, "SavePreferences"); Assert.Equal(BubbleDirection.Top, scope.Runtime.Settings.BubbleDirection);
        }
        Press(window, "CancelPreferences");
        Assert.Equal(5, idle.Value); Assert.Equal("5", idle.Text);
        Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
        idle.Value = 9; Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
    }

    [AvaloniaFact]
    public async Task ImportLocksSaveAndCancelUntilTheResultIsKnown()
    {
        using var scope = new Scope(); var window = scope.Window;
        var selectedFile = new TaskCompletionSource<string?>();
        window.ChooseSoundFile = _ => selectedFile.Task;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 11;
        Press(window, "ImportDueSound");
        Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.False(Find<Button>(window, "CancelPreferences").IsEnabled);
        selectedFile.SetResult(null);
        await Until(() => Find<Button>(window, "CancelPreferences").IsEnabled);
        Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.Equal(11, Find<NumericUpDown>(window, "ReminderIdle").Value);
        Press(window, "CancelPreferences"); Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
    }

    [AvaloniaFact]
    public async Task CancelRestoresImportedSoundsAndPreviewDoesNotDirtyTheForm()
    {
        using var scope = new Scope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "완료 알림 원본.wav"); File.WriteAllBytes(path, ReminderSounds.Default(ReminderSound.Completed));
        window.ChooseSoundFile = _ => Task.FromResult<string?>(path);
        CancellationToken playing = default;
        window.PlaySoundPreview = (_, _, token) => { playing = token; return Task.Delay(Timeout.Infinite, token); };
        Press(window, "PreviewDueSound"); Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
        Press(window, "CancelPreferences"); Assert.True(playing.IsCancellationRequested);
        Press(window, "ImportCompletionSound"); await Until(() => Find<Button>(window, "CancelPreferences").IsEnabled);
        Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Assert.Null(scope.Runtime.Settings.CompletionSoundId);
        Press(window, "CancelPreferences"); Assert.Equal("기본 효과음", Find<TextBlock>(window, "CompletionSoundName").Text);
        Press(window, "ImportCompletionSound"); await Until(() => Find<Button>(window, "CancelPreferences").IsEnabled);
        Press(window, "SavePreferences"); var savedId = scope.Runtime.Settings.CompletionSoundId;
        Assert.NotNull(savedId);
        Press(window, "ResetCompletionSound"); Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Press(window, "CancelPreferences"); Assert.Equal("완료 알림 원본.wav", Find<TextBlock>(window, "CompletionSoundName").Text);
        Assert.Equal(savedId, scope.Runtime.Settings.CompletionSoundId); Assert.False(Find<Button>(window, "SavePreferences").IsEnabled);
    }

    [AvaloniaFact]
    public void MinimumLayoutKeepsRowsStableAndPopupHasOneSurface()
    {
        using var scope = new Scope(); var window = scope.Window;
        window.Width = 860; window.Height = 680; Layout(window);
        var import = Find<Button>(window, "ImportDueSound"); var before = import.TranslatePoint(default, window)!.Value;
        var filename = Find<TextBlock>(window, "DueSoundName");
        filename.Text = new string('가', 100) + ".wav"; Layout(window);
        Assert.Equal(before, import.TranslatePoint(default, window)!.Value);
        Assert.Equal(Find<Button>(window, "ImportCompletionSound").Bounds.Width, import.Bounds.Width);
        var complete = Find<Grid>(window, "CompletionSoundRow"); var enabled = Find<CheckBox>(window, "ReminderSoundsEnabled");
        Assert.True(enabled.TranslatePoint(default, window)!.Value.Y > complete.TranslatePoint(default, window)!.Value.Y + complete.Bounds.Height);
        var volume = Find<Slider>(window, "ReminderVolumePercent"); var volumeValue = Find<TextBlock>(window, "ReminderVolumeValue");
        Assert.True(volume.Bounds.Width > 0);
        Assert.True(volumeValue.TranslatePoint(default, window)!.Value.X >= volume.TranslatePoint(default, window)!.Value.X + volume.Bounds.Width);
        var choice = Find<ComboBox>(window, "BubbleDirection");
        var closedBorder = choice.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Background");
        var original = (closedBorder.Background, closedBorder.BorderBrush, closedBorder.BorderThickness);
        window.MouseMove(choice.TranslatePoint(new Point(8, 8), window)!.Value); Layout(window);
        Assert.Equal(original, (closedBorder.Background, closedBorder.BorderBrush, closedBorder.BorderThickness));
        choice.IsDropDownOpen = true; Layout(window);
        try
        {
            var popup = choice.GetVisualDescendants().OfType<Popup>().Single();
            var surface = Assert.IsType<Border>(popup.Child);
            Assert.Equal(new Thickness(1), surface.BorderThickness); Assert.Equal(new Thickness(4), surface.Padding);
            Assert.Equal(DesignSystem.Surface, surface.Background);
            Assert.Equal(choice.Bounds.Width, surface.Bounds.Width);
            Assert.Equal(4, choice.ItemCount);
            var selected = Assert.IsType<ComboBoxItem>(choice.ContainerFromIndex(0));
            Assert.Equal(DesignSystem.Ink, selected.GetVisualDescendants().OfType<TextBlock>().Single().Foreground);
        }
        finally { choice.IsDropDownOpen = false; }
        var scroll = Find<ScrollViewer>(window, "SettingsPreferencesScroll");
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
    }

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window); Press(Window, "SettingsNavSettings");
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
