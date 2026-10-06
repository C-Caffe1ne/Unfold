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
public class GlbFileManagementTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; } = new() { Width = 1000, Height = 780 };
        public CharacterLibrary Library { get; }
        public PetManagementView Page { get; }
        public string Source { get; }
        public string? Chosen { get; set; }
        public Editor()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Source = Path.Combine(temp.Path, "first.glb"); File.WriteAllBytes(Source, GlbTests.Fixture(morph: true)); Chosen = Source;
            Library = new(Path.Combine(temp.Path, "library"));
            Page = new(Window, Library, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(Chosen), showPageHeaders: false);
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout(Window);
        }
        public async Task Open()
        {
            Press(Window, "OpenPetBuilderFile"); await Until(() => !Page.IsBusy); Layout(Window);
            await Until(() => !Find<GlbPetView>(Window).IsPreviewLoading);
        }
        public void Dispose() { Page.Dispose(); Window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
    private static T Find<T>(Window window, string? name = null) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => name is null || c.Name == name);
    private static void Press(Window window, string name) => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private static async Task<Window> Confirmation(Window owner)
    { await Until(() => owner.OwnedWindows.Any()); return owner.OwnedWindows.Single(); }
    private static void Choose(Window dialog, string text) => dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, text)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelOrCloseRemovalKeepsTheDraftAndOriginal(bool close)
    {
        using var editor = new Editor(); await editor.Open();
        Press(editor.Window, "SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        Find<TextBox>(editor.Window, "GlbPetName").Text = "작성 중인 펫";
        var original = File.ReadAllBytes(editor.Source);
        Press(editor.Window, "RemoveGlbPet"); var dialog = await Confirmation(editor.Window);
        Assert.True(editor.Page.IsBusy);
        Assert.False(Find<Button>(editor.Window, "OpenPetBuilderFile").IsEnabled);
        if (close) dialog.Close(); else Choose(dialog, "취소");
        await Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Page.HasUnsavedDraft); Assert.True(Find<GlbPetView>(editor.Window).HasDraft);
        Assert.Equal("작성 중인 펫", Find<TextBox>(editor.Window, "GlbPetName").Text);
        Assert.True(Find<Button>(editor.Window, "SaveGlbPet").IsEnabled);
        Assert.Equal(original, File.ReadAllBytes(editor.Source)); Assert.Single(editor.Library.List());
    }

    [AvaloniaFact]
    public async Task RemovingAnUnsavedFileClearsPlaybackAndCanAddAnotherFile()
    {
        using var editor = new Editor(); await editor.Open(); var original = File.ReadAllBytes(editor.Source);
        // A pending pose request must not restore the file after pressing its trash button.
        Find<ComboBox>(editor.Window, "GlbHeading_idle").SelectedIndex = 1;
        Press(editor.Window, "GlbFileRemove"); Assert.Empty(editor.Window.OwnedWindows); Layout(editor.Window);
        var glb = Find<GlbPetView>(editor.Window); var preview = Find<AnimationView>(editor.Window, "GlbPreview");
        Assert.False(glb.HasDraft); Assert.False(editor.Page.HasUnsavedDraft);
        Assert.Equal("first", Find<TextBox>(editor.Window, "GlbPetName").Text);
        Assert.Empty(Find<StackPanel>(editor.Window, "GlbMappings").Children);
        Assert.Equal(0, preview.RenderedPixelSize); Assert.False(preview.OpaqueAt(new(120, 120)));
        Assert.False(Find<Button>(editor.Window, "SaveGlbPet").IsEnabled);
        Assert.False(Find<Button>(editor.Window, "RemoveGlbPet").IsEnabled);
        Assert.True(Find<Button>(editor.Window, "OpenPetBuilderFile").IsEnabled);
        Assert.Equal(original, File.ReadAllBytes(editor.Source));
        var second = Path.Combine(Path.GetDirectoryName(editor.Source)!, "second.glb"); File.WriteAllBytes(second, GlbTests.Fixture());
        editor.Chosen = second; Find<TextBox>(editor.Window, "GlbPetName").Text = "second"; await editor.Open();
        Assert.Equal("second", Find<TextBox>(editor.Window, "GlbPetName").Text); Assert.True(glb.HasDraft);
        Press(editor.Window, "SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        Assert.Equal("second", Assert.Single(editor.Library.List()).Manifest.Name);
    }

    [AvaloniaFact]
    public async Task AddingTwoModelsAndDeletingOneRefreshesBothPickersAndPreservesTheOther()
    {
        using var editor = new Editor(); await editor.Open();
        Press(editor.Window, "SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        var first = Assert.Single(editor.Library.List());
        var secondPath = Path.Combine(Path.GetDirectoryName(editor.Source)!, "second.glb"); File.WriteAllBytes(secondPath, GlbTests.Fixture());
        editor.Chosen = secondPath; await editor.Open();
        Press(editor.Window, "SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        var second = editor.Library.List().Single(p => p.Manifest.Id != first.Manifest.Id);
        var choice = Find<ComboBox>(editor.Window, "GlbExistingPets");
        choice.SelectedItem = choice.Items.OfType<CharacterPackage>().Single(p => p.Manifest.Id == first.Manifest.Id);
        await Until(() => !editor.Page.IsBusy && !Find<GlbPetView>(editor.Window).IsPreviewLoading);
        var remove = Find<Button>(editor.Window, "RemoveGlbPet");
        Assert.Equal("펫 삭제", remove.Content); Assert.Contains("danger", remove.Classes);
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); var dialog = await Confirmation(editor.Window);
        var delete = dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "펫 삭제"));
        Assert.Contains("danger", delete.Classes); Assert.False(delete.IsDefault);
        Assert.True(dialog.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "취소")).IsFocused);
        Choose(dialog, "펫 삭제"); await Until(() => !editor.Page.IsBusy);
        Assert.False(Directory.Exists(first.DirectoryPath)); Assert.True(Directory.Exists(second.DirectoryPath));
        Assert.True(File.Exists(editor.Source)); Assert.True(File.Exists(secondPath));
        Assert.Equal(second.Manifest.Id, Assert.Single(choice.Items.OfType<CharacterPackage>()).Manifest.Id);
        editor.Chosen = CustomPetDraftTests.Fixture(); Press(editor.Window, "OpenPetBuilderFile"); await Until(() => !editor.Page.IsBusy); Layout(editor.Window);
        Assert.Equal(second.Manifest.Id, Assert.Single(Find<ComboBox>(editor.Window, "MediaExistingPets").Items.OfType<CharacterPackage>()).Manifest.Id);
    }

    [AvaloniaFact]
    public async Task FailedDeletionKeepsTheSavedDraftAndCanRetry()
    {
        using var editor = new Editor(); await editor.Open();
        Press(editor.Window, "SaveGlbPet"); await Until(() => !editor.Page.IsBusy); var saved = Assert.Single(editor.Library.List());
        using (var locked = new FileStream(Path.Combine(editor.Library.Root, ".library.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Press(editor.Window, "RemoveGlbPet"); Choose(await Confirmation(editor.Window), "펫 삭제"); await Until(() => !editor.Page.IsBusy);
            Assert.True(Find<GlbPetView>(editor.Window).HasDraft); Assert.True(Directory.Exists(saved.DirectoryPath));
            Assert.True(Find<TextBlock>(editor.Window, "GlbStatus").IsVisible);
            Assert.True(Find<Button>(editor.Window, "RemoveGlbPet").IsEnabled);
        }
        Press(editor.Window, "RemoveGlbPet"); Choose(await Confirmation(editor.Window), "펫 삭제"); await Until(() => !editor.Page.IsBusy);
        Assert.Empty(editor.Library.List()); Assert.False(Find<GlbPetView>(editor.Window).HasDraft);
    }

    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public async Task DeletingFromSettingsPreservesAValidActivePetAndSelection(bool active)
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        try
        {
            var source = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(source, GlbTests.Fixture(morph: true));
            var saved = new GlbPetDraft(source).Save(runtime.Library);
            var selected = active ? saved : new GlbPetDraft(source) { Name = "남길 펫" }.Save(runtime.Library);
            await runtime.Start(true, true); runtime.Stop(); await runtime.SelectInstalledCharacter(selected); runtime.ShowSettings();
            var window = (SettingsWindow)lifetime.MainWindow!; Press(window, "SettingsNavPacks");
            await Until(() => window.GetVisualDescendants().OfType<PetManagementView>().Any()); Layout(window);
            var choices = Find<ComboBox>(window, "MediaExistingPets");
            choices.SelectedItem = choices.Items.OfType<CharacterPackage>().Single(p => p.Manifest.Id == saved.Manifest.Id);
            var page = Find<PetManagementView>(window); await Until(() => !page.IsBusy && window.GetVisualDescendants().OfType<GlbPetView>().Any());
            // Removing the editor file must never switch or delete the active pet.
            Press(window, "GlbFileRemove"); Layout(window);
            Assert.Empty(window.OwnedWindows); Assert.Equal(selected.Manifest.Id, runtime.Settings.SelectedCharacterId);
            Assert.Contains(runtime.Library.List(), p => p.Manifest.Id == saved.Manifest.Id);
            var glbChoices = Find<ComboBox>(window, "GlbExistingPets");
            glbChoices.SelectedItem = glbChoices.Items.OfType<CharacterPackage>().Single(p => p.Manifest.Id == saved.Manifest.Id);
            await Until(() => !page.IsBusy && Find<GlbPetView>(window).HasDraft);
            Press(window, "RemoveGlbPet"); Choose(await Confirmation(window), "펫 삭제"); await Until(() => !page.IsBusy);
            Assert.Equal(active ? 0 : 1, runtime.Library.List().Count);
            if (active) Assert.True(runtime.Selected!.IsBuiltIn); else Assert.Equal(selected.Manifest.Id, runtime.Selected!.Manifest.Id);
            Assert.Equal(runtime.Selected.Manifest.Id, runtime.Settings.SelectedCharacterId);
            Assert.DoesNotContain(runtime.Characters, p => p.Manifest.Id == saved.Manifest.Id);
            Assert.Equal(runtime.Settings.SelectedCharacterId, AppSettings.Load(Path.Combine(temp.Path, "settings.json")).SelectedCharacterId);
            Assert.NotNull(runtime.ActivePet); Assert.True(File.Exists(source));
        }
        finally { runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaTheory]
    [InlineData(480, 560)] [InlineData(860, 680)] [InlineData(1120, 800)]
    public async Task RemovalFitsTheExistingLayout(double width, double height)
    {
        using var editor = new Editor(); await editor.Open(); editor.Window.Width = width; editor.Window.Height = height; Layout(editor.Window);
        var remove = Find<Button>(editor.Window, "GlbFileRemove"); var add = Find<Button>(editor.Window, "GlbFileAdd");
        Assert.True(remove.IsEffectivelyVisible); Assert.True(remove.IsEnabled);
        var scroll = Find<ScrollViewer>(editor.Window, "PageBodyScroll"); Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        Assert.Equal(add.TranslatePoint(default, editor.Window)!.Value.Y, remove.TranslatePoint(default, editor.Window)!.Value.Y);
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name is "GlbInfo" or "GlbActionHint" or "GlbMappingSummary");
    }
}
