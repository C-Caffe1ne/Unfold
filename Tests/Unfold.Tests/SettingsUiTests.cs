using System.Text.Json;
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

public class SettingsUiTests
{
    // Bundles an isolated AppRuntime and undoes the process-wide UNFOLD_DATA_DIR
    // mutation afterwards, so tests never touch the real user's settings.json/character
    // library and never leak state to other tests. IClassicDesktopStyleApplicationLifetime
    // cannot be implemented outside Avalonia, so this uses the real concrete lifetime;
    // none of the code paths under test call Start() or Quit(), so it never runs a
    // message loop or shows a real main window.
    private sealed class Fixture : IDisposable
    {
        private readonly string? previousDataDir = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly TempDirectory temp = new();
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public Fixture()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new AppRuntime(lifetime);
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousDataDir);
            temp.Dispose();
        }
    }

    // Reload constructs fresh package instances; compare the current runtime identity.
    private static async Task<CharacterPackage> AddCharacter(AppRuntime runtime, string name, bool opaque)
    {
        var document = new SpriteFixture(4, 4) { Name = name };
        if (opaque) Array.Fill(document.Pixels, 0xFFFFFFFFu);
        var saved = runtime.Library.InstallFixture(document);
        await runtime.Reload();
        return runtime.Characters.Single(c => c.Manifest.Id == saved.Manifest.Id);
    }

    // A sprite-sheet character's Sheet is decoded once and cached for the life of the
    // CharacterPackage object (including by the eager validation Library.List() already
    // performs during Reload()), so corrupting spritesheet.png on disk afterwards cannot
    // reproduce a real decode failure. A GIF-backed "idle" animation has no such cache -
    // LoadAnimation re-reads and re-decodes the GIF file on every call - so this builds
    // one by hand to get a load failure that is genuinely tied to the file's contents.
    private static readonly byte[] SampleGif = File.ReadAllBytes(
        Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "default-cat", "stretch.gif"));

    private static async Task<CharacterPackage> AddGifCharacter(AppRuntime runtime, string name)
    {
        var saved = runtime.Library.InstallFixture(new SpriteFixture(4, 4) { Name = name });
        File.WriteAllBytes(Path.Combine(saved.DirectoryPath, "idle.gif"), SampleGif);
        var manifest = saved.Manifest with { Animations = new() { ["idle"] = new AnimationDefinition(Gif: "idle.gif") } };
        File.WriteAllBytes(Path.Combine(saved.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        await runtime.Reload();
        return runtime.Characters.Single(c => c.Manifest.Id == saved.Manifest.Id);
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 25; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20, TestContext.Current.CancellationToken); }
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<Window> Dialog(Window owner)
    {
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (owner.OwnedWindows.FirstOrDefault() is { } dialog) return dialog;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        throw new TimeoutException("Expected an error dialog.");
    }

    private static T Control<T>(Window window, Func<T, bool> match) where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(match);


    // Replacing a read-only file is allowed on Unix. A directory at the destination
    // makes AtomicFile.Write fail on both platforms without changing permissions.
    private static IDisposable BlockSettingsSave(string file)
    {
        File.Move(file, file + ".saved"); Directory.CreateDirectory(file);
        return new RestoreSettings(file);
    }
    private sealed class RestoreSettings(string file) : IDisposable
    {
        private bool restored;
        public void Dispose()
        {
            if (restored) return;
            Directory.Delete(file); File.Move(file + ".saved", file); restored = true;
        }
    }

    [AvaloniaFact]
    public void TitleDoesNotAdvertiseTheHiddenCharacterCreator()
    {
        using var fixture = new Fixture();
        var window = new SettingsWindow(fixture.Runtime);
        Assert.DoesNotContain("Create", window.Title);
    }

    [AvaloniaFact]
    public async Task FailedShowPetSaveRevertsCheckboxToLastKnownState()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        await AddCharacter(runtime, "Rex", opaque: false);
        await runtime.UpdateSettings(runtime.Settings);
        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();
        Control<Button>(window, button => button.Name == "SettingsNavSettings")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        var showPet = Control<CheckBox>(window, c => Equals(c.Content, "바탕화면에 펫 표시"));
        Assert.True(showPet.IsChecked);

        var settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
        using var blockedSave = BlockSettingsSave(settingsFile);
        try
        {
            showPet.IsChecked = false;
            (await Dialog(window)).Close();
            await Settle();
        }
        finally { blockedSave.Dispose(); }

        Assert.True(showPet.IsChecked);
        Assert.True(runtime.Settings.ShowPet);
    }

    [AvaloniaFact]
    public async Task FailedCharacterSaveRevertsSelectionToLastKnownState()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        // Each AddCharacter call reloads the whole library, replacing every previously
        // fetched CharacterPackage instance - so resolve both handles only after both
        // characters exist, to match the identities SettingsWindow will actually see.
        var firstId = (await AddCharacter(runtime, "Rex", opaque: false)).Manifest.Id;
        var secondId = (await AddCharacter(runtime, "Mochi Jr", opaque: false)).Manifest.Id;
        var first = runtime.Characters.Single(c => c.Manifest.Id == firstId);
        var second = runtime.Characters.Single(c => c.Manifest.Id == secondId);
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = first.Manifest.Id });
        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();
        var characters = Control<ComboBox>(window, combo => combo.Name == "CharacterPicker");
        Assert.Equal(first, characters.SelectedItem);

        var settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
        using var blockedSave = BlockSettingsSave(settingsFile);
        try
        {
            characters.SelectedItem = second;
            (await Dialog(window)).Close();
            await Settle();
        }
        finally { blockedSave.Dispose(); }

        Assert.Equal(first, characters.SelectedItem);
        Assert.Equal(first.Manifest.Id, runtime.Settings.SelectedCharacterId);
    }

    [AvaloniaFact]
    public async Task LatestCharacterSelectionUpdatesWithoutATimerPreview()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        var blank = await AddCharacter(runtime, "Blank", opaque: false);
        var filled = await AddCharacter(runtime, "Filled", opaque: true);
        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();

        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = filled.Manifest.Id });
        await Settle();
        Assert.Empty(window.GetVisualDescendants().OfType<AnimationView>());

        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = blank.Manifest.Id });
        await Settle();
        Assert.Empty(window.GetVisualDescendants().OfType<AnimationView>());

        Assert.Equal(blank.Manifest.Id, runtime.Settings.SelectedCharacterId);
    }

    [AvaloniaFact]
    public async Task TimerSettingsDoesNotDecodeRemovedPreviewForACorruptGif()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        var character = await AddGifCharacter(runtime, "Rex");
        // Disable desktop playback to verify that the timer page makes no animation request.
        await runtime.UpdateSettings(runtime.Settings with { ShowPet = false, SelectedCharacterId = character.Manifest.Id });

        var gif = Path.Combine(character.DirectoryPath, "idle.gif");
        File.WriteAllBytes(gif, [0, 1, 2, 3]); // Timer settings must remain independent of this animation.

        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();
        await Settle();
        Assert.Empty(window.GetVisualDescendants().OfType<AnimationView>());

        File.WriteAllBytes(gif, SampleGif); // repaired on disk
        // The real recovery path: Reload() constructs a fresh CharacterPackage instance
        // (distinct from the one the failed load latched onto) and clears AppRuntime's
        // own clip cache, so the new instance's first load is a genuine, uncached attempt.
        await runtime.Reload();
        await Settle();

        Assert.Empty(window.GetVisualDescendants().OfType<AnimationView>());
    }
}
