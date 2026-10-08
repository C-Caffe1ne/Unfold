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

public class PetEditingCoreTests
{
    internal static CharacterPackage InstallMedia(CharacterLibrary library, string root, bool still = false)
    {
        var draft = new CustomPetDraft();
        var clip = still ? ImportedPetClip.FromImage("photo.png", ImageCodec.EncodePng(new(16, 16,
            Enumerable.Range(0, 256).Select(index => 0xFF000000u | (uint)(index * 65537)).ToArray())))
            : ImportedPetClip.FromGif("source.gif", File.ReadAllBytes(CustomPetDraftTests.Fixture()));
        draft.SetClip("idle", clip); draft.SetClip("click", clip); draft.SetPlayback("click", 2);
        var path = Path.Combine(root, $"{Guid.NewGuid():N}.unfoldpet"); draft.Export("내 펫", path);
        using var pack = CharacterPack.Open(path); return library.Install(pack, null);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MediaEditsPreserveIdentityPixelsTimingPlaybackAndIncrementVersion(bool still)
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var original = InstallMedia(library, temp.Path, still); var frames = original.LoadAnimation("click");
        var source = File.ReadAllBytes(CharacterLibrary.AssetPath(original.DirectoryPath, original.Manifest.SpriteSheet.File));
        var draft = new CustomPetDraft(original); Assert.Equal(2, draft.Playback("click"));
        draft.SetPlayback("idle", 2); var edited = draft.Save(library, "새 이름");
        Assert.Equal(original.Manifest.Id, edited.Manifest.Id); Assert.Equal("새 이름", edited.Manifest.Name);
        Assert.True(edited.Manifest.Animations["idle"].PingPong); Assert.True(edited.Manifest.Animations["click"].PingPong);
        Assert.Equal(source, File.ReadAllBytes(CharacterLibrary.AssetPath(edited.DirectoryPath, edited.Manifest.SpriteSheet.File)));
        var actual = edited.LoadAnimation("click"); Assert.Equal(frames.Count, actual.Count);
        for (var i = 0; i < frames.Count; i++) { Assert.Equal(frames[i].Duration, actual[i].Duration); Assert.Equal(frames[i].Image.Pixels, actual[i].Image.Pixels); }
        Assert.Equal("1.0.1", JsonDocument.Parse(File.ReadAllText(Path.Combine(edited.DirectoryPath, "pack.json"))).RootElement.GetProperty("contentVersion").GetString());
        draft.Save(library, "다시 저장");
        Assert.Equal("1.0.2", JsonDocument.Parse(File.ReadAllText(Path.Combine(edited.DirectoryPath, "pack.json"))).RootElement.GetProperty("contentVersion").GetString());
    }
    [Fact]
    public void SpriteEditsKeepExtraActionsAndOriginalArtworkWithoutGifConversion()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var pack = CharacterPack.Open(CharacterPackTests.CreatePack(temp.Path)); var original = library.Install(pack, null);
        var manifestPath = Path.Combine(original.DirectoryPath, "character.json");
        var manifest = original.Manifest with { Animations = new(original.Manifest.Animations) { ["look"] = new([1, 0, 1], 13.5, Loop: true) } };
        File.WriteAllBytes(manifestPath, JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        original = CharacterLibrary.LoadPackage(original.DirectoryPath); var previous = original.LoadAnimation("look");
        var draft = new CustomPetDraft(original); Assert.Contains("look", draft.AvailableActions);
        draft.SetPlayback("look", 2); var edited = draft.Save(library, "스프라이트");
        Assert.Null(edited.Manifest.Animations["look"].Gif); Assert.Equal(13.5, edited.Manifest.Animations["look"].Fps);
        Assert.True(edited.Manifest.Animations["look"].PingPong);
        Assert.Equal(previous.Select(frame => frame.Duration), edited.LoadAnimation("look").Select(frame => frame.Duration));
        Assert.Equal(previous[0].Image.Pixels, edited.LoadAnimation("look")[0].Image.Pixels);
    }
    [Fact]
    public void ReplacingStaticAndGifActionsPreservesOtherCellsAndUpdatesFallbackImage()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var pack = CharacterPack.Open(CharacterPackTests.CreatePack(temp.Path)); var original = library.Install(pack, null);
        var untouched = original.LoadAnimation("attention"); var draft = new CustomPetDraft(original);
        var pixels = Enumerable.Range(0, 256).Select(index => 0xFF000000u | (uint)(index * 65537)).ToArray();
        draft.SetClip("idle", ImportedPetClip.FromImage("new.png", ImageCodec.EncodePng(new(16, 16, pixels))));
        draft.SetClip("click", ImportedPetClip.FromGif("new.gif", File.ReadAllBytes(CustomPetDraftTests.Fixture())));
        var edited = draft.Save(library, "변경");
        Assert.Equal(pixels, edited.LoadAnimation("idle")[0].Image.Pixels); Assert.Equal(pixels, edited.StillImage.Pixels);
        Assert.Equal(4, edited.LoadAnimation("click").Count);
        for (var i = 0; i < untouched.Count; i++)
        { Assert.Equal(untouched[i].Image.Pixels, edited.LoadAnimation("attention")[i].Image.Pixels); Assert.Equal(untouched[i].Duration, edited.LoadAnimation("attention")[i].Duration); }
        Assert.Equal(pack.Character.LoadAnimation("attention")[0].Image.Pixels, untouched[0].Image.Pixels);
    }
    [Fact]
    public void FailedAndStaleEditsDoNotOverwriteOrRecreatePets()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var original = InstallMedia(library, temp.Path); var revision = CharacterLibrary.PetRevision(original.DirectoryPath);
        var bad = new CustomPetDraft(original); bad.RemoveClip("idle");
        Assert.Throws<InvalidDataException>(() => bad.Save(library, "필수 없음")); Assert.Equal(revision, CharacterLibrary.PetRevision(original.DirectoryPath));
        var stale = new CustomPetDraft(original); new CustomPetDraft(original).Save(library, "다른 수정");
        Assert.Throws<IOException>(() => stale.Save(library, "덮어쓰기"));
        Assert.Throws<IOException>(() => library.Delete(original.Manifest.Id, revision)); Assert.Equal("다른 수정", library.List().Single().Manifest.Name);
        var last = new CustomPetDraft(library.List().Single()); library.Delete(original.Manifest.Id);
        Assert.Throws<IOException>(() => last.Save(library, "삭제 복원")); Assert.Empty(library.List());
    }
    [Fact]
    public void EditingFromAnOldListEntryLoadsTheCurrentManifest()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var listed = InstallMedia(library, temp.Path); new CustomPetDraft(listed).Save(library, "목록 이후 수정");
        var reopened = new CustomPetDraft(listed); Assert.Equal("목록 이후 수정", reopened.OriginalName);
        var path = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
        var model = new GlbPetDraft(path).Save(library); new GlbPetDraft(model) { Name = "최근 3D" }.Save(library);
        Assert.Equal("최근 3D", new GlbPetDraft(model).Name);
    }
    [Fact]
    public void LegacySourceAndBuiltInProtectionRemainIntact()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"), ["default-cat"]);
        var original = library.Save(CodecTests.Fixture()); var path = Path.Combine(original.DirectoryPath, "source.piskel"); var bytes = File.ReadAllBytes(path);
        var edited = new CustomPetDraft(original).Save(library, "이전 펫 편집");
        Assert.Equal(original.Manifest.Id, edited.Manifest.Id); Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.False(library.CanManage("default-cat")); Assert.Throws<InvalidDataException>(() => library.Delete("default-cat"));
        Assert.Throws<InvalidDataException>(() => new CustomPetDraft(CharacterLibrary.LoadPackage(original.DirectoryPath, true)));
    }
    [Fact]
    public void StaleGlbEditsAndDeletionCannotReplaceANewerSavedPet()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var path = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
        var original = new GlbPetDraft(path).Save(library); var stale = new GlbPetDraft(original);
        var newer = new GlbPetDraft(original) { Name = "최신 펫" }; newer.Save(library);
        Assert.Throws<IOException>(() => stale.Save(library)); Assert.Throws<IOException>(() => library.Delete(original.Manifest.Id, stale.Revision));
        Assert.Equal("최신 펫", library.List().Single().Manifest.Name);
    }
}

[Collection("Timer settings")]
public class PetEditingUiTests
{
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Press(Window window, string name) => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Choose(Window dialog, string text) => dialog.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task Until(Func<bool> ready)
    { for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); } Assert.True(ready()); }
    private static async Task<Window> Dialog(Window window) { await Until(() => window.OwnedWindows.Any()); return window.OwnedWindows.Single(); }
    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task InstalledMediaCanBeEditedSavedAndDeletedFromTheUnifiedPicker(bool still)
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var saved = PetEditingCoreTests.InstallMedia(library, temp.Path, still); CharacterPackage? applied = null; string? removed = null;
        var window = new Window { Width = 640, Height = 560 };
        using var page = new PetManagementView(window, library, package => { applied = package; return Task.CompletedTask; }, removed: id => { removed = id; return Task.CompletedTask; });
        try
        {
            window.Content = Ui.PageFrame(window, page); window.Show(); Dispatcher.UIThread.RunJobs();
            var pets = Find<ComboBox>(window, "MediaExistingPets"); Assert.Equal(saved.Manifest.Id, pets.Items.OfType<CharacterPackage>().Single().Manifest.Id);
            pets.SelectedIndex = 0; await Until(() => !page.IsBusy && Find<TextBox>(window, "CustomPetName").Text == saved.Manifest.Name);
            Assert.False(page.HasUnsavedDraft); Assert.Equal(2, Find<ComboBox>(window, "CustomPetPlayback_click").SelectedIndex);
            Assert.True(Find<Button>(window, "RemoveCustomPet").IsEffectivelyVisible); Assert.False(Find<Button>(window, "SaveCustomPet").IsEnabled);
            Find<TextBox>(window, "CustomPetName").Text = "바꾼 펫"; Find<ComboBox>(window, "CustomPetPlayback_idle").SelectedIndex = 2;
            Press(window, "SaveCustomPet"); await Until(() => !page.IsBusy);
            Assert.Equal(saved.Manifest.Id, applied!.Manifest.Id); Assert.Equal("바꾼 펫", library.List().Single().Manifest.Name); Assert.False(page.HasUnsavedDraft);
            Assert.True(applied.Manifest.Animations["idle"].PingPong);
            window.UpdateLayout(); var scroll = Find<ScrollViewer>(window, "PageBodyScroll"); Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            var save = Find<Button>(window, "SaveCustomPet"); Assert.True(save.TranslatePoint(default, window)!.Value.Y + save.Bounds.Height <= window.ClientSize.Height);
            Press(window, "RemoveCustomPet"); Choose(await Dialog(window), "취소"); await Until(() => !page.IsBusy); Assert.Single(library.List());
            Press(window, "RemoveCustomPet"); Choose(await Dialog(window), "펫 삭제"); await Until(() => !page.IsBusy);
            Assert.Equal(saved.Manifest.Id, removed); Assert.Empty(library.List()); Assert.Equal(0, pets.ItemCount); Assert.False(page.HasUnsavedDraft);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task BothFormatPickersListAllCustomPetsAndCanSwitchToMedia()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var media = PetEditingCoreTests.InstallMedia(library, temp.Path); var path = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
        var model = new GlbPetDraft(path).Save(library); var window = new Window();
        using var page = new PetManagementView(window, library, _ => Task.CompletedTask);
        try
        {
            window.Content = Ui.PageFrame(window, page); window.Show(); Dispatcher.UIThread.RunJobs();
            var picker = Find<ComboBox>(window, "MediaExistingPets"); Assert.Equal(2, picker.ItemCount);
            picker.SelectedItem = picker.Items.OfType<CharacterPackage>().Single(pet => pet.IsGlb); await Until(() => !page.IsBusy && window.GetVisualDescendants().OfType<GlbPetView>().Any());
            var modelPicker = Find<ComboBox>(window, "GlbExistingPets"); Assert.Equal(2, modelPicker.ItemCount);
            modelPicker.SelectedItem = modelPicker.Items.OfType<CharacterPackage>().Single(pet => pet.Manifest.Id == media.Manifest.Id);
            await Until(() => !page.IsBusy && window.GetVisualDescendants().OfType<CustomPetView>().Any());
            Assert.Equal(media.Manifest.Name, Find<TextBox>(window, "CustomPetName").Text);
            Assert.Equal(media.Manifest.Id, ((CharacterPackage)picker.SelectedItem!).Manifest.Id);
            Assert.Equal(2, library.List().Count); Assert.False(page.HasUnsavedDraft);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task CancelledReplacementAndFailedWritesRetainTheMediaDraftAndAllowRetry()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var first = PetEditingCoreTests.InstallMedia(library, temp.Path); var second = PetEditingCoreTests.InstallMedia(library, temp.Path);
        var window = new Window(); using var page = new PetManagementView(window, library, _ => Task.CompletedTask);
        try
        {
            window.Content = Ui.PageFrame(window, page); window.Show(); Dispatcher.UIThread.RunJobs();
            var pets = Find<ComboBox>(window, "MediaExistingPets");
            pets.SelectedItem = pets.Items.OfType<CharacterPackage>().Single(pet => pet.Manifest.Id == first.Manifest.Id); await Until(() => !page.IsBusy);
            Find<TextBox>(window, "CustomPetName").Text = "작성 중";
            pets.SelectedItem = pets.Items.OfType<CharacterPackage>().Single(pet => pet.Manifest.Id == second.Manifest.Id);
            Choose(await Dialog(window), "계속 편집");
            await Until(() => !window.OwnedWindows.Any() && pets.SelectedItem is CharacterPackage pet && pet.Manifest.Id == first.Manifest.Id);
            Assert.Equal("작성 중", Find<TextBox>(window, "CustomPetName").Text); Assert.True(page.HasUnsavedDraft);
            Assert.Equal(first.Manifest.Id, ((CharacterPackage)pets.SelectedItem!).Manifest.Id);
            using (var locked = new FileStream(Path.Combine(library.Root, ".library.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Press(window, "SaveCustomPet"); await Until(() => !page.IsBusy);
                Assert.True(page.HasUnsavedDraft); Assert.Equal("작성 중", Find<TextBox>(window, "CustomPetName").Text);
                Assert.True(Find<TextBlock>(window, "CustomPetStatus").IsVisible);
                Press(window, "RemoveCustomPet"); Choose(await Dialog(window), "펫 삭제"); await Until(() => !page.IsBusy);
                Assert.True(Directory.Exists(first.DirectoryPath)); Assert.True(page.HasUnsavedDraft);
            }
            Press(window, "SaveCustomPet"); await Until(() => !page.IsBusy); Assert.False(page.HasUnsavedDraft);
            Assert.Equal("작성 중", library.List().Single(pet => pet.Manifest.Id == first.Manifest.Id).Manifest.Name);
            var newer = new CustomPetDraft(library.List().Single(pet => pet.Manifest.Id == first.Manifest.Id)); newer.Save(library, "외부 수정");
            Press(window, "EditCustomPet"); await Until(() => !page.IsBusy);
            Assert.Equal("외부 수정", Find<TextBox>(window, "CustomPetName").Text); Assert.False(page.HasUnsavedDraft);
            Assert.True(Directory.Exists(second.DirectoryPath));
        }
        finally { window.Close(); }
    }
    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public async Task DeletingMediaRefreshesRuntimeAndPreservesActiveSelection(bool active)
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR"); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        try
        {
            var media = PetEditingCoreTests.InstallMedia(runtime.Library, temp.Path); await runtime.Start(true, true); runtime.Stop();
            if (active) await runtime.SelectInstalledCharacter(media);
            var selectedId = runtime.Settings.SelectedCharacterId; runtime.ShowSettings(); var window = (SettingsWindow)lifetime.MainWindow!;
            Press(window, "SettingsNavPacks"); Dispatcher.UIThread.RunJobs();
            Find<ComboBox>(window, "MediaExistingPets").SelectedIndex = 0;
            var page = window.GetVisualDescendants().OfType<PetManagementView>().Single(); await Until(() => !page.IsBusy);
            Press(window, "RemoveCustomPet"); Choose(await Dialog(window), "펫 삭제"); await Until(() => !page.IsBusy);
            Assert.Empty(runtime.Library.List()); Assert.True(runtime.Selected!.IsBuiltIn); Assert.NotNull(runtime.ActivePet);
            if (!active) Assert.Equal(selectedId, runtime.Settings.SelectedCharacterId);
            Assert.Equal(runtime.Selected.Manifest.Id, runtime.Settings.SelectedCharacterId);
            Assert.Equal(runtime.Settings.SelectedCharacterId, AppSettings.Load(Path.Combine(temp.Path, "settings.json")).SelectedCharacterId);
        }
        finally { runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
}
