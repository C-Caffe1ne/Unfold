using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class OggSoundTests
{
    public static bool HasMediaTool => PetMediaImporter.FindFFmpeg() is not null;
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void FilePickerIncludesOggAlongsideWavAndMp3()
    {
        Assert.Contains("*.ogg", ReminderSoundImporter.FilePatterns);
        Assert.Contains("OGG", ReminderSoundImporter.SupportedFileTypes);
    }

    [Theory(Skip = "Prepare media tools to run native OGG conversion.", SkipUnless = nameof(HasMediaTool))]
    [InlineData("sound-vorbis.ogg")] [InlineData("sound-opus.ogg")]
    public async Task VorbisAndOpusKeepTheFullAudioAndSurviveSourceRemoval(string fixture)
    {
        using var temp = new TempDirectory(); var source = Path.Combine(temp.Path, "휴식 효과음.OGG");
        var original = File.ReadAllBytes(Fixture(fixture)); File.WriteAllBytes(source, original);
        var library = new ReminderSounds(Path.Combine(temp.Path, "Sounds"));
        var id = await ReminderSoundImporter.Import(source, library, TestContext.Current.CancellationToken);
        Assert.Equal(original, File.ReadAllBytes(source)); File.Delete(source);
        var data = File.ReadAllBytes(library.Resolve(ReminderSound.Completed, id, true));
        ReminderSounds.Validate(data); Assert.InRange(ReminderSounds.Duration(data).TotalSeconds, 1.20, 1.30);
        Assert.Equal(ReminderSounds.Duration(data), ReminderSounds.Duration(ReminderSounds.WithVolume(data, 25)));
        Assert.Equal(id, await ReminderSoundImporter.Import(Fixture(fixture), library, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.Combine(temp.Path, "Sounds"), "*.wav"));
    }

    [Fact(Skip = "Prepare media tools to run native OGG conversion.", SkipUnless = nameof(HasMediaTool))]
    public async Task LongBrokenWrongContainerOversizedAndCancelledOggAreRejectedWithoutInstallation()
    {
        using var temp = new TempDirectory(); var directory = Path.Combine(temp.Path, "Sounds"); var library = new ReminderSounds(directory);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(Fixture("sound-too-long.ogg"), library, TestContext.Current.CancellationToken));
        Assert.Contains("30초", error.Message);
        var broken = Path.Combine(temp.Path, "broken.ogg"); File.WriteAllText(broken, "not audio");
        await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(broken, library, TestContext.Current.CancellationToken));
        File.Copy(Fixture("sound-short.mp3"), broken, true);
        await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(broken, library, TestContext.Current.CancellationToken));
        using (var file = File.Create(broken)) file.SetLength(ReminderSoundImporter.MaxFileBytes + 1L);
        error = await Assert.ThrowsAsync<InvalidDataException>(() => ReminderSoundImporter.Import(broken, library, TestContext.Current.CancellationToken));
        Assert.Contains("5 MiB", error.Message);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReminderSoundImporter.Import(Fixture("sound-vorbis.ogg"), library, cancellation.Token));
        Assert.False(Directory.Exists(directory));
    }
}

public class NativeSoundPlaybackTests
{
    [Fact]
    public void DeviceBufferIncludesEveryDataChunkAndTheLastSampleEvenWhenFormatFollowsData()
    {
        var original = ReminderSounds.Default(ReminderSound.Completed); var samples = original[44..]; var split = samples.Length / 4 * 2;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        writer.Write("data"u8); writer.Write(split); writer.Write(samples.AsSpan(0, split));
        writer.Write("JUNK"u8); writer.Write(1); writer.Write((byte)99); writer.Write((byte)0);
        writer.Write("fmt "u8); writer.Write(16); writer.Write(original.AsSpan(20, 16));
        writer.Write("data"u8); writer.Write(samples.Length - split); writer.Write(samples.AsSpan(split));
        stream.Position = 4; writer.Write((int)stream.Length - 8);
        var (format, buffer) = NativeSoundPlayback.PcmBuffer(stream.ToArray());
        Assert.Equal(samples, buffer); Assert.Equal(original[20..36], format[..16]); Assert.Equal(new byte[2], format[16..]);
        Assert.Equal(ReminderSounds.Duration(original), ReminderSounds.Duration(stream.ToArray()));
    }
}

[Collection("Timer settings")]
public class OggSoundSettingsTests
{
    public static bool HasMediaTool => OggSoundTests.HasMediaTool;
    [AvaloniaTheory(Skip = "Prepare media tools to run native OGG conversion.", SkipUnless = nameof(HasMediaTool))]
    [InlineData("sound-vorbis.ogg")] [InlineData("sound-opus.ogg")]
    public async Task BothOggSlotsSavePreviewAndReopenWhileFailedReplacementKeepsTheSavedSound(string fixture)
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        using var window = new SettingsWindow(runtime);
        try
        {
            window.Show(); Press(window, "SettingsNavSettings");
            var source = Path.Combine(temp.Path, "알림.OGG"); File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture), source);
            window.ChooseSoundFile = _ => Task.FromResult<string?>(source);
            foreach (var button in new[] { "ImportDueSound", "ImportCompletionSound" })
            { Press(window, button); await Until(() => Find<Button>(window, button).IsEnabled); }
            Assert.NotNull(runtime.Settings.ReminderSoundId);
            Assert.Equal(runtime.Settings.ReminderSoundId, runtime.Settings.CompletionSoundId);
            var saved = AppSettings.Load(Path.Combine(temp.Path, "settings.json"));
            Assert.Equal("알림.OGG", saved.ReminderSoundName); Assert.Equal("알림.OGG", saved.CompletionSoundName);
            File.Delete(source); var calls = 0;
            window.PlaySoundPreview = (sound, settings, _) =>
            {
                var library = new ReminderSounds(Path.Combine(temp.Path, "Sounds"));
                var id = sound == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId;
                var bytes = File.ReadAllBytes(library.Resolve(sound, id, true));
                Assert.InRange(ReminderSounds.Duration(bytes).TotalSeconds, 1.2, 1.3); calls++; return Task.CompletedTask;
            };
            Press(window, "PreviewDueSound"); Press(window, "PreviewCompletionSound"); Assert.Equal(2, calls);
            using var reopened = new SettingsWindow(runtime); reopened.Show(); Press(reopened, "SettingsNavSettings");
            Assert.Equal("알림.OGG", Find<TextBlock>(reopened, "DueSoundName").Text);
            Assert.Equal("알림.OGG", Find<TextBlock>(reopened, "CompletionSoundName").Text); reopened.Hide();
            File.WriteAllText(source, "broken OGG"); Press(window, "ImportDueSound"); await Until(() => Find<Button>(window, "ImportDueSound").IsEnabled);
            Assert.Equal(saved.ReminderSoundId, runtime.Settings.ReminderSoundId);
            Assert.Equal("알림.OGG", Find<TextBlock>(window, "DueSoundName").Text);
        }
        finally
        {
            window.HideToTray(); window.Dispose(); runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous);
        }
    }
    private static T Find<T>(Control window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Press(Window window, string name)
    { Dispatcher.UIThread.RunJobs(); Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    private static async Task Until(Func<bool> ready)
    { for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); } Assert.True(ready()); }
}
