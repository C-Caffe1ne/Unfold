using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbWorkspaceLayoutTests
{
    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; }
        public GlbPetView Page { get; }
        public CharacterLibrary Library { get; }
        public CharacterPackage Saved { get; }
        public string Source { get; }
        public int InstalledCount { get; private set; }
        public Editor(double width, double height, bool showHeader)
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Source = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(Source, GlbTests.Fixture(morph: true));
            Library = new(Path.Combine(temp.Path, "library"));
            Saved = new GlbPetDraft(Source) { Name = "저장한 모델 펫" }.Save(Library);
            Window = new() { Width = width, Height = height };
            Page = new(Window, Library, _ => { InstalledCount++; return Task.CompletedTask; },
                () => Task.FromResult<string?>(Source), showHeader: showHeader);
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout(Window);
        }
        public T Find<T>(string name) where T : Control => GlbWorkspaceLayoutTests.Find<T>(Window, name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public async Task Open()
        { await Page.OpenPackage(Saved); await Until(() => !Page.IsBusy && !Page.IsPreviewLoading); Layout(Window); }
        public async Task Select(string action)
        { Find<ComboBox>("GlbPreviewAction").SelectedItem = action; await Until(() => !Page.IsPreviewLoading); Layout(Window); }
        public void Dispose() { Page.Dispose(); Window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
    private static T Find<T>(Window window, string? name = null) where T : Control
        => window.GetVisualDescendants().OfType<T>().Single(control => name is null || control.Name == name);
    private static void Layout(Window window)
    { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private static Rect BoundsIn(Control control, Control relative)
        => new(control.TranslatePoint(default, relative)!.Value, control.Bounds.Size);
    private static void AssertContained(Rect outer, Rect inner, string name)
    {
        Assert.True(inner.Left >= outer.Left - 1 && inner.Top >= outer.Top - 1 &&
            inner.Right <= outer.Right + 1 && inner.Bottom <= outer.Bottom + 1,
            $"{name}: {inner} is outside {outer}.");
    }
    private static void AssertGeometry(Window window)
    {
        var workspace = Find<PetEditorWorkspace>(window, "GlbEditor");
        var pane = Find<Grid>(window, "GlbPreviewPane"); var stage = Find<Border>(window, "GlbPreviewStage");
        var controls = Find<StackPanel>(window, "GlbControls"); var settings = Find<StackPanel>(window, "GlbActionPane");
        var stageRect = BoundsIn(stage, workspace); var controlsRect = BoundsIn(controls, workspace); var settingsRect = BoundsIn(settings, workspace);
        Assert.InRange(stage.Bounds.Height, 220, 360);
        if (window.Width == 1120 && window.Height == 800) Assert.Equal(360, stage.Bounds.Height, precision: 1);
        Assert.Equal(workspace.Bounds.Width, stage.Bounds.Width, precision: 1); Assert.Equal(0, stageRect.Left, precision: 1);
        Assert.Equal(3, Grid.GetColumnSpan(pane)); Assert.Equal(0, Grid.GetRow(pane));
        Assert.Equal(0, Grid.GetColumn(controls)); Assert.Equal(2, Grid.GetRow(controls));
        Assert.Equal(2, Grid.GetColumn(settings)); Assert.Equal(2, Grid.GetRow(settings));
        Assert.Equal(controlsRect.Top, settingsRect.Top, precision: 1);
        Assert.Equal(controlsRect.Width, settingsRect.Width, precision: 1);
        Assert.InRange(controlsRect.Top - stageRect.Bottom, 15, 17);
        Assert.InRange(settingsRect.Left - controlsRect.Right, 23, 25);
        Assert.True(controlsRect.Width > 0 && settingsRect.Width > 0);
        AssertContained(new(default, workspace.Bounds.Size), controlsRect, "GLB controls");
        AssertContained(new(default, workspace.Bounds.Size), settingsRect, "GLB action pane");
        var pause = Find<Button>(window, "PauseGlbPet"); var replay = Find<Button>(window, "ReplayGlbPet");
        foreach (var input in new Control[] { pause, replay, Find<ComboBox>(window, "GlbExistingPets"), Find<Grid>(window, "GlbFileRow") })
            AssertContained(new(default, controls.Bounds.Size), BoundsIn(input, controls), input.Name!);
        Assert.False(BoundsIn(pause, controls).Intersects(BoundsIn(replay, controls)), "Playback buttons overlap instead of wrapping.");
        var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"{scroll.Extent.Width} > {scroll.Viewport.Width}");
    }

    private static Rect DisplayCanvas(Window window)
    {
        var preview = Find<AnimationView>(window, "GlbPreview");
        var start = preview.TranslatePoint(default, window)!.Value;
        var end = preview.TranslatePoint(new(preview.Bounds.Width, preview.Bounds.Height), window)!.Value;
        return new(start, new Size(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)));
    }
    private static void AssertPreviewVisible(Window window)
    {
        var scroll = Find<ScrollViewer>(window, "PageBodyScroll"); scroll.ScrollToHome(); Layout(window);
        var viewport = new Rect(scroll.TranslatePoint(default, window)!.Value, scroll.Viewport);
        var stage = Find<Border>(window, "GlbPreviewStage"); var stageRect = BoundsIn(stage, window);
        AssertContained(viewport, stageRect, "The entire GLB preview");
        AssertContained(stageRect, BoundsIn(Find<Viewbox>(window, "GlbPreviewModel"), window), "The model viewport");
        AssertContained(stageRect, DisplayCanvas(window), "The displayed model canvas");
        foreach (var name in new[] { "GlbRotateUp", "GlbRotateDown", "GlbRotateLeft", "GlbRotateRight", "ResetGlbPreviewRotation" })
            AssertContained(viewport, BoundsIn(Find<Button>(window, name), window), name);
    }

    [AvaloniaTheory]
    [InlineData(480, 560, true)] [InlineData(480, 560, false)]
    [InlineData(640, 560, true)] [InlineData(640, 560, false)]
    [InlineData(860, 680, true)] [InlineData(860, 680, false)]
    [InlineData(1120, 800, true)] [InlineData(1120, 800, false)]
    public async Task PreviewUsesTheFullWidthAboveTwoAlignedPanes(double width, double height, bool showHeader)
    {
        using var editor = new Editor(width, height, showHeader); await editor.Open();
        AssertGeometry(editor.Window);
        await editor.Select("click"); AssertGeometry(editor.Window);
        foreach (var action in GlbPetDraft.Actions)
            Assert.Equal(action == "click", editor.Find<StackPanel>("GlbActionRow_" + action).IsVisible);
        var save = editor.Find<Button>("SaveGlbPet"); var bottom = save.TranslatePoint(new(0, save.Bounds.Height), editor.Window)!.Value.Y;
        Assert.InRange(bottom, 0, editor.Window.ClientSize.Height);
    }

    [AvaloniaTheory]
    [InlineData(480, 560)] [InlineData(1120, 800)]
    public async Task LongerActionContentDoesNotChangeTheViewportSizedPreview(double width, double height)
    {
        using var editor = new Editor(width, height, showHeader: false); await editor.Open();
        var stage = editor.Find<Border>("GlbPreviewStage"); var original = stage.Bounds.Height;
        await editor.Select("click"); editor.Find<StackPanel>("GlbActionPane").MinHeight = 500; Layout(editor.Window);
        Assert.InRange(original, 220, 360); Assert.Equal(original, stage.Bounds.Height, precision: 1);
        AssertGeometry(editor.Window);
        Assert.True(editor.Find<ScrollViewer>("PageBodyScroll").Extent.Height > editor.Find<ScrollViewer>("PageBodyScroll").Viewport.Height);
    }

    [AvaloniaTheory]
    [InlineData(true)] [InlineData(false)]
    public async Task DefaultWindowDisplaysAModelCanvasAtLeastTwiceThePreviousSize(bool showHeader)
    {
        using var editor = new Editor(1120, 800, showHeader); await editor.Open();
        var stage = editor.Find<Border>("GlbPreviewStage");
        await Until(() => Math.Abs(stage.Bounds.Height - 360) < .1); Layout(editor.Window);
        AssertGeometry(editor.Window); AssertPreviewVisible(editor.Window);
        var canvas = DisplayCanvas(editor.Window);
        Assert.InRange(canvas.Width, 260, 265); Assert.InRange(canvas.Height, 260, 265);
        Assert.True(canvas.Width >= 124 * 2 && canvas.Height >= 124 * 2);
    }

    [AvaloniaFact]
    public async Task ResizingChangesPreviewScaleWhileScrollingKeepsItsHeightAndScaleStable()
    {
        using var editor = new Editor(1120, 800, showHeader: false); await editor.Open();
        var stage = editor.Find<Border>("GlbPreviewStage");
        await Until(() => Math.Abs(stage.Bounds.Height - 360) < .1); Layout(editor.Window);
        var originalCanvas = DisplayCanvas(editor.Window);
        editor.Window.Width = 640; editor.Window.Height = 560; Layout(editor.Window);
        await Until(() => stage.Bounds.Height >= 220 && stage.Bounds.Height < 359.9); Layout(editor.Window);
        AssertGeometry(editor.Window); AssertPreviewVisible(editor.Window);
        var smallHeight = stage.Bounds.Height; var smallCanvas = DisplayCanvas(editor.Window);
        Assert.InRange(smallCanvas.Height, 123.9, originalCanvas.Height - 1);
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll");
        var availableScroll = scroll.Extent.Height - scroll.Viewport.Height; Assert.True(availableScroll > 0);
        scroll.Offset = new Vector(0, Math.Min(120, availableScroll)); Layout(editor.Window);
        Assert.True(scroll.Offset.Y > 0); Assert.Equal(smallHeight, stage.Bounds.Height, precision: 1);
        var scrolledCanvas = DisplayCanvas(editor.Window);
        Assert.Equal(smallCanvas.Width, scrolledCanvas.Width, precision: 1); Assert.Equal(smallCanvas.Height, scrolledCanvas.Height, precision: 1);
        scroll.ScrollToHome(); Layout(editor.Window); Assert.Equal(smallHeight, stage.Bounds.Height, precision: 1);
        editor.Window.Width = 1120; editor.Window.Height = 800; Layout(editor.Window);
        await Until(() => Math.Abs(stage.Bounds.Height - 360) < .1); Layout(editor.Window);
        AssertGeometry(editor.Window); AssertPreviewVisible(editor.Window);
        var restored = DisplayCanvas(editor.Window);
        Assert.Equal(originalCanvas.Width, restored.Width, precision: 1); Assert.Equal(originalCanvas.Height, restored.Height, precision: 1);
    }

    [AvaloniaFact]
    public async Task LowerControlsKeepSaveActionChangesEditAndClearAccessibleAtNarrowWidth()
    {
        using var editor = new Editor(480, 560, showHeader: false); await editor.Open();
        Assert.False(editor.Page.HasUnsavedChanges);
        var action = editor.Find<ComboBox>("GlbPreviewAction"); var stage = editor.Find<Border>("GlbPreviewStage");
        Assert.True(editor.Find<Button>("OpenGlbPet").Focus());
        Tab(); Assert.True(stage.IsFocused, "The preview must follow the file-open button in keyboard navigation.");
        var add = editor.Find<Button>("GlbFileAdd");
        for (var i = 0; i < 12 && !add.IsFocused; i++)
        {
            Tab(); Assert.False(action.IsFocused, "Keyboard navigation reached the action pane before the left controls.");
        }
        Assert.True(add.IsFocused, "The left file controls must follow the preview's rotation buttons.");
        await editor.Select("click"); Assert.True(action.Focus());
        editor.Find<ComboBox>("GlbClip_click").SelectedItem = "Idle";
        await Until(() => !editor.Page.IsPreviewLoading);
        editor.Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
        await Until(() => !editor.Page.IsPreviewLoading);
        editor.Press("GlbRotateRight"); AssertGeometry(editor.Window);
        var save = editor.Find<Button>("SaveGlbPet"); Assert.True(save.IsEnabled); Assert.True(save.Focus());
        editor.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        editor.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        await Until(() => !editor.Page.IsBusy && !editor.Page.HasUnsavedChanges);
        Assert.Equal(1, editor.InstalledCount);
        var saved = Assert.Single(editor.Library.List()); Assert.True(saved.Manifest.Animations["click"].Loop);
        Assert.Equal(new GlbRotation(0, 15, 0), saved.Manifest.Animations["click"].Rotation);
        var edit = editor.Find<Button>("EditGlbPet"); Assert.True(edit.IsEffectivelyVisible); Assert.True(edit.Focus());
        editor.Press("EditGlbPet"); await Until(() => !editor.Page.IsBusy && !editor.Page.IsPreviewLoading);
        await editor.Select("click"); Assert.Equal(new GlbRotation(0, 15, 0), editor.Find<AnimationView>("GlbPreview").GlbPreviewRotation);
        var clear = editor.Find<Button>("GlbFileRemove"); Assert.Equal("3D 파일 제거", AutomationProperties.GetName(clear));
        Assert.True(clear.Focus());
        editor.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        editor.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " "); Layout(editor.Window);
        Assert.False(editor.Page.HasDraft); Assert.False(editor.Page.HasUnsavedChanges);
        Assert.Equal(0, editor.Find<AnimationView>("GlbPreview").RenderedPixelSize);
        Assert.False(editor.Find<ComboBox>("GlbPreviewAction").IsEnabled);
        Assert.False(save.IsEnabled); Assert.True(editor.Find<Button>("GlbFileAdd").IsEnabled);
        void Tab()
        {
            editor.Window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            editor.Window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); Layout(editor.Window);
        }
    }

    [AvaloniaFact]
    public async Task SavedGlbUsesTheSameLayoutInsideTheMinimumSettingsWindowShell()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        SettingsWindow? window = null;
        try
        {
            var source = Path.Combine(temp.Path, "saved.glb"); File.WriteAllBytes(source, GlbTests.Fixture(morph: true));
            var package = new GlbPetDraft(source) { Name = "설정 창에서 편집할 모델 펫" }.Save(runtime.Library);
            // Diagnostic startup raises MinWidth/MinHeight to its initial 1120×800.
            // Construct the production shell directly to exercise its real minimum size.
            window = new SettingsWindow(runtime) { Width = 640, Height = 560 };
            lifetime.MainWindow = window;
            Assert.Equal(640, window.MinWidth); Assert.Equal(560, window.MinHeight);
            window.Show(); Layout(window);
            Find<Button>(window, "SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => window.GetVisualDescendants().OfType<PetBuilderView>().Any());
            await Find<PetBuilderView>(window).OpenPackage(package);
            await Until(() => !Find<GlbPetView>(window).IsBusy && !Find<GlbPetView>(window).IsPreviewLoading); Layout(window);
            Assert.Equal(640, window.Bounds.Width); Assert.Equal(560, window.Bounds.Height);
            Assert.Equal(package.Manifest.Id, Find<GlbPetView>(window).EditingId); AssertGeometry(window); AssertPreviewVisible(window);
            Assert.True(DisplayCanvas(window).Height >= 123.9);
            Assert.False(Find<GlbPetView>(window).HasUnsavedChanges);
        }
        finally { window?.Hide(); window?.Dispose(); runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
}
