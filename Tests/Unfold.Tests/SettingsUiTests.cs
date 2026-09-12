using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
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

    // Library.Save() and Reload() each construct their own CharacterPackage instances,
    // so the object Save() hands back is never the one runtime.Selected/Characters will
    // later expose. Re-fetching after Reload() keeps tests comparing the same identity
    // SettingsWindow actually sees.
    private static async Task<CharacterPackage> AddCharacter(AppRuntime runtime, string name, bool opaque)
    {
        var document = new PixelDocument(4, 4) { Name = name };
        if (opaque) document.Draw(PixelTool.Fill, new(0, 0), new(0, 0), 0xFFFFFFFF, 1, 0, 0);
        var saved = runtime.Library.Save(document);
        await runtime.Reload();
        return runtime.Characters.Single(c => c.Manifest.Id == saved.Manifest.Id);
    }

    // A sprite-sheet character's Sheet is decoded once and cached for the life of the
    // CharacterPackage object (including by the eager validation Library.List() already
    // performs during Reload()), so corrupting spritesheet.png on disk afterwards cannot
    // reproduce a real decode failure. A GIF-backed "idle" animation has no such cache -
    // LoadAnimation re-reads and re-decodes the GIF file on every call - so this builds
    // one by hand to get a load failure that is genuinely tied to the file's contents.
    private static readonly byte[] SampleGif = File.ReadAllBytes(Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../Sources/Unfold/Resources/Characters/default-cat/stretch.gif")));

    private static async Task<CharacterPackage> AddGifCharacter(AppRuntime runtime, string name)
    {
        var saved = runtime.Library.Save(new PixelDocument(4, 4) { Name = name });
        File.WriteAllBytes(Path.Combine(saved.DirectoryPath, "idle.gif"), SampleGif);
        var manifest = saved.Manifest with { Animations = new() { ["idle"] = new AnimationDefinition(Gif: "idle.gif") } };
        File.WriteAllBytes(Path.Combine(saved.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        await runtime.Reload();
        return runtime.Characters.Single(c => c.Manifest.Id == saved.Manifest.Id);
    }

    private static AnimationView Preview(Window window) => window.GetVisualDescendants().OfType<AnimationView>().Single();

    private static bool AnyOpaque(AnimationView view)
    {
        for (var x = 6; x < 120; x += 12) for (var y = 6; y < 120; y += 12) if (view.OpaqueAt(new Point(x, y))) return true;
        return false;
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

    private static readonly Point Center = new(60, 60);

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
        await runtime.UpdateSettings(runtime.Settings); // ensure settings.json exists before we lock it
        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();
        var showPet = Control<CheckBox>(window, c => Equals(c.Content, "Show desktop pet"));
        Assert.True(showPet.IsChecked);

        var settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
        File.SetAttributes(settingsFile, FileAttributes.ReadOnly);
        try
        {
            showPet.IsChecked = false;
            (await Dialog(window)).Close();
            await Settle();
        }
        finally { File.SetAttributes(settingsFile, FileAttributes.Normal); }

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
        var characters = Control<ComboBox>(window, _ => true);
        Assert.Equal(first, characters.SelectedItem);

        var settingsFile = Path.Combine(AppPaths.DataRoot, "settings.json");
        File.SetAttributes(settingsFile, FileAttributes.ReadOnly);
        try
        {
            characters.SelectedItem = second;
            (await Dialog(window)).Close();
            await Settle();
        }
        finally { File.SetAttributes(settingsFile, FileAttributes.Normal); }

        Assert.Equal(first, characters.SelectedItem);
        Assert.Equal(first.Manifest.Id, runtime.Settings.SelectedCharacterId);
    }

    [AvaloniaFact]
    public async Task LatestCharacterSelectionWinsThePreview()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        var blank = await AddCharacter(runtime, "Blank", opaque: false);
        var filled = await AddCharacter(runtime, "Filled", opaque: true);
        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();

        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = filled.Manifest.Id });
        await Settle();
        Assert.True(Preview(window).OpaqueAt(Center));

        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = blank.Manifest.Id });
        await Settle();
        Assert.False(Preview(window).OpaqueAt(Center));

        Assert.Equal(blank.Manifest.Id, runtime.Settings.SelectedCharacterId);
    }

    [AvaloniaFact]
    public async Task PreviewRecoversAfterALoadFailureOnceTheCharacterIsReloaded()
    {
        using var fixture = new Fixture(); var runtime = fixture.Runtime;
        var character = await AddGifCharacter(runtime, "Rex");
        // ShowPet stays off for this selection: AppRuntime.UpdatePet() would otherwise
        // load and cache the same "idle" clip for the pet window right here, while the
        // GIF is still good, and that cache entry would hide the corruption below from
        // SettingsWindow's own load.
        await runtime.UpdateSettings(runtime.Settings with { ShowPet = false, SelectedCharacterId = character.Manifest.Id });

        var gif = Path.Combine(character.DirectoryPath, "idle.gif");
        File.WriteAllBytes(gif, [0, 1, 2, 3]); // corrupt: the first load must fail

        var window = new SettingsWindow(runtime); window.Show(); Dispatcher.UIThread.RunJobs();
        await Settle();
        Assert.False(AnyOpaque(Preview(window)));

        File.WriteAllBytes(gif, SampleGif); // repaired on disk
        // The real recovery path: Reload() constructs a fresh CharacterPackage instance
        // (distinct from the one the failed load latched onto) and clears AppRuntime's
        // own clip cache, so the new instance's first load is a genuine, uncached attempt.
        await runtime.Reload();
        await Settle();

        Assert.True(AnyOpaque(Preview(window)));
    }
}
