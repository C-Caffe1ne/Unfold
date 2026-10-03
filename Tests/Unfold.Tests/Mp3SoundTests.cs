using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class Mp3SoundImporterTests
{
    public static bool HasMediaTool => PetMediaImporter.FindFFmpeg() is not null;
    internal static string Fixture(string name = "sound-short.mp3") => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    [Fact]
    public async Task WavKeepsOriginalBytesAndUnsupportedFormatsAreRejected()
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(Path.Combine(temp.Path, "Sounds"));
        var path = Path.Combine(temp.Path, "effect.WAV"); var data = ReminderSounds.Default(ReminderSound.Completed); File.WriteAllBytes(path, data);
        var id = await ReminderSoundImporter.Import(path, library, TestContext.Current.CancellationToken);
        Assert.Equal(data, File.ReadAllBytes(library.Resolve(ReminderSound.Completed, id, true)));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import("unsupported.ogg", library, TestContext.Current.CancellationToken));
    }
    [Fact(Skip = "Prepare media tools to run native MP3 conversion.", SkipUnless = nameof(HasMediaTool), SkipType = typeof(Mp3SoundImporterTests))]
    public async Task Mp3ConvertsToPlayableWavWithVolumeSupportAndSurvivesSourceDeletion()
    {
        using var temp = new TempDirectory(); var source = Path.Combine(temp.Path, "내 효과음.MP3"); File.Copy(Fixture(), source);
        var library = new ReminderSounds(Path.Combine(temp.Path, "Sounds"));
        var id = await ReminderSoundImporter.Import(source, library, TestContext.Current.CancellationToken); File.Delete(source);
        var data = File.ReadAllBytes(library.Resolve(ReminderSound.Due, id, true));
        ReminderSounds.Validate(data); Assert.InRange(ReminderSounds.Duration(data).TotalSeconds, 0.74, 0.85);
        Assert.Equal("RIFF"u8.ToArray(), data[..4]);
        var silent = ReminderSounds.WithVolume(data, 0);
        Assert.NotEqual(data, silent); Assert.Equal(ReminderSounds.Duration(data), ReminderSounds.Duration(silent));
        Assert.Equal(id, await ReminderSoundImporter.Import(Fixture(), library, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.Combine(temp.Path, "Sounds"), "*.wav"));
    }
    [Fact(Skip = "Prepare media tools to run native MP3 conversion.", SkipUnless = nameof(HasMediaTool), SkipType = typeof(Mp3SoundImporterTests))]
    public async Task LongBrokenOversizedAndCancelledMp3ImportsLeaveNoInstalledCopy()
    {
        using var temp = new TempDirectory(); var soundRoot = Path.Combine(temp.Path, "Sounds"); var library = new ReminderSounds(soundRoot);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(Fixture("sound-too-long.mp3"), library, TestContext.Current.CancellationToken));
        Assert.Contains("30초", error.Message);
        var broken = Path.Combine(temp.Path, "broken.mp3"); File.WriteAllText(broken, "not audio");
        await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(broken, library, TestContext.Current.CancellationToken));
        var large = Path.Combine(temp.Path, "large.mp3"); using (var stream = File.Create(large)) stream.SetLength(ReminderSoundImporter.MaxFileBytes + 1);
        error = await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(large, library, TestContext.Current.CancellationToken)); Assert.Contains("5 MiB", error.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReminderSoundImporter.Import(Fixture(), library, cancellation.Token));
        Assert.False(Directory.Exists(soundRoot));
    }
}

[Collection("Timer settings")]
public class Mp3SoundSettingsTests
{
    public static bool HasMediaTool => PetMediaImporter.FindFFmpeg() is not null;
    [AvaloniaFact(Skip = "Prepare media tools to run native MP3 conversion.", SkipUnless = nameof(HasMediaTool), SkipType = typeof(Mp3SoundSettingsTests))]
    public async Task BothNotificationMp3SlotsApplyImmediatelyAndCanBePreviewedAndReopened()
    {
        using var scope = new Scope(); var window = scope.Window;
        var source = Path.Combine(scope.Root, "알림 테스트.mp3"); File.Copy(Mp3SoundImporterTests.Fixture(), source);
        window.ChooseSoundFile = _ => Task.FromResult<string?>(source);
        foreach (var button in new[] { "ImportDueSound", "ImportCompletionSound" })
        { scope.Click(button); await Until(() => scope.Find<Button>(button).IsEnabled); }
        Assert.Equal("알림 테스트.mp3", scope.Find<TextBlock>("DueSoundName").Text);
        Assert.Equal("알림 테스트.mp3", scope.Find<TextBlock>("CompletionSoundName").Text);
        Assert.NotNull(scope.Runtime.Settings.ReminderSoundId);
        var previews = new List<ReminderSound>();
        window.PlaySoundPreview = (kind, settings, _) =>
        {
            var id = kind == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId;
            var data = File.ReadAllBytes(new ReminderSounds(scope.Sounds).Resolve(kind, id, true));
            ReminderSounds.Validate(data); Assert.InRange(ReminderSounds.Duration(data).TotalSeconds, 0.74, 0.85);
            previews.Add(kind); return Task.CompletedTask;
        };
        scope.Click("PreviewDueSound"); scope.Click("PreviewCompletionSound");
        Assert.Equal(new[] { ReminderSound.Due, ReminderSound.Completed }, previews);
        File.Delete(source);
        var saved = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(saved.ReminderSoundId, saved.CompletionSoundId); Assert.Equal("알림 테스트.mp3", saved.ReminderSoundName);
        Assert.Equal("알림 테스트.mp3", saved.CompletionSoundName);
        var imported = new ReminderSounds(scope.Sounds).Resolve(ReminderSound.Due, saved.ReminderSoundId, true);
        Assert.True(File.Exists(imported));
        window.HideToTray(); window.Dispose();
        using var reopened = new SettingsWindow(scope.Runtime); reopened.Show(); Dispatcher.UIThread.RunJobs();
        reopened.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "SettingsNavSettings")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
        var name = reopened.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "DueSoundName");
        Assert.Equal("알림 테스트.mp3", name.Text); reopened.HideToTray();
    }
    [AvaloniaFact(Skip = "Prepare media tools to run native MP3 conversion.", SkipUnless = nameof(HasMediaTool), SkipType = typeof(Mp3SoundSettingsTests))]
    public async Task FailedMp3ReplacementKeepsSavedSoundAndResetRemovesOnlyImportedCopies()
    {
        using var scope = new Scope(); scope.Window.ChooseSoundFile = _ => Task.FromResult<string?>(Mp3SoundImporterTests.Fixture());
        scope.Click("ImportDueSound"); await Until(() => scope.Find<Button>("ImportDueSound").IsEnabled);
        var saved = scope.Runtime.Settings.ReminderSoundId; Assert.NotNull(saved);
        scope.Window.ChooseSoundFile = _ => Task.FromResult<string?>(Mp3SoundImporterTests.Fixture("sound-too-long.mp3"));
        scope.Click("ImportDueSound"); await Until(() => scope.Find<Button>("ImportDueSound").IsEnabled);
        Assert.Contains("30초", scope.Find<TextBlock>("PreferencesStatus").Text);
        Assert.Equal(saved, scope.Runtime.Settings.ReminderSoundId); Assert.Equal("sound-short.mp3", scope.Find<TextBlock>("DueSoundName").Text);
        Assert.Single(Directory.GetFiles(scope.Sounds, "*.wav"));
        scope.Click("ResetDueSound"); Assert.Empty(Directory.GetFiles(scope.Sounds, "*.wav"));
        scope.Window.ChooseSoundFile = _ => Task.FromResult<string?>(Mp3SoundImporterTests.Fixture());
        scope.Click("ImportCompletionSound"); await Until(() => scope.Find<Button>("ImportCompletionSound").IsEnabled);
        Assert.Single(Directory.GetFiles(scope.Sounds, "*.wav")); scope.Click("ResetCompletionSound"); Assert.Empty(Directory.GetFiles(scope.Sounds, "*.wav"));
    }
    public static bool IsWindows => OperatingSystem.IsWindows();
    [AvaloniaFact(Skip = "POSIX process cancellation probe.", SkipWhen = nameof(IsWindows), SkipType = typeof(Mp3SoundSettingsTests))]
    public async Task DisposingDuringConversionStopsTheProcessAndInstallsNoSound()
    {
        if (OperatingSystem.IsWindows()) return;
        using var scope = new Scope(); var previous = Environment.GetEnvironmentVariable("UNFOLD_FFMPEG_PATH");
        var executable = Path.Combine(scope.Root, "converter"); var marker = Path.Combine(scope.Root, "started");
        File.WriteAllText(executable, $"#!/bin/sh\nprintf '%s' $$ > '{marker}'\nexec /bin/sleep 30\n");
        File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            Environment.SetEnvironmentVariable("UNFOLD_FFMPEG_PATH", executable);
            scope.Window.ChooseSoundFile = _ => Task.FromResult<string?>(Mp3SoundImporterTests.Fixture());
            scope.Click("ImportDueSound"); await Until(() => File.Exists(marker) && new FileInfo(marker).Length > 0);
            var processId = int.Parse(File.ReadAllText(marker));
            scope.Window.Dispose(); await Until(() => scope.Find<Button>("ImportDueSound").IsEnabled);
            Assert.Empty(Directory.Exists(scope.Sounds) ? Directory.GetFiles(scope.Sounds, "*.wav") : []);
            Assert.Throws<ArgumentException>(() => System.Diagnostics.Process.GetProcessById(processId));
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_FFMPEG_PATH", previous); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
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
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Click(string name) { Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
        public void Dispose()
        { Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
