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
            Find<TabControl>("PetManagementTabs").SelectedIndex = 1; Layout();
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
    public async Task OneBuilderRoutesBothFormatsAndPreservesTheirDraftsAcrossNavigation()
    {
        using var editor = new Editor();
        var tabs = editor.Find<TabControl>("PetManagementTabs");
        Assert.Equal(new[] { "펫 팩 열기", "펫 팩 만들기" }, tabs.Items.OfType<TabItem>().Select(t => t.Header));
        await editor.Open(CustomPetDraftTests.Fixture());
        editor.Find<TextBox>("CustomPetName").Text = "영상 펫";
        editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
        await editor.Open(editor.GlbPath);
        Assert.Equal(1, editor.Find<ComboBox>("PetBuilderFormat").SelectedIndex);
        editor.Find<TextBox>("GlbPetName").Text = "모델 펫";
        editor.Find<Expander>("GlbAdvanced").IsExpanded = true; editor.Layout();
        editor.Find<ComboBox>("GlbHeading").SelectedIndex = 1;
        tabs.SelectedIndex = 0; editor.Layout(); tabs.SelectedIndex = 1; editor.Layout();
        Assert.Equal("모델 펫", editor.Find<TextBox>("GlbPetName").Text);
        editor.Find<ComboBox>("PetBuilderFormat").SelectedIndex = 0; editor.Layout();
        Assert.Equal("영상 펫", editor.Find<TextBox>("CustomPetName").Text);
        Assert.Equal(2, editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex);
        Assert.True(editor.Find<Button>("CreateCustomPetPack").IsEnabled);
        editor.Find<ComboBox>("PetBuilderFormat").SelectedIndex = 1; editor.Layout();
        Assert.Equal(1, editor.Find<ComboBox>("GlbHeading").SelectedIndex);
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
        Assert.Equal(0, editor.Find<ComboBox>("PetBuilderFormat").SelectedIndex);
        Assert.Equal(fixture, editor.Find<TextBlock>("CustomPetLabel_idle").Text);
        editor.Find<TextBox>("CustomPetName").Text = "테스트 펫";
        editor.Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
        editor.Press("CreateCustomPetPack"); await Until(() => !editor.Page.IsBusy); editor.Layout();
        Assert.Equal(0, editor.Find<TabControl>("PetManagementTabs").SelectedIndex);
        using var pack = CharacterPack.Open(editor.Output!);
        Assert.False(pack.Character.IsGlb); Assert.True(pack.Character.Manifest.Animations["idle"].PingPong);
        Assert.Empty(editor.Library.List());
    }
    [AvaloniaFact]
    public async Task GlbExportCanBeCancelledAndThenOpenedAndInstalledWithoutChangingItsSettings()
    {
        using var editor = new Editor(); await editor.Open(editor.GlbPath);
        editor.Find<TextBox>("GlbPetName").Text = "내 GLB 펫";
        editor.Find<Expander>("GlbAdvanced").IsExpanded = true; editor.Layout();
        editor.Find<ComboBox>("GlbHeading").SelectedIndex = 2;
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
        editor.Find<ComboBox>("GlbSpeed_click").SelectedIndex = 1;
        var output = editor.Output; editor.Output = null;
        editor.Press("CreateGlbPetPack"); await Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Page.HasUnsavedDraft); Assert.Empty(editor.Library.List());
        editor.Output = output;
        editor.Press("CreateGlbPetPack"); await Until(() => !editor.Page.IsBusy); editor.Layout();
        Assert.Equal(0, editor.Find<TabControl>("PetManagementTabs").SelectedIndex);
        Assert.False(editor.Page.HasUnsavedDraft); Assert.Empty(editor.Library.List());
        using (var pack = CharacterPack.Open(output!))
        {
            Assert.True(pack.Character.IsGlb); Assert.Equal(180, pack.Character.Manifest.Model!.Heading);
            Assert.Equal("내 GLB 펫", pack.Character.Manifest.Name);
            Assert.True(pack.Character.Manifest.Animations["click"].Loop);
            Assert.Equal(.5, pack.Character.Manifest.Animations["click"].Speed);
        }
        await Until(() => editor.Find<Button>("InstallPetPack").IsEnabled);
        editor.Press("InstallPetPack"); await Until(() => !editor.Page.IsBusy);
        Assert.True(editor.Library.List().Single().IsGlb);
        editor.Find<TabControl>("PetManagementTabs").SelectedIndex = 1; editor.Layout();
        Assert.Equal(1, editor.Find<ComboBox>("GlbExistingPets").ItemCount);
    }
    [AvaloniaTheory]
    [InlineData(480, 560)]
    [InlineData(860, 680)]
    [InlineData(1120, 800)]
    public async Task BothEditorsFitAndExposeOnlyTheSelectedAction(double width, double height)
    {
        using var editor = new Editor(); await editor.Open(CustomPetDraftTests.Fixture());
        await editor.Open(editor.GlbPath);
        editor.Window.Width = width; editor.Window.Height = height;
        for (var format = 0; format < 2; format++)
        {
            editor.Find<ComboBox>("PetBuilderFormat").SelectedIndex = format; editor.Layout();
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
