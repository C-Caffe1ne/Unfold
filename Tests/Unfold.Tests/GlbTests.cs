using System.Buffers.Binary;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.Headless;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbTests
{
    internal static byte[] Fixture(bool rootMotion = true, bool morph = false, string interpolation = "LINEAR")
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        var views = new List<object>(); var accessors = new List<object>();
        int Data(float[] data, string type, int count)
        {
            var offset = (int)buffer.Position; foreach (var value in data) writer.Write(value);
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = data.Length * 4 });
            accessors.Add(new { bufferView = views.Count - 1, componentType = 5126, count, type }); return accessors.Count - 1;
        }
        var position = Data([-.4f, 0, 0, .4f, 0, 0, 0, 1, 0], "VEC3", 3);
        // JOINTS uses unsigned bytes, unlike all other synthetic accessors.
        var jo = (int)buffer.Position; writer.Write(new byte[12]); views.Add(new { buffer = 0, byteOffset = jo, byteLength = 12 });
        accessors.Add(new { bufferView = views.Count - 1, componentType = 5121, count = 3, type = "VEC4" }); var joints = accessors.Count - 1;
        var weights = Data([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], "VEC4", 3);
        var time = Data([0, .2f], "SCALAR", 2);
        var translation = Data([0, 0, 0, 1, 0, 0], "VEC3", 2);
        var rotation = Data([0, 0, 0, 1, 0, 1, 0, 0], "VEC4", 2);
        var delta = Data([0, 0, 0, 0, 0, 0, .2f, 0, 0], "VEC3", 3);
        var morphWeights = Data([0, 1], "SCALAR", 2);
        var primitive = new Dictionary<string, object> { ["attributes"] = new { POSITION = position, JOINTS_0 = joints, WEIGHTS_0 = weights } };
        if (morph) primitive["targets"] = new[] { new { POSITION = delta } };
        var channels = new List<object>(); var samplers = new List<object>();
        void Channel(int node, string path, int output)
        { channels.Add(new { sampler = samplers.Count, target = new { node, path } }); samplers.Add(new { input = time, output, interpolation }); }
        if (rootMotion) { Channel(0, "translation", translation); Channel(0, "rotation", rotation); }
        if (morph) Channel(1, "weights", morphWeights);
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)buffer.Length } }, bufferViews = views, accessors,
            nodes = new object[] { new { name = "Root", children = new[] { 1 } }, new { name = "Body", mesh = 0, skin = 0 } },
            skins = new[] { new { joints = new[] { 0 } } }, meshes = new[] { new { primitives = new[] { primitive } } },
            scenes = new[] { new { nodes = new[] { 0 } } }, scene = 0,
            animations = new[] { new { name = "Idle", samplers, channels } }
        });
        var jsonLength = (json.Length + 3) / 4 * 4; var binary = buffer.ToArray(); var result = new byte[12 + 8 + jsonLength + 8 + binary.Length];
        using var output = new BinaryWriter(new MemoryStream(result)); output.Write(0x46546C67u); output.Write(2u); output.Write((uint)result.Length);
        output.Write((uint)jsonLength); output.Write(0x4E4F534Au); output.Write(json); for (var i = json.Length; i < jsonLength; i++) output.Write((byte)' ');
        output.Write((uint)binary.Length); output.Write(0x004E4942u); output.Write(binary); return result;
    }
    [Fact]
    public void RootTranslationAndRotationRemainFixedButBackgroundIsTransparent()
    {
        var model = GlbModel.Parse(Fixture()); var definition = new GlbDefinition("model.glb");
        var first = model.Render("Idle", 0, definition, "Idle"); var last = model.Render("Idle", .2, definition, "Idle");
        Assert.Equal(first.Pixels, last.Pixels);
        Assert.Contains(first.Pixels, p => p >> 24 == 0); Assert.Contains(first.Pixels, p => p >> 24 == 255);
        Assert.Equal(1, model.TriangleCount); Assert.Equal(.2, model.Animations[0].Duration, 5);
    }
    [Fact]
    public void MorphWeightsStillAnimateWithRootLocked()
    {
        var model = GlbModel.Parse(Fixture(morph: true)); var definition = new GlbDefinition("model.glb");
        Assert.NotEqual(model.Render("Idle", 0, definition, "Idle").Pixels, model.Render("Idle", .2, definition, "Idle").Pixels);
        var clip = model.CreateAnimation("Idle", definition, "Idle", .5);
        Assert.Equal(.4, clip.DurationSeconds, 5);
        var first = clip[0]; _ = clip[1]; _ = clip[2]; Assert.Same(first, clip[0]);
        Assert.Throws<InvalidDataException>(() => model.CreateAnimation("Idle", definition, "Idle", 0));
    }
    [Fact]
    public void StepMorphChangesExactlyAtItsKeyframe()
    {
        var model = GlbModel.Parse(Fixture(morph: true, interpolation: "STEP"));
        var definition = new GlbDefinition("model.glb");
        var first = model.Render("Idle", 0, definition, "Idle");
        Assert.Equal(first.Pixels, model.Render("Idle", .1, definition, "Idle").Pixels);
        Assert.NotEqual(first.Pixels, model.Render("Idle", .2, definition, "Idle").Pixels);
    }
    [Theory]
    [InlineData(192, 1, 192)]
    [InlineData(192, 2, 384)]
    [InlineData(288, 2, 576)]
    [InlineData(288, 3, 864)]
    [InlineData(1000, 2, 1024)]
    public void GlbResolutionCoversDisplayAndDpiWithinTheMemoryLimit(double logicalSize, double dpi, int pixels)
        => Assert.Equal(pixels, AnimationView.PixelSizeFor(logicalSize, dpi));
    [Fact]
    public void SharedClipCanRenderDifferentViewSizesWithoutMutatingExistingImages()
    {
        var clip = GlbModel.Parse(Fixture()).CreateAnimation("Idle", new("model.glb"), "Idle");
        var thumbnail = clip[0]; var enlarged = clip.GetFrame(0, 576); var snapshot = enlarged.Image.Pixels.ToArray();
        _ = clip.GetFrame(1, 384);
        Assert.Equal(576, enlarged.Image.Width); Assert.Equal(snapshot, enlarged.Image.Pixels);
        Assert.Same(thumbnail, clip[0]); Assert.Equal(192, thumbnail.Image.Width);
        Assert.Contains(enlarged.Image.Pixels, pixel => (pixel >> 24) is > 0 and < 255);
        Assert.Throws<ArgumentOutOfRangeException>(() => clip.GetFrame(0, GlbAnimationFrames.MaxFrameSize + 1));
    }
    [AvaloniaFact]
    public async Task GlbResolutionIncludesViewboxScalingWhilePaused()
    {
        using var view = new AnimationView { Width = 96, Height = 96 };
        var window = new Window { Width = 288, Height = 288, Content = new Viewbox { Child = view, Stretch = Avalonia.Media.Stretch.Uniform } };
        try
        {
            view.SetRunning(false); window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            view.SetFrames(GlbModel.Parse(Fixture()).CreateAnimation("Idle", new("model.glb"), "Idle"), true, false);
            for (var i = 0; i < 100 && view.RenderedPixelSize < 288; i++)
            { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(96, view.Bounds.Width); Assert.True(view.RenderedPixelSize >= 288);
            var timer = (DispatcherTimer)typeof(AnimationView).GetField("timer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
            Assert.False(timer.IsEnabled);
        }
        finally { view.Dispose(); window.Close(); }
    }
    [Fact]
    public void RenderedGlbEdgesHavePartialCoverage()
    {
        var clip = GlbModel.Parse(Fixture()).CreateAnimation("Idle", new("model.glb"), "Idle");
        Assert.Contains(clip[0].Image.Pixels, pixel => (pixel >> 24) is > 0 and < 255);
        Assert.Contains(clip[0].Image.Pixels, pixel => pixel >> 24 == 0);
        Assert.Contains(clip[0].Image.Pixels, pixel => pixel >> 24 == 255);
    }
    [AvaloniaFact]
    public async Task PausedGlbRendersAgainAtTheEnlargedDisplaySize()
    {
        var clip = GlbModel.Parse(Fixture()).CreateAnimation("Idle", new("model.glb"), "Idle");
        using var view = new AnimationView { Width = 192, Height = 192 };
        var window = new Window { Width = 400, Height = 400, Content = view };
        PixelImage Current() => (PixelImage)typeof(AnimationView).GetField("liveImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); view.SetRunning(false); view.SetFrames(clip, true, false);
            view.Width = view.Height = 288; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < 100 && Current().Width < 288; i++)
            { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
            Assert.True(Current().Width >= 288, $"Enlarged pet still uses {Current().Width}px.");
            Assert.False(view.OpaqueAt(new Point(0, 0))); Assert.True(view.OpaqueAt(new Point(144, 195)));
        }
        finally { view.Dispose(); window.Close(); }
    }
    [Fact]
    public void TruncatedAndCorruptFilesAreRejectedBeforeRendering()
    {
        var bytes = Fixture(); Assert.Throws<InvalidDataException>(() => GlbModel.Parse(bytes[..^1]));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => GlbModel.Parse(bytes));
    }
    [Fact]
    public void MappingsPersistThroughPackInstallAndReopenWithoutChangingSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "Unfold-glb-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var input = Path.Combine(root, "test.glb"); var bytes = Fixture(morph: true); File.WriteAllBytes(input, bytes);
            var draft = new GlbPetDraft(input); draft.Set("hover", "Idle", true, .5); draft.Set("pickup", "Idle");
            var library = new CharacterLibrary(Path.Combine(root, "library")); var package = draft.Save(library);
            Assert.True(package.IsGlb); Assert.True(package.HasPointerArt); Assert.IsType<GlbAnimationFrames>(package.LoadAnimation("hover"));
            var reopened = Assert.Single(library.List()); Assert.Equal(.5, reopened.Manifest.Animations["hover"].Speed);
            Assert.Equal(bytes, File.ReadAllBytes(input));
            var edit = new GlbPetDraft(reopened) { Name = "Edited" }; edit.Set("hover", null); edit.Save(library);
            Assert.Equal("Edited", Assert.Single(library.List()).Manifest.Name);
            Assert.DoesNotContain("hover", library.List()[0].Manifest.Animations.Keys);
        }
        finally { Directory.Delete(root, true); }
    }
    [AvaloniaFact]
    public async Task GlbEditorImportsMapsSavesAndEditsThroughControls()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Unfold-glb-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "test.glb"); File.WriteAllBytes(file, Fixture(morph: true));
        var library = new CharacterLibrary(Path.Combine(directory, "library")); CharacterPackage? selected = null;
        var owner = new Window { Width = 620, Height = 850 }; using var page = new GlbPetView(owner, library, p => { selected = p; return Task.CompletedTask; }, () => Task.FromResult<string?>(file));
        owner.Content = page;
        T Find<T>(string name) where T : Control => ((Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(owner)).OfType<T>().Single(c => c.Name == name));
        async Task Until(Func<bool> ready)
        {
            for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
            Assert.True(ready(), Find<TextBlock>("GlbStatus").Text);
        }
        void Press(string name) => Find<Button>(name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        try
        {
            owner.Show(); Press("OpenGlbPet"); await Until(() => !page.IsBusy && Find<Button>("SaveGlbPet").IsEnabled);
            Find<ComboBox>("GlbClip_click").SelectedItem = "Idle"; Find<ComboBox>("GlbSpeed_click").SelectedIndex = 1;
            Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1; Find<TextBox>("GlbPetName").Text = "Mapped pet";
            Press("SaveGlbPet"); await Until(() => !page.IsBusy && selected is not null);
            Assert.Equal("Mapped pet", selected!.Manifest.Name); Assert.Equal(.5, selected.Manifest.Animations["click"].Speed);
            Assert.True(selected.Manifest.Animations["click"].Loop); Assert.False(page.HasUnsavedChanges);
            Find<ComboBox>("GlbExistingPets").SelectedIndex = 0; await Until(() => !page.IsBusy && page.HasUnsavedChanges);
            Find<TextBox>("GlbPetName").Text = "Edited pet"; Press("SaveGlbPet"); await Until(() => !page.IsBusy && selected.Manifest.Name == "Edited pet");
            Assert.Single(library.List());
        }
        finally { page.Dispose(); owner.Close(); Directory.Delete(directory, true); }
    }
    [AvaloniaFact]
    public async Task GlbClickDragAndPointerReleaseKeepExistingLifecycle()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime(); using var runtime = new AppRuntime(lifetime);
        try
        {
            var file = Path.Combine(temp.Path, "pet.glb"); File.WriteAllBytes(file, Fixture(morph: true)); var draft = new GlbPetDraft(file);
            foreach (var key in new[] { "click", "pickup", "held", "land", "hover", "attention", "celebrate" }) draft.Set(key, "Idle", key == "held");
            draft.Set("walk", "Idle", true);
            var package = draft.Save(runtime.Library); await runtime.Start(true, true); runtime.Stop(); await runtime.SelectInstalledCharacter(package);
            var pet = runtime.ActivePet!; Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
            async Task Until(Func<bool> ready)
            {
                for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
                Assert.True(ready(), pet.ActiveAnimation);
            }
            // Select a painted point in window coordinates, including the speech layout offset.
            var local = new Point(96, 130); var point = pet.PetView.TranslatePoint(local, pet)!.Value;
            pet.MouseDown(point, Avalonia.Input.MouseButton.Left); pet.MouseUp(point, Avalonia.Input.MouseButton.Left);
            await Until(() => pet.ActiveAnimation == "click"); await Until(() => pet.ActiveAnimation == "idle");
            point = pet.PetView.TranslatePoint(local, pet)!.Value;
            pet.MouseDown(point, Avalonia.Input.MouseButton.Left); pet.MouseMove(point + new Vector(20, 0));
            Assert.Equal(PetPointerPhase.Pickup, pet.PointerPhase); await Until(() => pet.PointerPhase == PetPointerPhase.Held);
            pet.MouseUp(point + new Vector(20, 0), Avalonia.Input.MouseButton.Left); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
            await Until(() => pet.ActiveAnimation == "idle"); Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
            await runtime.ShowReminder(); runtime.StartBreak(); await Until(() => pet.ActiveAnimation == "walk");
            Assert.True(pet.IsRoaming); runtime.Stop(); await Until(() => pet.ActiveAnimation == "idle");
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = false }); await pet.React("click"); Assert.False(pet.IsVisible);
        }
        finally { runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
    [AvaloniaFact]
    public async Task PausingDiscardsAnInFlightCompletion()
    {
        var clip = GlbModel.Parse(Fixture()).CreateAnimation("Idle", new("model.glb"), "Idle");
        using var view = new AnimationView { Width = 192, Height = 192 };
        var window = new Window { Width = 192, Height = 192, Content = view };
        var completed = 0; view.Completed += () => completed++;
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); view.SetFrames(clip, false, false);
            ((DispatcherTimer)typeof(AnimationView).GetField("timer", flags)!.GetValue(view)!).Stop();
            await Task.Delay(250, TestContext.Current.CancellationToken);
            var rendering = (Task)typeof(AnimationView).GetMethod("AdvanceLive", flags)!.Invoke(view, new object[] { false })!;
            view.SetRunning(false); await rendering;
            Assert.Equal(0, completed);
            view.SetRunning(true);
            await (Task)typeof(AnimationView).GetMethod("AdvanceLive", flags)!.Invoke(view, new object[] { false })!;
            Assert.Equal(1, completed);
        }
        finally { view.Dispose(); window.Close(); }
    }
    [AvaloniaFact]
    public void LiveViewUsesPaintedPixelsAndIgnoresMirroringAndStaleClipSwaps()
    {
        var model = GlbModel.Parse(Fixture()); var clip = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        using var view = new AnimationView { Width = 192, Height = 192 }; var window = new Window { Width = 192, Height = 192, Content = view };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); view.SetFrames(clip, false, false);
            Assert.False(view.OpaqueAt(new Point(0, 0))); Assert.True(view.OpaqueAt(new Point(96, 130)));
            view.SetPose(PetPose.Neutral, true);
            Assert.True(((Avalonia.Media.MatrixTransform)view.RenderTransform!).Matrix.M11 > 0);
            view.SetRunning(false); view.SetFrames(clip, true, false);
            var timer = (DispatcherTimer)typeof(AnimationView).GetField("timer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
            Assert.False(timer.IsEnabled); view.Dispose(); view.SetFrames(clip, true); Assert.False(view.OpaqueAt(new Point(96, 130)));
        }
        finally { window.Close(); }
    }
}
