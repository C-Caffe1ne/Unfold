using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbPreviewHeightTests
{
    private const int Size = 256;
    private static byte[] Box(float width, float height, float depth, float units = 1, Vector3 offset = default)
    {
        Vector3[] corners = [new(-width / 2, 0, -depth / 2), new(width / 2, 0, -depth / 2),
            new(width / 2, height, -depth / 2), new(-width / 2, height, -depth / 2),
            new(-width / 2, 0, depth / 2), new(width / 2, 0, depth / 2),
            new(width / 2, height, depth / 2), new(-width / 2, height, depth / 2)];
        int[] triangles = [0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1,
            3, 2, 6, 3, 6, 7, 0, 3, 7, 0, 7, 4, 1, 5, 6, 1, 6, 2];
        using var binary = new MemoryStream();
        using (var writer = new BinaryWriter(binary, System.Text.Encoding.UTF8, leaveOpen: true))
            foreach (var index in triangles)
            {
                var point = corners[index] * units + offset;
                writer.Write(point.X); writer.Write(point.Y); writer.Write(point.Z);
            }
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)binary.Length } },
            bufferViews = new[] { new { buffer = 0, byteOffset = 0, byteLength = (int)binary.Length } },
            accessors = new[] { new { bufferView = 0, componentType = 5126, count = triangles.Length, type = "VEC3" } },
            meshes = new[] { new { primitives = new[] { new { attributes = new { POSITION = 0 } } } } },
            nodes = new[] { new { name = "Body", mesh = 0 } }, scenes = new[] { new { nodes = new[] { 0 } } }, scene = 0
        });
        var jsonSize = (json.Length + 3) / 4 * 4;
        using var output = new MemoryStream(); using var encoded = new BinaryWriter(output);
        encoded.Write(0x46546C67u); encoded.Write(2u); encoded.Write(28 + jsonSize + (int)binary.Length);
        encoded.Write(jsonSize); encoded.Write(0x4E4F534Au); encoded.Write(json);
        for (var i = json.Length; i < jsonSize; i++) encoded.Write((byte)' ');
        encoded.Write((int)binary.Length); encoded.Write(0x004E4942u); encoded.Write(binary.ToArray());
        return output.ToArray();
    }
    private static GlbAnimationFrames HeightPreview(GlbModel model, GlbDefinition? definition = null)
        => model.CreateAnimation(model.Animations[0].Name, definition ?? new("model.glb"), model.Animations[0].Name,
            normalizePreviewHeight: true);
    private static PixelImage Render(GlbAnimationFrames frames, GlbRotation rotation, int size = Size)
    {
        var pixels = new uint[size * size]; frames.RenderPreviewFrameInto(0, size, pixels, rotation);
        return new(size, size, pixels);
    }
    private static (int X, int Y, int Width, int Height) OpaqueBounds(PixelImage image)
    {
        var left = image.Width; var top = image.Height; var right = -1; var bottom = -1;
        for (var y = 0; y < image.Height; y++) for (var x = 0; x < image.Width; x++)
            if (image.Pixels[y * image.Width + x] >> 24 >= 128)
            { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        Assert.True(right >= left && bottom >= top, "The model must have visible pixels.");
        return (left, top, right - left + 1, bottom - top + 1);
    }

    [Fact]
    public void DifferentWidthsDepthsAndUnitsHaveTheSameNeutralPixelHeight()
    {
        var shapes = new[] { (.4f, .2f, 1f), (1f, .2f, 1f), (.4f, 2f, 1f), (.4f, .2f, .01f), (.4f, .2f, 1000f) };
        var heights = shapes.Select(shape => OpaqueBounds(Render(HeightPreview(
            GlbModel.Parse(Box(shape.Item1, 1, shape.Item2, shape.Item3))), new())).Height).ToArray();
        Assert.All(heights, height => Assert.InRange(height, 198, 201));
        Assert.InRange(heights.Max() - heights.Min(), 0, 1);
    }

    [Fact]
    public void RotationKeepsScaleInsteadOfFittingEachNewProjection()
    {
        var frames = HeightPreview(GlbModel.Parse(Box(.6f, 1, .2f)));
        var front = OpaqueBounds(Render(frames, new()));
        foreach (var yaw in new[] { -135, -90, -45, 45, 90, 135, 180 })
            Assert.InRange(Math.Abs(OpaqueBounds(Render(frames, new(0, yaw, 0))).Height - front.Height), 0, 1);
        // A quarter turn projects depth or width onto the screen's Y axis. Keeping
        // these proportions proves rotation did not silently zoom back to full height.
        var pitched = OpaqueBounds(Render(frames, new(90, 0, 0)));
        var rolled = OpaqueBounds(Render(frames, new(0, 0, 90)));
        Assert.InRange(pitched.Height, front.Height * .2 - 2, front.Height * .2 + 2);
        Assert.InRange(rolled.Height, front.Height * .6 - 2, front.Height * .6 + 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void InitialIndexedLiveAndTerminalFramesUseTheSameHeightFraming(bool explicitRotation)
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var definition = new GlbDefinition("model.glb", 25, Rotation: explicitRotation ? new(15, 25, 10) : null);
        var frames = HeightPreview(model, definition); var orientation = definition.EffectiveRotation;
        Assert.Equal(Render(frames, orientation, 192).Pixels, frames[0].Image.Pixels);
        var expected = new uint[Size * Size]; var actual = new uint[expected.Length];
        foreach (var index in new[] { 0, frames.Count / 2 })
        {
            frames.RenderPreviewFrameInto(index, Size, expected, orientation);
            frames.RenderFrameInto(index, Size, actual);
            Assert.Equal(expected, actual); Assert.Equal(expected, frames.GetFrame(index, Size).Image.Pixels);
        }
        frames.RenderPreviewFrameInto(frames.Count - 1, Size, expected, orientation, terminal: true);
        frames.RenderTerminalFrameInto(Size, actual); Assert.Equal(expected, actual);
        Assert.NotEqual(Render(frames, orientation).Pixels, actual);
    }

    [Fact]
    public void OffCenterModelsKeepTheSameFramingWhenRotated()
    {
        var centered = HeightPreview(GlbModel.Parse(Box(.4f, 1, .25f)));
        var shifted = HeightPreview(GlbModel.Parse(Box(.4f, 1, .25f, offset: new(3, -2, 5))));
        foreach (var rotation in new[] { new GlbRotation(), new(20, 35, 10), new(-25, 100, -20), new(0, 180, 0) })
        {
            var expected = OpaqueBounds(Render(centered, rotation)); var actual = OpaqueBounds(Render(shifted, rotation));
            Assert.InRange(Math.Abs(expected.X - actual.X), 0, 1); Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, 1);
            Assert.InRange(Math.Abs(expected.Width - actual.Width), 0, 1); Assert.InRange(Math.Abs(expected.Height - actual.Height), 0, 1);
            Assert.InRange(actual.X + (actual.Width - 1) / 2d, 126, 129);
            Assert.InRange(actual.Y + (actual.Height - 1) / 2d, 129, 132);
        }
    }

    [Theory]
    [InlineData(0f)] [InlineData(.000001f)] [InlineData(.000015f)]
    public void FlatReferencesFallBackToTheExistingBoundedFraming(float height)
    {
        var model = GlbModel.Parse(Box(.8f, height, .6f)); var rotation = new GlbRotation(90, 0, 0);
        var definition = new GlbDefinition("model.glb", Rotation: rotation);
        var normalized = HeightPreview(model, definition);
        var regular = model.CreateAnimation("Static", definition, "Static");
        var expected = regular.GetFrame(0, Size).Image;
        var actual = Render(normalized, rotation);
        Assert.Equal(expected.Pixels, actual.Pixels);
        var bounds = OpaqueBounds(actual);
        Assert.InRange(bounds.Width, 1, Size - 1); Assert.InRange(bounds.Height, 1, Size - 1);
    }

    [AvaloniaFact]
    public async Task SavedAndReopenedEditorKeepsHeightFramingWhileRuntimeRetainsItsDefault()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "pet.glb"); var bytes = Box(.8f, 1, .6f);
        File.WriteAllBytes(path, bytes); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var package = new GlbPetDraft(path).Save(library);
        var window = new Window { Width = 1120, Height = 800 };
        using var page = new GlbPetView(window, library, _ => Task.CompletedTask, () => Task.FromResult<string?>(path));
        T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
        GlbAnimationFrames Frames() => (GlbAnimationFrames)typeof(AnimationView).GetField("live", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Find<AnimationView>("GlbPreview"))!;
        var rotation = new GlbRotation(0, 35, 0);
        try
        {
            window.Content = Ui.PageFrame(window, page); window.Show(); window.UpdateLayout();
            await page.OpenPackage(package);
            await Until(() => !page.IsPreviewLoading && Find<AnimationView>("GlbPreview").RenderedGlbPreviewRotation == new GlbRotation());
            var initialHeight = OpaqueBounds(Frames()[0].Image).Height;
            Assert.InRange(initialHeight, 148, 151);
            Find<NumericUpDown>("GlbRotationY").Value = 35;
            await Until(() => Find<AnimationView>("GlbPreview").RenderedGlbPreviewRotation == rotation);
            var beforeSave = Render(Frames(), rotation, 192);
            Find<Button>("SaveGlbPet").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !page.IsBusy);
            Assert.False(page.HasUnsavedChanges, Find<TextBlock>("GlbStatus").Text);
            package = library.List().Single();
            Assert.Equal(rotation, package.Manifest.Animations["idle"].Rotation);
            await page.OpenPackage(package);
            await Until(() => !page.IsPreviewLoading && Find<AnimationView>("GlbPreview").RenderedGlbPreviewRotation == rotation);
            Assert.Equal(beforeSave.Pixels, Frames()[0].Image.Pixels);
            Assert.Equal(initialHeight, OpaqueBounds(Frames()[0].Image).Height);
            var runtime = Assert.IsType<GlbAnimationFrames>(package.LoadAnimation("idle"));
            var draft = new GlbPetDraft(package);
            Assert.Equal(runtime[0].Image.Pixels, draft.Preview("idle")[0].Image.Pixels);
            Assert.True(OpaqueBounds(runtime[0].Image).Height < initialHeight - 20);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { page.Dispose(); window.Close(); }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 500 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
}
