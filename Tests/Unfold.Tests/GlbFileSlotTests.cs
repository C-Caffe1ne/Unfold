using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbFileSlotTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; } = new() { Width = 1000, Height = 780 };
        public PetBuilderView Page { get; }
        public CharacterLibrary Library { get; }
        public string Source { get; }
        public string? Chosen { get; set; }
        public Editor()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Source = Path.Combine(temp.Path, "sample.glb"); File.WriteAllBytes(Source, GlbTests.Fixture(morph: true)); Chosen = Source;
            Library = new(Path.Combine(temp.Path, "library"));
            Page = new(Window, Library, _ => Task.CompletedTask, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(Chosen));
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public async Task Open()
        {
            Press("OpenPetBuilderFile"); await Until(() => !Page.IsBusy); Layout();
            await Until(() => !Window.GetVisualDescendants().OfType<GlbPetView>().Single().IsPreviewLoading);
        }
        public void Dispose() { Page.Dispose(); Window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task TrashImmediatelyClearsOnlyTheEditorFileEvenWhenThePetIsSaved(bool saved)
    {
        using var editor = new Editor(); await editor.Open();
        editor.Find<TextBox>("GlbPetName").Text = "남겨둘 이름";
        if (saved) { editor.Press("SaveGlbPet"); await Until(() => !editor.Page.IsBusy); }
        var source = File.ReadAllBytes(editor.Source);
        var installed = saved ? Assert.Single(editor.Library.List()) : null;
        var revision = installed is null ? null : File.ReadAllBytes(Path.Combine(installed.DirectoryPath, "model.glb"));
        Assert.Equal("sample.glb", editor.Find<TextBlock>("GlbFileLabel").Text);
        Assert.NotNull(editor.Find<Image>("GlbFileThumbnail").Source);
        var remove = editor.Find<Button>("GlbFileRemove"); Assert.Contains("danger", remove.Classes);
        Assert.Equal("3D 파일 제거", AutomationProperties.GetName(remove));
        editor.Press("GlbFileRemove"); editor.Layout();
        Assert.Empty(editor.Window.OwnedWindows);
        Assert.Equal("파일 없음", editor.Find<TextBlock>("GlbFileLabel").Text);
        Assert.Null(editor.Find<Image>("GlbFileThumbnail").Source);
        Assert.Equal("남겨둘 이름", editor.Find<TextBox>("GlbPetName").Text);
        Assert.Equal(0, editor.Find<AnimationView>("GlbPreview").RenderedPixelSize);
        Assert.False(editor.Find<Button>("GlbFileRemove").IsEnabled);
        Assert.True(editor.Find<Button>("GlbFileAdd").IsEnabled);
        Assert.False(editor.Find<Button>("SaveGlbPet").IsEnabled);
        Assert.False(editor.Find<Button>("CreateGlbPetPack").IsEnabled);
        Assert.Equal(source, File.ReadAllBytes(editor.Source)); Assert.Equal(saved ? 1 : 0, editor.Library.List().Count);
        if (installed is not null) Assert.Equal(revision, File.ReadAllBytes(Path.Combine(installed.DirectoryPath, "model.glb")));
        // The same add control restores a file without deleting or replacing an installed pet.
        editor.Press("GlbFileAdd"); await Until(() => !editor.Page.IsBusy); editor.Layout();
        Assert.Equal("sample.glb", editor.Find<TextBlock>("GlbFileLabel").Text);
        Assert.Equal("남겨둘 이름", editor.Find<TextBox>("GlbPetName").Text);
        Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
    }

    [AvaloniaTheory]
    [InlineData(480)] [InlineData(860)] [InlineData(1120)]
    public async Task LongFilenameFitsWithTheSameCompactButtonsAsMedia(double width)
    {
        using var editor = new Editor();
        var longPath = Path.Combine(Path.GetDirectoryName(editor.Source)!, new string('g', 150) + ".glb");
        File.Copy(editor.Source, longPath); editor.Chosen = longPath; await editor.Open();
        editor.Window.Width = width; editor.Layout();
        var label = editor.Find<TextBlock>("GlbFileLabel"); var add = editor.Find<Button>("GlbFileAdd"); var remove = editor.Find<Button>("GlbFileRemove");
        Assert.Equal(Path.GetFileName(longPath), ToolTip.GetTip(label));
        Assert.Equal(Avalonia.Media.TextTrimming.CharacterEllipsis, label.TextTrimming);
        Assert.All(new[] { add, remove }, button => { Assert.Equal(32, button.Width); Assert.Equal(32, button.Height); Assert.IsType<PathIcon>(button.Content); Assert.True(button.IsEffectivelyVisible); });
        Assert.Equal(add.TranslatePoint(default, editor.Window)!.Value.Y, remove.TranslatePoint(default, editor.Window)!.Value.Y);
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll"); Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        editor.Press("GlbFileRemove"); editor.Chosen = null; editor.Press("GlbFileAdd"); await Until(() => !editor.Page.IsBusy);
        Assert.Equal("파일 없음", label.Text); Assert.False(remove.IsEnabled);
    }
}
