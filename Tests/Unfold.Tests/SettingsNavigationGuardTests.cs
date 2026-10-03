using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class SettingsNavigationGuardTests
{
    [AvaloniaTheory]
    [InlineData("SettingsNavTimer")]
    [InlineData("SettingsNavPacks")]
    [InlineData("SettingsNavReview")]
    public void EveryPageTransitionAppliesEditsWithoutOpeningAModal(string destination)
    {
        using var scope = new Scope(); var window = scope.Window;
        var page = Find<ContentControl>(window, "SettingsPageHost").Content;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 9;
        Find<Slider>(window, "ReminderVolumePercent").Value = 37;
        Press(window, destination);
        Assert.NotSame(page, Find<ContentControl>(window, "SettingsPageHost").Content); Assert.Empty(window.OwnedWindows);
        var saved = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(9, saved.IdleMinutes); Assert.Equal(37, saved.ReminderVolumePercent);
        Press(window, "SettingsNavSettings"); Assert.Equal(9, Find<NumericUpDown>(window, "ReminderIdle").Value);
    }

    [AvaloniaFact]
    public void InvalidInputDoesNotBlockNavigationOrChangeTheSavedValue()
    {
        using var scope = new Scope(); var window = scope.Window;
        var page = Find<ContentControl>(window, "SettingsPageHost").Content;
        Find<NumericUpDown>(window, "ReminderIdle").Text = "";
        Press(window, "SettingsNavReview"); Assert.NotSame(page, Find<ContentControl>(window, "SettingsPageHost").Content);
        Assert.Empty(window.OwnedWindows); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
    }

    [AvaloniaFact]
    public void StorageFailureDoesNotBlockNavigationAndReopeningRetriesTheEdit()
    {
        using var scope = new Scope(); var window = scope.Window;
        var path = Path.Combine(scope.Root, "settings.json"); Directory.CreateDirectory(path);
        Find<NumericUpDown>(window, "ReminderIdle").Value = 9;
        Assert.True(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
        Press(window, "SettingsNavTimer"); Assert.Empty(window.OwnedWindows); Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Directory.Delete(path); Press(window, "SettingsNavSettings");
        Assert.Equal(9, AppSettings.Load(path).IdleMinutes); Assert.False(Find<TextBlock>(window, "PreferencesStatus").IsVisible);
    }

    [AvaloniaFact]
    public void RepeatedNavigationDoesNotOpenSettingsModals()
    {
        using var scope = new Scope(); var window = scope.Window;
        Find<NumericUpDown>(window, "ReminderIdle").Value = 9;
        foreach (var destination in new[] { "SettingsNavTimer", "SettingsNavReview", "SettingsNavPacks", "SettingsNavSettings" }) Press(window, destination);
        Assert.Empty(window.OwnedWindows); Assert.Equal(9, scope.Runtime.Settings.IdleMinutes);
    }

    [AvaloniaFact]
    public async Task NavigationDuringAnImportDoesNotDiscardTheResult()
    {
        using var scope = new Scope(); var window = scope.Window;
        var page = Find<ContentControl>(window, "SettingsPageHost").Content;
        var selected = new TaskCompletionSource<string?>(); window.ChooseSoundFile = _ => selected.Task;
        Press(window, "ImportDueSound"); Press(window, "SettingsNavTimer");
        Assert.NotSame(page, Find<ContentControl>(window, "SettingsPageHost").Content); Assert.Empty(window.OwnedWindows);
        var sound = Path.Combine(scope.Root, "sound.wav"); File.WriteAllBytes(sound, ReminderSounds.Default(ReminderSound.Due));
        selected.SetResult(sound); await Until(() => scope.Runtime.Settings.ReminderSoundId is not null);
        var saved = AppSettings.Load(Path.Combine(scope.Root, "settings.json")); Assert.NotNull(saved.ReminderSoundId);
        Assert.True(File.Exists(Path.Combine(scope.Root, "Sounds", saved.ReminderSoundId + ".wav")));
    }

    [AvaloniaFact]
    public void SavedPreferencesSurviveDisposalAndReopening()
    {
        using var scope = new Scope();
        Find<NumericUpDown>(scope.Window, "SnoozeMinutes").Value = 13;
        Find<CheckBox>(scope.Window, "ReminderSoundsEnabled").IsChecked = false;
        scope.Window.HideToTray(); scope.Window.Dispose();
        using var reopened = new AppRuntime(new ClassicDesktopStyleApplicationLifetime());
        Assert.Equal(13, reopened.Settings.SnoozeMinutes); Assert.False(reopened.Settings.ReminderSoundsEnabled);
    }

    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 200; i++) { Layout(); if (ready()) return; await Task.Delay(10, TestContext.Current.CancellationToken); }
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
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(); Press(Window, "SettingsNavSettings");
        }
        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
