using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetBuilderTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; } = new() { Width = 900, Height = 780 };
        public PetManagementView Page { get; }
        public CharacterLibrary Library { get; }
        public string FilePath { get; set; }
        public string? Output { get; set; }
        public string GlbPath { get; }
        public Editor()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            GlbPath = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(GlbPath, GlbTests.Fixture(morph: true));
            FilePath = GlbPath; Output = Path.Combine(temp.Path, "pet.unfoldpet");
            Library = new(Path.Combine(temp.Path, "library"));
            Page = new(Window, Library, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(FilePath),
                chooseOutput: () => Task.FromResult<string?>(Output), showPageHeaders: false);
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public async Task Open(string path)
        { FilePath = path; Press("OpenPetBuilderFile"); await Until(() => !Page.IsBusy); Layout(); }
        public void Dispose() { Page.Dispose(); Window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    [AvaloniaFact]
    public async Task OneBuilderRoutesByExtensionAndKeepsDraftsAcrossNavigation()
    {
        using var editor = new Editor();
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<TabControl>(), c => c.Name == "PetManagementTabs");
        Assert.NotNull(editor.Find<TextBox>("CustomPetName"));
        await editor.Open(CustomPetDraftTests.Fixture());
        editor.Find<TextBox>("CustomPetName").Text = "영상 펫";
        editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
        var mediaAction = editor.Find<ComboBox>("CustomPetSelectedAction");
        await editor.Open(editor.GlbPath);
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<Control>(), c => c.Name == "PetBuilderFormat");
        editor.Find<TextBox>("GlbPetName").Text = "모델 펫";
        editor.Find<ComboBox>("GlbHeading_idle").SelectedIndex = 1;
        editor.Page.IsVisible = false; editor.Layout(); editor.Page.IsVisible = true; editor.Layout();
        Assert.Equal("모델 펫", editor.Find<TextBox>("GlbPetName").Text);
        Assert.Equal(1, editor.Find<ComboBox>("GlbHeading_idle").SelectedIndex);
        // Opening another media action restores that format's existing name and idle mapping.
        mediaAction.SelectedItem = "click";
        await editor.Open(CustomPetDraftTests.Fixture());
        Assert.Equal("영상 펫", editor.Find<TextBox>("CustomPetName").Text);
        Assert.Equal(2, editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex);
        Assert.True(editor.Find<Button>("CreateCustomPetPack").IsEnabled);
        Assert.True(editor.Page.HasUnsavedDraft);
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<TextBlock>(), t =>
            t.Name is "GlbActionHint" or "GlbPreviewStatus" or "GlbPreviewClip" or "GlbInfo" or "GlbMappingSummary" ||
            t.Name?.StartsWith("GlbRepeatHint_", StringComparison.Ordinal) == true);
    }
    [AvaloniaTheory]
    [InlineData("pet-motion.gif")]
    [InlineData("pet-motion.mp4")]
    public async Task MediaImportsThroughUnifiedPickerAndExportsItsPlayback(string fixture)
    {
        if (fixture.EndsWith(".mp4", StringComparison.Ordinal)) Assert.SkipUnless(PetMediaImporterTests.HasVideoTool, "Prepare media tools for MP4 conversion.");
        using var editor = new Editor(); await editor.Open(CustomPetDraftTests.Fixture(fixture));
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<Control>(), c => c.Name == "PetBuilderFormat");
        Assert.Equal(fixture, editor.Find<TextBlock>("CustomPetLabel_idle").Text);
        editor.Find<TextBox>("CustomPetName").Text = "테스트 펫";
        editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
        editor.Press("CreateCustomPetPack");
        await Until(() => editor.Window.OwnedWindows.OfType<PetPackWindow>().Any());
        var import = editor.Window.OwnedWindows.OfType<PetPackWindow>().Single();
        await Until(() => !import.IsBusy); import.Close();
        await Until(() => !editor.Page.IsBusy); editor.Layout();
        Assert.NotNull(editor.Find<TextBox>("CustomPetName"));
        using var pack = CharacterPack.Open(editor.Output!);
        Assert.False(pack.Character.IsGlb); Assert.True(pack.Character.Manifest.Animations["idle"].PingPong);
        Assert.Empty(editor.Library.List());
    }
    [AvaloniaFact]
    public async Task GlbExportCanBeCancelledAndThenOpenedAndInstalledWithoutChangingItsSettings()
    {
        using var editor = new Editor(); await editor.Open(editor.GlbPath);
        editor.Find<TextBox>("GlbPetName").Text = "내 GLB 펫";
        editor.Find<ComboBox>("GlbHeading_idle").SelectedIndex = 2;
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
        editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex = 1;
        var output = editor.Output; editor.Output = null;
        editor.Press("CreateGlbPetPack"); await Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Page.HasUnsavedDraft); Assert.Empty(editor.Library.List());
        editor.Output = output;
        editor.Press("CreateGlbPetPack");
        await Until(() => editor.Window.OwnedWindows.OfType<PetPackWindow>().Any());
        var import = editor.Window.OwnedWindows.OfType<PetPackWindow>().Single();
        await Until(() => !import.IsBusy); editor.Layout();
        Assert.False(editor.Page.HasUnsavedDraft); Assert.Empty(editor.Library.List());
        using (var pack = CharacterPack.Open(output!))
        {
            Assert.True(pack.Character.IsGlb); Assert.Equal(180, pack.Character.Manifest.Animations["idle"].Heading);
            Assert.Equal(0, pack.Character.Manifest.Animations["click"].Heading);
            Assert.Equal("내 GLB 펫", pack.Character.Manifest.Name);
            Assert.True(pack.Character.Manifest.Animations["click"].Loop);
            Assert.Equal(.5, pack.Character.Manifest.Animations["click"].Speed);
        }
        var install = import.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "InstallPetPack");
        await Until(() => install.IsEnabled);
        install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Library.List().Single().IsGlb); editor.Layout();
        Assert.Equal(1, editor.Find<ComboBox>("GlbExistingPets").ItemCount);
    }
    [AvaloniaFact]
    public async Task SavedGlbCanBeSelectedBelowPreviewFromTheDefaultScreen()
    {
        using var editor = new Editor(); var saved = new GlbPetDraft(editor.GlbPath).Save(editor.Library);
        editor.Window.GetVisualDescendants().OfType<PetBuilderView>().Single().RefreshPets();
        var choice = editor.Find<ComboBox>("MediaExistingPets");
        Assert.Equal(saved.Manifest.Id, Assert.Single(choice.Items.OfType<CharacterPackage>()).Manifest.Id);
        choice.SelectedIndex = 0;
        await Until(() => !editor.Page.IsBusy && editor.Window.GetVisualDescendants().OfType<TextBox>().Any(c => c.Name == "GlbPetName"));
        Assert.Equal(saved.Manifest.Name, editor.Find<TextBox>("GlbPetName").Text);
        Assert.False(editor.Page.HasUnsavedDraft);
    }
    [AvaloniaFact]
    public async Task UnifiedPickerAppliesAnOlderPackWithoutDiscardingTheDraft()
    {
        using var editor = new Editor();
        var root = Path.GetDirectoryName(editor.GlbPath)!;
        using var newer = CharacterPack.Open(CharacterPackTests.CreatePack(root, "2.0.0"));
        editor.Library.Install(newer, null);
        editor.Find<TextBox>("CustomPetName").Text = "작성 중";
        editor.FilePath = CharacterPackTests.CreatePack(root, "1.0.0", color: 0xFFABCDEF);
        editor.Press("OpenPetBuilderFile");
        await Until(() => editor.Window.OwnedWindows.OfType<PetPackWindow>().Any());
        var import = editor.Window.OwnedWindows.OfType<PetPackWindow>().Single();
        var install = import.GetVisualDescendants().OfType<Button>().Single(c => c.Name == "InstallPetPack");
        await Until(() => install.IsEnabled);
        install.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(() => !editor.Page.IsBusy); editor.Layout();
        using var older = CharacterPack.Open(editor.FilePath);
        Assert.Equal("1.0.0", editor.Library.InspectInstall(older).InstalledVersion);
        Assert.Equal(older.Character.LoadAnimation("idle")[0].Image.Pixels, Assert.Single(editor.Library.List()).LoadAnimation("idle")[0].Image.Pixels);
        Assert.Equal("작성 중", editor.Find<TextBox>("CustomPetName").Text);
        Assert.Empty(editor.Window.OwnedWindows);
    }
    [AvaloniaTheory]
    [InlineData(480, 560)]
    [InlineData(860, 680)]
    [InlineData(1120, 800)]
    public async Task BothEditorsFitAndExposeOnlyTheSelectedAction(double width, double height)
    {
        using var editor = new Editor(); await editor.Open(CustomPetDraftTests.Fixture());
        editor.Window.Width = width; editor.Window.Height = height;
        for (var format = 0; format < 2; format++)
        {
            if (format == 1) await editor.Open(editor.GlbPath);
            editor.Layout();
            var name = editor.Find<TextBox>(format == 0 ? "CustomPetName" : "GlbPetName");
            var open = editor.Find<Button>("OpenPetBuilderFile");
            var nameAt = name.TranslatePoint(default, editor.Window)!.Value;
            var openAt = open.TranslatePoint(default, editor.Window)!.Value;
            Assert.True(openAt.X >= nameAt.X + name.Bounds.Width);
            Assert.InRange(Math.Abs(openAt.Y - nameAt.Y), 0, 1);
            var previewPane = editor.Find<Grid>(format == 0 ? "CustomPetPreviewPane" : "GlbPreviewPane");
            Assert.InRange(Math.Abs(name.Bounds.Width - previewPane.Bounds.Width / 2), 0, 1);
            var settings = editor.Find<StackPanel>(format == 0 ? "CustomPetActionPane" : "GlbActionPane");
            if (width >= 860)
            {
                var stage = editor.Find<Border>(format == 0 ? "CustomPetPreviewSurface" : "GlbPreviewStage");
                var title = previewPane.Children.OfType<TextBlock>().Single();
                Assert.InRange(Math.Abs(stage.Bounds.Height - Math.Max(140, settings.DesiredSize.Height - title.DesiredSize.Height - 8)), 0, 1);
            }
            var pets = editor.Find<ComboBox>(format == 0 ? "MediaExistingPets" : "GlbExistingPets");
            var pause = editor.Find<Button>(format == 0 ? "PauseCustomPet" : "PauseGlbPet");
            Assert.True(pets.TranslatePoint(default, editor.Window)!.Value.Y >= pause.TranslatePoint(default, editor.Window)!.Value.Y + pause.Bounds.Height);
            var selector = editor.Find<ComboBox>(format == 0 ? "CustomPetSelectedAction" : "GlbPreviewAction");
            selector.SelectedItem = "click"; editor.Layout();
            var scroll = editor.Find<ScrollViewer>("PageBodyScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            var rows = editor.Find<StackPanel>(format == 0 ? "CustomPetActionSlots" : "GlbMappings");
            Assert.EndsWith("click", Assert.Single(rows.Children, c => c.IsVisible).Name);
            var save = editor.Find<Button>(format == 0 ? "CreateCustomPetPack" : "CreateGlbPetPack");
            var origin = save.TranslatePoint(default, editor.Window)!.Value;
            Assert.InRange(origin.Y + save.Bounds.Height, 0, editor.Window.ClientSize.Height);
        }
    }
}
