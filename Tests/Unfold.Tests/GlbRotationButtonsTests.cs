using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
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
public class GlbRotationButtonsTests
{
    private static readonly string[] Arrows = ["GlbRotateUp", "GlbRotateLeft", "GlbRotateRight", "GlbRotateDown"];
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class Editor : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previousData = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public Window Window { get; } = new() { Width = 1120, Height = 800 };
        public GlbPetView Page { get; }
        public CharacterLibrary Library { get; }
        public CharacterPackage Package { get; }
        public AnimationView Preview => Find<AnimationView>("GlbPreview");
        public Border Stage => Find<Border>("GlbPreviewStage");
        public Editor(GlbRotation rotation = default, GlbRotation clickRotation = default)
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            var source = Path.Combine(temp.Path, "pet.glb");
            File.WriteAllBytes(source, GlbTests.Fixture(morph: true));
            Library = new(Path.Combine(temp.Path, "library"));
            var draft = new GlbPetDraft(source); draft.Set("click", "Idle", false);
            if (rotation != new GlbRotation()) draft.SetRotation("idle", rotation);
            if (clickRotation != new GlbRotation()) draft.SetRotation("click", clickRotation);
            Package = draft.Save(Library);
            Page = new(Window, Library, _ => Task.CompletedTask, () => Task.FromResult<string?>(source));
            Window.Content = Ui.PageFrame(Window, Page); Window.Show(); Layout();
        }
        public T Find<T>(string name) where T : Control => Window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        public void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        public void Press(string name, int times) { for (var i = 0; i < times; i++) Press(name); }
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        public async Task Open() { await Page.OpenPackage(Package); await Settled(); Layout(); }
        public async Task Select(string action) { Find<ComboBox>("GlbPreviewAction").SelectedItem = action; await Settled(); }
        public Task Settled() => Until(() => !Page.IsBusy && !Page.IsPreviewLoading && !Field<bool>(Preview, "renderingLive") &&
            Preview.RenderedGlbPreviewRotation == Preview.GlbPreviewRotation);
        public void Key(Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPress(key, modifiers, physical, key == Avalonia.Input.Key.Space ? " " : null);
            Window.KeyRelease(key, modifiers, physical, key == Avalonia.Input.Key.Space ? " " : null); Layout();
        }
        public void Dispose()
        {
            Page.Dispose(); Window.Close(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previousData); temp.Dispose();
        }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    private static void AssertRotation(Editor editor, GlbRotation expected)
    {
        Assert.Equal(expected, editor.Preview.GlbPreviewRotation);
        Assert.Equal(expected, editor.Preview.RenderedGlbPreviewRotation);
    }
    private static void AssertDisplayedPose(AnimationView preview, GlbAnimationFrames clip, int frame, GlbRotation rotation, bool terminal)
    {
        var expected = new uint[preview.RenderedPixelSize * preview.RenderedPixelSize];
        clip.RenderPreviewFrameInto(frame, preview.RenderedPixelSize, expected, rotation, terminal);
        Assert.Equal(expected, Field<PixelImage>(preview, "liveImage").Pixels);
    }

    [AvaloniaTheory]
    [InlineData("GlbRotateUp", 15, -45, 25)]
    [InlineData("GlbRotateDown", 45, -45, 25)]
    [InlineData("GlbRotateLeft", 30, -60, 25)]
    [InlineData("GlbRotateRight", 30, -30, 25)]
    public async Task EachArrowChangesOnlyItsAxisByFifteenDegrees(string button, float x, float y, float z)
    {
        using var editor = new Editor(new(30, -45, 25)); await editor.Open(); editor.Press("PauseGlbPet");
        Assert.False(editor.Page.HasUnsavedChanges);
        var frames = Field<GlbAnimationFrames>(editor.Preview, "live");
        editor.Press(button); await editor.Settled();
        var expected = new GlbRotation(x, y, z); AssertRotation(editor, expected);
        Assert.Equal(expected, editor.Preview.RenderedGlbPreviewRotation);
        Assert.Same(frames, Field<GlbAnimationFrames>(editor.Preview, "live"));
        Assert.True(editor.Page.HasUnsavedChanges); Assert.True(editor.Find<Button>("SaveGlbPet").IsEnabled);
        Assert.Equal("계속 재생", editor.Find<Button>("PauseGlbPet").Content);
    }

    [AvaloniaTheory]
    [InlineData("GlbRotateUp", -150, -45)]
    [InlineData("GlbRotateDown", -150, -45)]
    [InlineData("GlbRotateLeft", 30, 135)]
    [InlineData("GlbRotateRight", 30, 135)]
    public async Task ArrowsCompleteMultipleFullOrbitsWithoutClamping(string button, float halfX, float halfY)
    {
        var initial = new GlbRotation(30, -45, 25);
        using var editor = new Editor(initial); await editor.Open(); editor.Press("PauseGlbPet"); await editor.Settled();
        var initialPixels = Field<PixelImage>(editor.Preview, "liveImage").Pixels.ToArray();
        editor.Press(button, 12); await editor.Settled(); AssertRotation(editor, new(halfX, halfY, 25));
        editor.Press(button, 12); await editor.Settled(); AssertRotation(editor, initial);
        Assert.Equal(initialPixels, Field<PixelImage>(editor.Preview, "liveImage").Pixels);
        editor.Press(button, 24); await editor.Settled(); AssertRotation(editor, initial);
        Assert.Equal(initialPixels, Field<PixelImage>(editor.Preview, "liveImage").Pixels);
        Assert.True(editor.Find<Button>(button).IsEnabled);
    }

    [AvaloniaTheory]
    [InlineData("GlbRotateUp", -165, 45, 165, 45)]
    [InlineData("GlbRotateDown", 165, 45, -165, 45)]
    [InlineData("GlbRotateLeft", 30, -165, 30, 165)]
    [InlineData("GlbRotateRight", 30, 165, 30, -165)]
    public async Task ArrowsContinueAcrossThePositiveAndNegativeHalfTurn(string button, float x, float y, float expectedX, float expectedY)
    {
        using var editor = new Editor(new(x, y, 35)); await editor.Open(); editor.Press("PauseGlbPet");
        editor.Press(button, 2); await editor.Settled(); AssertRotation(editor, new(expectedX, expectedY, 35));
        Assert.True(editor.Find<Button>(button).IsEnabled);
    }

    [AvaloniaFact]
    public async Task RotatingAPausedMidAnimationPreservesItsFrameAndElapsedTime()
    {
        using var editor = new Editor(new(0, 0, 35)); await editor.Open();
        await Until(() => Field<int>(editor.Preview, "index") > 0 && !Field<bool>(editor.Preview, "renderingLive"));
        editor.Press("PauseGlbPet"); await editor.Settled();
        var frame = Field<int>(editor.Preview, "index"); var clock = Field<Stopwatch>(editor.Preview, "elapsed");
        var time = clock.ElapsedTicks; var clip = Field<GlbAnimationFrames>(editor.Preview, "live");
        Assert.True(frame > 0); Assert.False(clock.IsRunning);
        editor.Press("GlbRotateUp"); editor.Press("GlbRotateRight"); await editor.Settled();
        var rotation = new GlbRotation(-15, 15, 35); AssertRotation(editor, rotation);
        Assert.Same(clip, Field<GlbAnimationFrames>(editor.Preview, "live"));
        Assert.Equal(frame, Field<int>(editor.Preview, "index")); Assert.Equal(time, clock.ElapsedTicks); Assert.False(clock.IsRunning);
        AssertDisplayedPose(editor.Preview, clip, frame, rotation, terminal: false);
        Assert.Equal("계속 재생", editor.Find<Button>("PauseGlbPet").Content);
    }

    [AvaloniaFact]
    public async Task RotatingACompletedActionRetainsItsExactTerminalPoseWithoutReplaying()
    {
        using var editor = new Editor(); await editor.Open();
        var completions = 0; editor.Preview.Completed += () => completions++;
        await editor.Select("click"); await Until(() => completions == 1); await editor.Settled();
        var clip = Field<GlbAnimationFrames>(editor.Preview, "live");
        var frame = Field<int>(editor.Preview, "index"); var clock = Field<Stopwatch>(editor.Preview, "elapsed"); var time = clock.ElapsedTicks;
        editor.Press("GlbRotateDown"); editor.Press("GlbRotateLeft"); await editor.Settled();
        var rotation = new GlbRotation(15, -15, 0); AssertRotation(editor, rotation);
        Assert.Same(clip, Field<GlbAnimationFrames>(editor.Preview, "live"));
        Assert.Equal(frame, Field<int>(editor.Preview, "index")); Assert.Equal(time, clock.ElapsedTicks); Assert.False(clock.IsRunning);
        Assert.Equal(1, completions); Assert.True(Field<bool>(editor.Preview, "completed"));
        AssertDisplayedPose(editor.Preview, clip, frame, rotation, terminal: true);
        Assert.Equal("다시 재생", editor.Find<Button>("PauseGlbPet").Content);
    }

    [AvaloniaFact]
    public async Task ArrowKeysAndHomeWorkWhileAnArrowButtonHasKeyboardFocus()
    {
        using var editor = new Editor(new(0, 0, 25)); await editor.Open(); editor.Press("PauseGlbPet");
        var left = editor.Find<Button>("GlbRotateLeft"); Assert.True(left.Focus());
        editor.Key(Key.Up, PhysicalKey.ArrowUp); editor.Key(Key.Right, PhysicalKey.ArrowRight); await editor.Settled();
        AssertRotation(editor, new(-15, 15, 25)); Assert.True(left.IsFocused);
        editor.Key(Key.Down, PhysicalKey.ArrowDown); editor.Key(Key.Left, PhysicalKey.ArrowLeft); await editor.Settled();
        AssertRotation(editor, new(0, 0, 25));
        editor.Key(Key.Home, PhysicalKey.Home); await editor.Settled();
        AssertRotation(editor, new()); Assert.Equal("계속 재생", editor.Find<Button>("PauseGlbPet").Content);
    }

    [AvaloniaFact]
    public async Task ArrowDirectionsAreSavedAndReopenedIndependentlyForEachAction()
    {
        using var editor = new Editor(new(0, 0, 25), new(0, 0, -35)); await editor.Open(); editor.Press("PauseGlbPet");
        editor.Press("GlbRotateUp"); editor.Press("GlbRotateRight", 2); await editor.Settled();
        var idle = new GlbRotation(-15, 30, 25);
        await editor.Select("click"); AssertRotation(editor, new(0, 0, -35));
        editor.Press("GlbRotateDown", 2); editor.Press("GlbRotateLeft"); await editor.Settled();
        var click = new GlbRotation(30, -15, -35);
        await editor.Select("idle"); AssertRotation(editor, idle);
        editor.Press("SaveGlbPet"); await Until(() => !editor.Page.IsBusy);
        Assert.False(editor.Page.HasUnsavedChanges, editor.Find<TextBlock>("GlbStatus").Text);
        var saved = Assert.Single(editor.Library.List());
        Assert.Equal(idle, saved.Manifest.Animations["idle"].Rotation); Assert.Equal(click, saved.Manifest.Animations["click"].Rotation);
        await editor.Page.OpenPackage(saved); await editor.Settled(); AssertRotation(editor, idle);
        await editor.Select("click"); AssertRotation(editor, click); Assert.False(editor.Page.HasUnsavedChanges);
        Assert.Equal(-35, editor.Preview.GlbPreviewRotation!.Value.Z);
    }

    [AvaloniaFact]
    public async Task BlankUnmappedAndClearedPreviewsCannotRotateOrBecomeDirty()
    {
        using var editor = new Editor();
        VerifyDisabled(); Assert.False(editor.Page.HasUnsavedChanges);
        await editor.Open(); await editor.Select("look");
        Assert.False(editor.Page.HasUnsavedChanges); VerifyDisabled(); Assert.False(editor.Page.HasUnsavedChanges);
        await editor.Select("idle"); editor.Press("GlbRotateUp");
        editor.Press("GlbFileRemove"); editor.Layout();
        Assert.False(editor.Page.HasDraft); Assert.Equal(0, editor.Preview.RenderedPixelSize);
        VerifyDisabled(); Assert.False(editor.Page.HasUnsavedChanges);
        void VerifyDisabled()
        {
            var previous = editor.Preview.GlbPreviewRotation;
            foreach (var name in Arrows.Append("ResetGlbPreviewRotation"))
            {
                Assert.False(editor.Find<Button>(name).IsEnabled);
                editor.Press(name);
            }
            Assert.Equal(previous, editor.Preview.GlbPreviewRotation);
        }
    }

    [AvaloniaTheory]
    [InlineData("GlbRotateUp", 15, -45)]
    [InlineData("GlbRotateDown", 45, -45)]
    [InlineData("GlbRotateLeft", 30, -60)]
    [InlineData("GlbRotateRight", 30, -30)]
    public async Task ARealPointerClickOnAnOverlayArrowRotatesExactlyOnceWithoutStageCapture(string name, float x, float y)
    {
        using var editor = new Editor(new(30, -45, 25)); await editor.Open(); editor.Press("PauseGlbPet"); await editor.Settled();
        editor.Stage.BringIntoView(); editor.Layout();
        var button = editor.Find<Button>(name); var clicks = 0; IPointer? pointer = null;
        button.Click += (_, _) => clicks++;
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
        var point = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), editor.Window)!.Value;
        editor.Window.MouseDown(point, MouseButton.Left);
        Assert.NotNull(pointer); Assert.NotSame(editor.Stage, pointer.Captured);
        AssertRotation(editor, new(30, -45, 25)); Assert.Equal(0, clicks);
        editor.Window.MouseUp(point, MouseButton.Left); await editor.Settled();
        Assert.Equal(1, clicks); AssertRotation(editor, new(x, y, 25)); Assert.Null(pointer.Captured);
    }

    [AvaloniaTheory]
    [InlineData("GlbRotateUp")] [InlineData("GlbRotateDown")]
    [InlineData("GlbRotateLeft")] [InlineData("GlbRotateRight")]
    public async Task DraggingFromAnOverlayArrowDoesNotStartAModelRotation(string name)
    {
        var initial = new GlbRotation(30, -45, 25);
        using var editor = new Editor(initial); await editor.Open(); editor.Press("PauseGlbPet"); await editor.Settled();
        editor.Stage.BringIntoView(); editor.Layout();
        var button = editor.Find<Button>(name); var clicks = 0; IPointer? pointer = null;
        button.Click += (_, _) => clicks++;
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
        var start = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), editor.Window)!.Value;
        var pad = editor.Find<Grid>("GlbRotationPad");
        var end = pad.TranslatePoint(new(pad.Bounds.Width / 2, pad.Bounds.Height / 2), editor.Window)!.Value;
        editor.Window.MouseDown(start, MouseButton.Left);
        Assert.NotNull(pointer); Assert.NotSame(editor.Stage, pointer.Captured);
        editor.Window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(end, MouseButton.Left); await editor.Settled();
        Assert.Equal(0, clicks); AssertRotation(editor, initial); Assert.False(editor.Page.HasUnsavedChanges);
        Assert.Null(pointer.Captured);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task ShiftDirectionKeysStillRotateXYAndPreserveSavedZ(bool focusArrow)
    {
        using var editor = new Editor(new(30, -45, 25)); await editor.Open(); editor.Press("PauseGlbPet");
        Control target = focusArrow ? editor.Find<Button>("GlbRotateRight") : editor.Stage;
        Assert.True(target.Focus());
        editor.Key(Key.Right, PhysicalKey.ArrowRight, RawInputModifiers.Shift); await editor.Settled();
        AssertRotation(editor, new(30, -30, 25));
        editor.Key(Key.Left, PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
        editor.Key(Key.Up, PhysicalKey.ArrowUp, RawInputModifiers.Shift); await editor.Settled();
        AssertRotation(editor, new(15, -45, 25));
    }

    [AvaloniaFact]
    public async Task ShiftModelDragRotatesXYWithoutChangingSavedZ()
    {
        using var editor = new Editor(new(30, -45, 25)); await editor.Open(); editor.Press("PauseGlbPet"); await editor.Settled();
        editor.Stage.BringIntoView(); editor.Layout();
        var start = editor.Stage.TranslatePoint(new(editor.Stage.Bounds.Width * .4, editor.Stage.Bounds.Height * .5), editor.Window)!.Value;
        var end = editor.Stage.TranslatePoint(new(editor.Stage.Bounds.Width * ( .4 + 1d / 6 ), editor.Stage.Bounds.Height * ( .5 + 1d / 12 )), editor.Window)!.Value;
        editor.Window.MouseDown(start, MouseButton.Left, RawInputModifiers.Shift);
        editor.Window.MouseMove(end, RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton);
        editor.Window.MouseUp(end, MouseButton.Left, RawInputModifiers.Shift); await editor.Settled();
        var rotation = editor.Preview.GlbPreviewRotation!.Value;
        Assert.InRange(rotation.X, 59, 61); Assert.InRange(rotation.Y, 14, 16); Assert.Equal(25, rotation.Z);
        Assert.True(editor.Page.HasUnsavedChanges);
    }

    [AvaloniaTheory]
    [InlineData(640, 560)] [InlineData(1120, 800)]
    public async Task OverlayArrowsFitInsideThePreviewAndRemainReachableWithTheKeyboard(double width, double height)
    {
        using var editor = new Editor(); await editor.Open(); editor.Press("PauseGlbPet");
        editor.Window.Width = width; editor.Window.Height = height; editor.Layout();
        var pad = editor.Find<Grid>("GlbRotationPad"); var reset = editor.Find<Button>("ResetGlbPreviewRotation");
        var model = editor.Find<Viewbox>("GlbPreviewModel");
        Assert.Same(pad, editor.Stage.Child); Assert.Contains(model, pad.Children);
        Assert.Equal("펫 회전", AutomationProperties.GetName(pad));
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<NumericUpDown>(), c => c.Name?.StartsWith("GlbRotation") == true);
        Assert.DoesNotContain(editor.Window.GetVisualDescendants().OfType<Grid>(), c => c.Name == "GlbRotationFields");
        Assert.True(editor.Stage.Bounds.Height >= 220, $"Preview height is {editor.Stage.Bounds.Height}.");
        Assert.True(model.Bounds.Width >= 123.9 && model.Bounds.Height >= 123.9,
            $"Model viewport is {model.Bounds.Width} × {model.Bounds.Height}.");
        var scroll = editor.Find<ScrollViewer>("PageBodyScroll");
        scroll.ScrollToHome(); editor.Layout();
        var viewport = scroll.TranslatePoint(default, editor.Window)!.Value;
        var stagePosition = editor.Stage.TranslatePoint(default, editor.Window)!.Value;
        Assert.True(stagePosition.Y >= viewport.Y - 1, "The preview is above the visible scroll area.");
        Assert.True(stagePosition.Y + editor.Stage.Bounds.Height <= viewport.Y + scroll.Viewport.Height + 1,
            $"Preview ends at {stagePosition.Y + editor.Stage.Bounds.Height}, viewport ends at {viewport.Y + scroll.Viewport.Height}.");
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1, $"{scroll.Extent.Width} > {scroll.Viewport.Width}");
        var controls = Arrows.Append("ResetGlbPreviewRotation").Select(editor.Find<Button>).ToArray();
        var rectangles = new List<Rect>();
        foreach (var control in controls)
        {
            Assert.True(control.IsEffectivelyVisible); Assert.True(control.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)));
            Assert.NotNull(ToolTip.GetTip(control));
            var position = control.TranslatePoint(default, pad)!.Value;
            Assert.InRange(position.X, 0, pad.Bounds.Width - control.Bounds.Width + 1);
            Assert.InRange(position.Y, 0, pad.Bounds.Height - control.Bounds.Height + 1);
            Assert.Equal(40, control.Bounds.Width, precision: 1); Assert.Equal(40, control.Bounds.Height, precision: 1);
            var rectangle = new Rect(position, control.Bounds.Size);
            Assert.All(rectangles, other => Assert.False(other.Intersects(rectangle), $"{control.Name} overlaps another rotation control."));
            rectangles.Add(rectangle);
        }
        Point Center(string name)
        {
            var control = editor.Find<Button>(name);
            return control.TranslatePoint(new(control.Bounds.Width / 2, control.Bounds.Height / 2), pad)!.Value;
        }
        var up = Center("GlbRotateUp"); var down = Center("GlbRotateDown");
        var left = Center("GlbRotateLeft"); var right = Center("GlbRotateRight"); var home = Center(reset.Name!);
        // Layout rounding can place an integer-sized button half a DIP from an odd-sized pad's center.
        Assert.InRange(Math.Abs(pad.Bounds.Width / 2 - up.X), 0, 1); Assert.InRange(Math.Abs(pad.Bounds.Width / 2 - down.X), 0, 1);
        Assert.InRange(Math.Abs(pad.Bounds.Height / 2 - left.Y), 0, 1); Assert.InRange(Math.Abs(pad.Bounds.Height / 2 - right.Y), 0, 1);
        Assert.Equal(20, up.Y, precision: 1); Assert.Equal(pad.Bounds.Height - 20, down.Y, precision: 1);
        Assert.Equal(20, left.X, precision: 1); Assert.Equal(pad.Bounds.Width - 20, right.X, precision: 1);
        Assert.Equal(pad.Bounds.Width - 20, home.X, precision: 1); Assert.Equal(20, home.Y, precision: 1);
        foreach (var name in Arrows)
        {
            var button = editor.Find<Button>(name); Assert.True(button.Focus()); Assert.True(button.IsFocused);
            editor.Key(Key.Space, PhysicalKey.Space);
        }
        await editor.Settled(); AssertRotation(editor, new());
        editor.Find<Button>("GlbRotateUp").Focus(); editor.Key(Key.Space, PhysicalKey.Space); await editor.Settled();
        AssertRotation(editor, new(-15, 0, 0)); Assert.True(reset.IsEnabled);
        var reached = new HashSet<string>(); editor.Stage.Focus();
        for (var i = 0; i < 60 && reached.Count < controls.Length; i++)
        {
            editor.Key(Key.Tab, PhysicalKey.Tab);
            foreach (var control in controls.Where(control => control.IsFocused)) reached.Add(control.Name!);
        }
        Assert.Equal(controls.Length, reached.Count);
        Assert.True(reset.Focus()); editor.Key(Key.Space, PhysicalKey.Space); await editor.Settled();
        AssertRotation(editor, new()); Assert.False(reset.IsEnabled);
    }
}
