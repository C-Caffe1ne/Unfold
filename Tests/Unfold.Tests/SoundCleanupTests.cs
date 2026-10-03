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
public class SoundCleanupTests
{
    [AvaloniaFact]
    public async Task ResetAndReplacementRemoveOnlyUnusedCopies()
    {
        using var scope = new Scope();
        var first = await scope.Import("ImportDueSound", ReminderSound.Due);
        var second = await scope.Import("ImportDueSound", ReminderSound.Completed);
        Assert.False(File.Exists(first)); Assert.True(File.Exists(second));
        scope.Click("ResetDueSound"); Assert.False(File.Exists(second));
        Assert.True(File.Exists(scope.Source(ReminderSound.Due))); Assert.True(File.Exists(scope.Source(ReminderSound.Completed)));
    }

    [AvaloniaFact]
    public async Task SharedSoundSurvivesUntilBothReferencesAreReset()
    {
        using var scope = new Scope();
        var shared = await scope.Import("ImportDueSound", ReminderSound.Due);
        await scope.Import("ImportCompletionSound", ReminderSound.Due);
        Assert.Equal(scope.Runtime.Settings.ReminderSoundId, scope.Runtime.Settings.CompletionSoundId);
        scope.Click("ResetDueSound"); Assert.True(File.Exists(shared));
        scope.Click("ResetCompletionSound"); Assert.False(File.Exists(shared));
    }

    [AvaloniaFact]
    public async Task FailedAutomaticSaveKeepsTheSavedFileAndDisposalRemovesOnlyTheUnappliedReplacement()
    {
        using var scope = new Scope();
        var saved = await scope.Import("ImportDueSound", ReminderSound.Due);
        var settingsPath = Path.Combine(scope.Root, "settings.json"); File.Delete(settingsPath); Directory.CreateDirectory(settingsPath);
        var pending = await scope.Import("ImportDueSound", ReminderSound.Completed);
        Assert.True(scope.Find<TextBlock>("PreferencesStatus").IsVisible);
        Assert.True(File.Exists(saved)); Assert.True(File.Exists(pending));
        scope.Window.Dispose(); Assert.True(File.Exists(saved)); Assert.False(File.Exists(pending));
    }

    [AvaloniaFact]
    public async Task NavigationHidingAndDisposalKeepTheAutomaticallySavedSound()
    {
        using var scope = new Scope(); var saved = await scope.Import("ImportDueSound", ReminderSound.Due);
        scope.Click("SettingsNavTimer"); Assert.Empty(scope.Window.OwnedWindows);
        scope.Window.HideToTray(); Assert.True(File.Exists(saved));
        scope.Window.Show(); scope.Click("SettingsNavSettings");
        Assert.NotNull(scope.Runtime.Settings.ReminderSoundId);
        scope.Window.Dispose(); Assert.True(File.Exists(saved));
    }

    [AvaloniaFact]
    public async Task PickerReturningAfterDisposalDoesNotInstallAFile()
    {
        using var scope = new Scope(); var pending = new TaskCompletionSource<string?>();
        scope.Window.ChooseSoundFile = _ => pending.Task; scope.Click("ImportDueSound");
        scope.Window.Dispose(); pending.SetResult(scope.Source(ReminderSound.Due));
        await Task.Delay(100, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        Assert.Empty(Directory.Exists(scope.Sounds) ? Directory.GetFiles(scope.Sounds, "*.wav") : []);
    }

    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 250 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public string Sounds => Path.Combine(Root, "Sounds");
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs(); Click("SettingsNavSettings");
            foreach (var kind in Enum.GetValues<ReminderSound>()) File.WriteAllBytes(Source(kind), ReminderSounds.Default(kind));
        }
        public string Source(ReminderSound kind) => Path.Combine(Root, kind + ".wav");
        public async Task<string> Import(string button, ReminderSound kind)
        {
            Window.ChooseSoundFile = _ => Task.FromResult<string?>(Source(kind)); Click(button);
            await Until(() => Find<Button>(button).IsEnabled);
            var id = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Source(kind))));
            var path = Path.Combine(Sounds, id + ".wav"); Assert.True(File.Exists(path)); return path;
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Click(string name) { Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public void Dispose()
        { Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
