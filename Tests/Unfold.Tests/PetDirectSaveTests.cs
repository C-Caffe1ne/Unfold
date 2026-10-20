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

[Collection("Timer settings")]
public class PetDirectSaveTests
{
    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Press(Control root, string name) =>
        Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static string? PackVersion(CharacterPackage package)
    {
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(package.DirectoryPath, "pack.json")));
        return metadata.RootElement.GetProperty("contentVersion").GetString();
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++)
        { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ANewDraftCanBeSavedAgainWithTheSameIdAndAnIncrementedVersion(bool still)
    {
        using var data = new DataScope();
        var library = new CharacterLibrary(Path.Combine(data.Root, "library"));
        var draft = new CustomPetDraft();
        var clip = still ? ImportedPetClip.FromImage("photo.jpg", StaticPetMediaTests.Image(".jpg"))
            : ImportedPetClip.FromGif("motion.gif", File.ReadAllBytes(CustomPetDraftTests.Fixture()));
        draft.SetClip("idle", clip);
        var original = draft.Save(library, "새 친구");
        var saved = draft.Save(library, "다시 저장");
        Assert.Equal(original.Manifest.Id, saved.Manifest.Id); Assert.Single(library.List());
        Assert.Equal("다시 저장", saved.Manifest.Name);
        Assert.Equal("1.0.1", PackVersion(saved));
    }

    [AvaloniaTheory]
    [InlineData(".gif")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".webp")]
    [InlineData(".bmp")]
    public async Task NewMediaSavesAppliesAndEditsTheSamePetWithoutAnExportDialog(string extension)
    {
        using var data = new DataScope();
        var source = Path.Combine(data.Root, "source" + extension);
        var bytes = extension == ".gif" ? File.ReadAllBytes(CustomPetDraftTests.Fixture()) : StaticPetMediaTests.Image(extension);
        File.WriteAllBytes(source, bytes);
        var expected = (await PetMediaImporter.Import(source)).LoadFrames();
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        await runtime.Start(true, true); runtime.Stop();
        var owner = new Window { Width = 480, Height = 560 };
        var outputRequests = 0; var applications = 0;
        using var page = new PetManagementView(owner, runtime.Library, async package =>
        {
            applications++; await runtime.SelectInstalledCharacter(package);
        }, chooseMedia: () => Task.FromResult<string?>(source), chooseOutput: () =>
        {
            outputRequests++; return Task.FromResult<string?>(null);
        });
        owner.Content = Ui.PageFrame(owner, page); owner.Show(); PetLibraryTestNavigation.OpenBuilder(owner);
        try
        {
            var save = Find<Button>(owner, "SaveCustomPet");
            Assert.True(save.IsEffectivelyVisible); Assert.False(save.IsEnabled);
            Find<TextBox>(owner, "CustomPetName").Text = "새 친구";
            Assert.False(save.IsEnabled);
            Press(owner, "OpenPetBuilderFile"); await Until(() => !page.IsBusy);
            Find<ComboBox>(owner, "CustomPetPlayback_idle").SelectedIndex = 2;
            Assert.True(save.IsEnabled);
            Assert.False(Find<Button>(owner, "RemoveCustomPet").IsEffectivelyVisible);

            Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
            var saved = Assert.Single(runtime.Library.List()); var id = saved.Manifest.Id;
            Assert.Equal("새 친구", saved.Manifest.Name); Assert.True(saved.Manifest.Animations["idle"].PingPong);
            Assert.Equal(id, runtime.Settings.SelectedCharacterId); Assert.Equal(id, runtime.Selected!.Manifest.Id);
            Assert.Equal(id, AppSettings.Load(Path.Combine(data.Root, "settings.json")).SelectedCharacterId);
            var actual = saved.LoadAnimation("idle"); Assert.Equal(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++)
            { Assert.Equal(expected[i].Image.Pixels, actual[i].Image.Pixels); Assert.Equal(expected[i].Duration, actual[i].Duration); }
            Assert.False(page.HasUnsavedDraft); Assert.False(save.IsEnabled);
            Assert.True(Find<Button>(owner, "RemoveCustomPet").IsEffectivelyVisible);
            var pets = Find<ComboBox>(owner, "MediaExistingPets");
            Assert.Equal(id, Assert.IsType<CharacterPackage>(pets.SelectedItem).Manifest.Id);
            Assert.Single(pets.Items.OfType<CharacterPackage>());
            Assert.Equal(1, applications); Assert.Equal(0, outputRequests); Assert.Empty(owner.OwnedWindows);
            Dispatcher.UIThread.RunJobs(); owner.UpdateLayout();
            Assert.InRange(save.TranslatePoint(default, owner)!.Value.X, 0, owner.ClientSize.Width - save.Bounds.Width);

            Find<TextBox>(owner, "CustomPetName").Text = "이름 수정";
            await Until(() => save.IsEnabled); Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
            saved = Assert.Single(runtime.Library.List());
            Assert.Equal(id, saved.Manifest.Id); Assert.Equal("이름 수정", saved.Manifest.Name);
            Assert.Equal("1.0.1", PackVersion(saved));
            Assert.False(page.HasUnsavedDraft); Assert.Equal(2, applications); Assert.Equal(0, outputRequests);
            Assert.Equal(bytes, File.ReadAllBytes(source));
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task FailedApplicationKeepsTheInstalledDraftAvailableForRetry()
    {
        using var data = new DataScope();
        var library = new CharacterLibrary(Path.Combine(data.Root, "library"));
        var owner = new Window(); var applications = 0;
        using var page = new PetManagementView(owner, library, _ =>
        {
            if (++applications == 1) throw new IOException("Apply failed");
            return Task.CompletedTask;
        }, chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()));
        owner.Content = page; owner.Show(); PetLibraryTestNavigation.OpenBuilder(owner);
        try
        {
            Find<TextBox>(owner, "CustomPetName").Text = "재시도";
            Press(owner, "OpenPetBuilderFile"); await Until(() => !page.IsBusy);
            Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
            var saved = Assert.Single(library.List());
            Assert.True(page.HasUnsavedDraft); Assert.True(Find<Button>(owner, "SaveCustomPet").IsEnabled);
            Assert.Equal(DesignSystem.Error, Find<TextBlock>(owner, "CustomPetStatus").Foreground);
            Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
            Assert.Equal(saved.Manifest.Id, Assert.Single(library.List()).Manifest.Id);
            Assert.Equal(2, applications); Assert.False(page.HasUnsavedDraft);
            Assert.False(Find<Button>(owner, "SaveCustomPet").IsEnabled);
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task FailedNewInstallationPreservesTheDraftAndAllowsRetry()
    {
        using var data = new DataScope();
        var library = new CharacterLibrary(Path.Combine(data.Root, "library"));
        var owner = new Window(); var applications = 0;
        using var page = new PetManagementView(owner, library, _ => { applications++; return Task.CompletedTask; },
            chooseMedia: () => Task.FromResult<string?>(CustomPetDraftTests.Fixture()));
        owner.Content = page; owner.Show(); PetLibraryTestNavigation.OpenBuilder(owner);
        try
        {
            Find<TextBox>(owner, "CustomPetName").Text = "작성 중";
            Press(owner, "OpenPetBuilderFile"); await Until(() => !page.IsBusy);
            using (var locked = new FileStream(Path.Combine(library.Root, ".library.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
                Assert.Equal(0, applications); Assert.True(page.HasUnsavedDraft);
                Assert.Equal("작성 중", Find<TextBox>(owner, "CustomPetName").Text);
                Assert.True(Find<Button>(owner, "SaveCustomPet").IsEnabled);
            }
            Assert.Empty(library.List());
            Press(owner, "SaveCustomPet"); await Until(() => !page.IsBusy);
            Assert.Equal("작성 중", Assert.Single(library.List()).Manifest.Name);
            Assert.Equal(1, applications); Assert.False(page.HasUnsavedDraft);
        }
        finally { owner.Close(); }
    }

    [Theory]
    [InlineData("모찌", "모찌.unfoldpet")]
    [InlineData("새 친구 2", "새 친구 2.unfoldpet")]
    [InlineData("  My pet  ", "My pet.unfoldpet")]
    [InlineData("이름.unfoldpet", "이름.unfoldpet")]
    [InlineData("Pet.UNFOLDPET", "Pet.UNFOLDPET")]
    [InlineData("새/펫\\이름:작은*친구?", "새_펫_이름_작은_친구_.unfoldpet")]
    [InlineData("친구... ", "친구.unfoldpet")]
    [InlineData("", "custom-pet.unfoldpet")]
    [InlineData("..", "custom-pet.unfoldpet")]
    [InlineData("CON", "_CON.unfoldpet")]
    [InlineData("nul.unfoldpet", "_nul.unfoldpet")]
    [InlineData("COM1", "_COM1.unfoldpet")]
    public void PackFileNamesPreserveNamesAndRemainSingleSafeFileNames(string petName, string expected)
        => Assert.Equal(expected, CustomPetView.PackFileName(petName));

    private sealed class DataScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temp.Path;
        public DataScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
