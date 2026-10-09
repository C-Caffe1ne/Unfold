using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
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
public class GlbPreviewRotationTests
{
    [Fact]
    public void PreviewRotationKeepsOffCenterModelsFramedAndSharedFramesUnchanged()
    {
        var bytes = GlbTests.Fixture(morph: true);
        var binary = 28 + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        // Offset the mesh from the world origin; orbiting must still keep it centered.
        for (var vertex = 0; vertex < 3; vertex++)
        {
            var x = binary + vertex * 12;
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(x), BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(x)) + 2);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(x + 8), 3);
        }
        var model = GlbModel.Parse(bytes);
        var clip = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        var shared = clip[0].Image; var original = shared.Pixels.ToArray();
        var pixels = new uint[192 * 192];
        clip.RenderPreviewFrameInto(0, 192, pixels, new()); var front = pixels.ToArray();
        foreach (var rotation in new[] { new GlbRotation(30, 45, 25), new GlbRotation(-40, 135, -20), new GlbRotation(0, 180, 0), new GlbRotation(25, -45, 45) })
        {
            clip.RenderPreviewFrameInto(0, 192, pixels, rotation);
            var visible = Enumerable.Range(0, pixels.Length).Where(i => pixels[i] >> 24 > 0).ToArray();
            Assert.NotEmpty(visible);
            Assert.InRange((visible.Min(i => i % 192) + visible.Max(i => i % 192)) / 2d, 75, 116);
            Assert.All(visible, i => Assert.InRange(i % 192, 1, 190));
        }
        foreach (var rotation in new[] { new GlbRotation(45, 0, 0), new GlbRotation(0, 45, 0), new GlbRotation(0, 0, 45) })
        {
            clip.RenderPreviewFrameInto(0, 192, pixels, rotation);
            Assert.NotEqual(front, pixels); Assert.Contains(pixels, pixel => pixel >> 24 > 0);
        }
        clip.RenderPreviewFrameInto(0, 192, pixels, new GlbRotation(360, 360, 360).Normalize()); Assert.Equal(front, pixels);
        clip.RenderPreviewFrameInto(0, 192, pixels, new(), terminal: true); Assert.NotEqual(front, pixels);
        Assert.Equal(original, shared.Pixels);
        clip.RenderFrameInto(0, 192, pixels); Assert.Equal(original, pixels);
        Assert.Throws<ArgumentOutOfRangeException>(() => clip.RenderPreviewFrameInto(0, 192, pixels, new(float.NaN)));
        Assert.Throws<ArgumentException>(() => clip.RenderPreviewFrameInto(0, 192, new uint[1], new()));
    }

    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        public Window Window { get; } = new() { Width = 1120, Height = 800 };
        public GlbPetView Page { get; }
        public CharacterLibrary Library { get; }
        public string Source { get; }
        public AnimationView Preview => Find<AnimationView>("GlbPreview");
        public Border Stage => Find<Border>("GlbPreviewStage");
        public Editor()
        {
            Source = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(Source, GlbTests.Fixture(morph: true));
            Library = new(Path.Combine(temp.Path, "library"));
            var draft = new GlbPetDraft(Source); draft.Set("click", "Idle", false); draft.Save(Library);
            Page = new(Window, Library, _ => Task.CompletedTask, () => Task.FromResult<string?>(Source));
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public async Task Open()
        {
            Find<ComboBox>("GlbExistingPets").SelectedIndex = 0;
            await Until(() => !Page.IsBusy && !Page.IsPreviewLoading && Preview.RenderedGlbPreviewRotation == new GlbRotation());
            Layout();
        }
        public Point Point(double fraction = .25) => Stage.TranslatePoint(new(Stage.Bounds.Width * fraction, Stage.Bounds.Height / 2), Window)!.Value;
        public void Key(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, physical, null);
            Window.KeyRelease(key, modifiers, physical, null); Layout();
        }
        public void Dispose() { Page.Dispose(); Window.Close(); temp.Dispose(); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }

    [AvaloniaFact]
    public async Task PausedPreviewDragSavesItsDirectionAndStopsAfterCaptureLoss()
    {
        using var editor = new Editor(); await editor.Open();
        editor.Press("PauseGlbPet");
        var start = editor.Point(); var end = editor.Point(5d / 12) + new Vector(0, editor.Stage.Bounds.Height / 12);
        editor.Window.MouseDown(start, MouseButton.Left);
        editor.Window.MouseMove(end); editor.Window.MouseUp(end, MouseButton.Left);
        await Until(() => editor.Preview.RenderedGlbPreviewRotation == editor.Preview.GlbPreviewRotation);
        Assert.InRange(editor.Preview.GlbPreviewRotation!.Value.Y, 59, 61);
        Assert.InRange(editor.Preview.GlbPreviewRotation.Value.X, 29, 31);
        Assert.True(editor.Page.HasUnsavedChanges); Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
        Assert.Equal("계속 재생", editor.Find<Button>("PauseGlbPet").Content);
        var heading = editor.Preview.GlbPreviewRotation;
        editor.Window.MouseMove(start); Assert.Equal(heading, editor.Preview.GlbPreviewRotation);
        editor.Window.MouseDown(start, MouseButton.Right);
        editor.Window.MouseMove(end, RawInputModifiers.RightMouseButton); editor.Window.MouseUp(end, MouseButton.Right);
        Assert.Equal(heading, editor.Preview.GlbPreviewRotation);
        IPointer? pointer = null;
        editor.Stage.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
            RoutingStrategies.Tunnel, true);
        editor.Window.MouseDown(start, MouseButton.Left); Assert.NotNull(pointer); pointer.Capture(null);
        editor.Window.MouseMove(end, RawInputModifiers.LeftMouseButton); editor.Window.MouseUp(end, MouseButton.Left);
        Assert.Equal(heading, editor.Preview.GlbPreviewRotation);
        editor.Press("SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        Assert.False(editor.Page.HasUnsavedChanges, editor.Find<TextBlock>("GlbStatus").Text);
        Assert.Equal(heading, editor.Library.List().Single().Manifest.Animations["idle"].Rotation);
        Assert.Null(editor.Library.List().Single().Manifest.Animations["click"].Rotation);
        editor.Press("ResetGlbPreviewRotation"); await Until(() => editor.Preview.RenderedGlbPreviewRotation == new GlbRotation());
        Assert.False(editor.Find<Button>("ResetGlbPreviewRotation").IsEnabled);
    }

    [AvaloniaFact]
    public async Task CompletedPreviewRotatesWithKeyboardWithoutReplayingAndLatestAngleWins()
    {
        using var editor = new Editor(); await editor.Open();
        var completed = 0; editor.Preview.Completed += () => completed++;
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Until(() => completed == 1);
        editor.Stage.Focus();
        for (var i = 0; i < 9; i++) editor.Key(Key.Right, PhysicalKey.ArrowRight);
        await Until(() => editor.Preview.RenderedGlbPreviewRotation == new GlbRotation(0, 135, 0));
        Assert.Equal(1, completed); Assert.Equal("다시 재생", editor.Find<Button>("PauseGlbPet").Content);
        editor.Key(Key.Left, PhysicalKey.ArrowLeft); await Until(() => editor.Preview.RenderedGlbPreviewRotation == new GlbRotation(0, 120, 0));
        editor.Key(Key.Home, PhysicalKey.Home); await Until(() => editor.Preview.RenderedGlbPreviewRotation == new GlbRotation());
        Assert.Equal(1, completed); Assert.True(editor.Page.HasUnsavedChanges);
        editor.Key(Key.Left, PhysicalKey.ArrowLeft); await Until(() => editor.Preview.RenderedGlbPreviewRotation == new GlbRotation(0, -15, 0));
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "idle";
        await Until(() => !editor.Page.IsPreviewLoading && editor.Preview.RenderedGlbPreviewRotation == new GlbRotation());
        editor.Press("SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        await editor.Page.OpenPackage(editor.Library.List().Single());
        await Until(() => !editor.Page.IsPreviewLoading && editor.Preview.RenderedGlbPreviewRotation == new GlbRotation());
        editor.Press("GlbFileRemove"); editor.Stage.Focus(); editor.Key(Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal(new GlbRotation(), editor.Preview.GlbPreviewRotation); Assert.Equal(0, editor.Preview.RenderedPixelSize);
    }

    [AvaloniaFact]
    public async Task ShiftDragAndInputsSaveAllAxesAndRestoreEachActionIndependently()
    {
        using var editor = new Editor(); await editor.Open(); editor.Press("PauseGlbPet");
        var start = editor.Point(); var end = editor.Point(.5);
        editor.Window.MouseDown(start, MouseButton.Left, RawInputModifiers.Shift);
        editor.Window.MouseMove(end, RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(end, MouseButton.Left, RawInputModifiers.Shift);
        Assert.Equal(new GlbRotation(0, 0, 90), editor.Preview.GlbPreviewRotation);
        editor.Find<NumericUpDown>("GlbRotationX").Value = 25;
        editor.Find<NumericUpDown>("GlbRotationY").Value = -40;
        var idle = new GlbRotation(25, -40, 90);
        await Until(() => editor.Preview.RenderedGlbPreviewRotation == idle);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Until(() => !editor.Page.IsPreviewLoading && editor.Preview.RenderedGlbPreviewRotation == new GlbRotation());
        editor.Stage.Focus(); editor.Key(Key.Down, PhysicalKey.ArrowDown);
        editor.Key(Key.Left, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
        var click = new GlbRotation(15, 0, -15);
        await Until(() => editor.Preview.RenderedGlbPreviewRotation == click);
        editor.Press("SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        var package = editor.Library.List().Single();
        Assert.Equal(idle, package.Manifest.Animations["idle"].Rotation);
        Assert.Equal(click, package.Manifest.Animations["click"].Rotation);
        await editor.Page.OpenPackage(package);
        await Until(() => !editor.Page.IsPreviewLoading && editor.Preview.RenderedGlbPreviewRotation == idle);
        Assert.False(editor.Page.HasUnsavedChanges);
        Assert.Equal(25, editor.Find<NumericUpDown>("GlbRotationX").Value);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
        await Until(() => !editor.Page.IsPreviewLoading && editor.Preview.RenderedGlbPreviewRotation == click);
        editor.Find<ComboBox>("GlbPreviewAction").SelectedItem = "look";
        await Until(() => !editor.Page.IsPreviewLoading);
        Assert.False(editor.Find<NumericUpDown>("GlbRotationX").IsEnabled);
    }

    [AvaloniaTheory]
    [InlineData(640, 560)] [InlineData(1120, 800)]
    public async Task RotationControlsFitAtMinimumAndDefaultWindowSizes(double width, double height)
    {
        using var editor = new Editor(); await editor.Open();
        editor.Window.Width = width; editor.Window.Height = height; editor.Layout();
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll");
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        Assert.True(editor.Stage.Focusable);
        Assert.True(editor.Find<Button>("ResetGlbPreviewRotation").IsEffectivelyVisible);
    }
}
