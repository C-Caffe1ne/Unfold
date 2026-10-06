using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbPipelineTests
{
    internal static byte[] DenseFixture(int samples = 12000, string interpolation = "LINEAR")
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        var views = new List<object>(); var accessors = new List<object>();
        int Data(float[] data, string type, int count)
        {
            var offset = (int)buffer.Position; foreach (var value in data) writer.Write(value);
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = data.Length * 4 });
            accessors.Add(new { bufferView = views.Count - 1, componentType = 5126, count, type }); return accessors.Count - 1;
        }
        var positions = Data([-.4f, 0, 0, .4f, 0, 0, 0, 1, 0], "VEC3", 3);
        var time = Data(Enumerable.Range(0, samples).Select(i => i * 2f / (samples - 1)).ToArray(), "SCALAR", samples);
        var channels = new List<object>(); var samplers = new List<object>();
        for (var node = 0; node < 8; node++)
        {
            var cubic = interpolation == "CUBICSPLINE"; var values = new float[samples * 3 * (cubic ? 3 : 1)];
            for (var i = 0; i < samples; i++) values[(i * (cubic ? 3 : 1) + (cubic ? 1 : 0)) * 3] = MathF.Sin(i * .001f) * .1f;
            var output = Data(values, "VEC3", values.Length / 3);
            channels.Add(new { sampler = node, target = new { node, path = "translation" } });
            samplers.Add(new { input = time, output, interpolation });
        }
        var root = new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)buffer.Length } }, bufferViews = views, accessors,
            nodes = Enumerable.Range(0, 8).Select(i => new { name = "Part" + i, mesh = 0 }).ToArray(),
            meshes = new[] { new { primitives = new[] { new { attributes = new { POSITION = positions } } } } },
            scenes = new[] { new { nodes = Enumerable.Range(0, 8).ToArray() } }, scene = 0,
            animations = new[] { new { name = "Motion", channels, samplers } }
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(root); var jsonSize = (json.Length + 3) / 4 * 4;
        using var outputStream = new MemoryStream(); using var outputWriter = new BinaryWriter(outputStream);
        outputWriter.Write(0x46546C67u); outputWriter.Write(2u); outputWriter.Write(28 + jsonSize + (int)buffer.Length);
        outputWriter.Write(jsonSize); outputWriter.Write(0x4E4F534Au); outputWriter.Write(json);
        for (var i = json.Length; i < jsonSize; i++) outputWriter.Write((byte)' ');
        outputWriter.Write((int)buffer.Length); outputWriter.Write(0x004E4942u); outputWriter.Write(buffer.ToArray());
        return outputStream.ToArray();
    }

    [Fact]
    public void DenseAnimationLoadingDoesNotAllocateOneArrayPerKey()
    {
        _ = GlbModel.Parse(DenseFixture(8)); var bytes = DenseFixture();
        var before = GC.GetAllocatedBytesForCurrentThread(); var model = GlbModel.Parse(bytes);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, bytes.Length * 2L);
        Assert.Equal(1, model.TriangleCount); Assert.Equal(8, model.NodeNames.Count); GC.KeepAlive(model);
    }

    [Fact]
    public void ModelLoadingSharesContentAcrossPathsAndInvalidatesChangedBytes()
    {
        using var temp = new TempDirectory(); var firstPath = Path.Combine(temp.Path, "first.glb"); var secondPath = Path.Combine(temp.Path, "second.glb");
        var bytes = GlbTests.Fixture(morph: true); File.WriteAllBytes(firstPath, bytes); File.WriteAllBytes(secondPath, bytes);
        var first = GlbModel.Load(firstPath); var second = GlbModel.Load(secondPath);
        Assert.Same(first, second);
        var stamp = File.GetLastWriteTimeUtc(firstPath); var edited = bytes.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(edited.AsSpan(edited.Length - 4), .5f);
        File.WriteAllBytes(firstPath, edited); File.SetLastWriteTimeUtc(firstPath, stamp);
        var changed = GlbModel.Load(firstPath); Assert.NotSame(first, changed);
        Assert.NotEqual(first.Render("Idle", .1, new("model.glb"), "Idle").Pixels, changed.Render("Idle", .1, new("model.glb"), "Idle").Pixels);
        Assert.Same(second, GlbModel.Load(secondPath));
    }

    [Fact]
    public void SharedModelCacheDoesNotKeepUnusedModelsAlive()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "unused.glb"); File.WriteAllBytes(path, DenseFixture(15));
        var weak = LoadWeak(path);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static WeakReference LoadWeak(string path) => new(GlbModel.Load(path));

    [Fact]
    public void PackageMetadataDoesNotKeepAnUnusedDecodedModelAlive()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "unselected.glb");
        File.WriteAllBytes(path, DenseFixture(17)); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var package = MakePackage(path, library); var weak = PackageModelWeak(package);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        Assert.Equal("Motion", package.Model.Animations[0].Name);
        GC.KeepAlive(package);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static CharacterPackage MakePackage(string path, CharacterLibrary library) => new GlbPetDraft(path).Save(library);
    [MethodImpl(MethodImplOptions.NoInlining)] private static WeakReference PackageModelWeak(CharacterPackage package) => new(package.Model);

    [Fact]
    public void MetadataListsDoNotDecodeModelsAndFirstUseStillValidates()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "pet.glb");
        File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
        var draft = new GlbPetDraft(path); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var installed = draft.Save(library);
        var listed = Assert.Single(library.List(loadModels: false));
        var modelField = typeof(CharacterPackage).GetField("model", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(modelField.GetValue(listed));
        Assert.Same(draft.Model, listed.Model);
        Assert.Same(draft.Model, new GlbPetDraft(listed).Model);
        Assert.Same(draft.Model, installed.Model);
        File.WriteAllBytes(Path.Combine(listed.DirectoryPath, "model.glb"), new byte[64]);
        var invalid = Assert.Single(library.List(loadModels: false));
        Assert.Throws<InvalidDataException>(() => invalid.LoadAnimation("idle"));
        Assert.Empty(library.List());
    }

    [Theory]
    [InlineData("LINEAR")]
    [InlineData("STEP")]
    [InlineData("CUBICSPLINE")]
    public void ConsumerOwnedRenderingHasNoFrameSizedAllocations(string interpolation)
    {
        var model = GlbModel.Parse(DenseFixture(24, interpolation));
        var clip = model.CreateAnimation("Motion", new("model.glb"), "Motion");
        const int size = 576; var pixels = new uint[size * size];
        for (var i = 0; i < 4; i++) clip.RenderFrameInto(i, size, pixels);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 40; i++) clip.RenderFrameInto(i % clip.Count, size, pixels);
        Assert.InRange((GC.GetAllocatedBytesForCurrentThread() - before) / 40, 0, 1024);
        Assert.Equal(clip.GetFrame(39 % clip.Count, size).Image.Pixels, pixels);
    }

    [Fact]
    public async Task OwnedOutputsRemainIndependentAcrossConcurrentPosesAndClearOldPixels()
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var first = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        var second = model.CreateAnimation("Idle", new("model.glb", 45), "Idle");
        var one = Enumerable.Repeat(uint.MaxValue, 192 * 192).ToArray(); var two = new uint[384 * 384];
        var immutable = first.GetFrame(1, 192).Image; var snapshot = immutable.Pixels.ToArray();
        for (var i = 0; i < 6; i++)
        {
            await Task.WhenAll(Task.Run(() => first.RenderFrameInto(2, 192, one), TestContext.Current.CancellationToken), Task.Run(() => second.RenderFrameInto(3, 384, two), TestContext.Current.CancellationToken));
            Assert.Equal(first.GetFrame(2, 192).Image.Pixels, one);
            Assert.Equal(second.GetFrame(3, 384).Image.Pixels, two);
            first.RenderFrameInto(0, 192, one);
            Assert.Equal(first[0].Image.Pixels, one);
        }
        Assert.Equal(snapshot, immutable.Pixels);
        Assert.Throws<ArgumentException>(() => first.RenderFrameInto(1, 192, new uint[1]));
    }

    [AvaloniaFact]
    public void BothPetPickersLeaveUnselectedModelsUndecoded()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        for (var i = 0; i < 3; i++)
        {
            var path = Path.Combine(temp.Path, $"model-{i}.glb"); File.WriteAllBytes(path, DenseFixture(10 + i));
            new GlbPetDraft(path).Save(library);
        }
        var owner = new Window { Width = 1000, Height = 800 };
        using var builder = new PetBuilderView(owner, library, _ => Task.CompletedTask, _ => Task.CompletedTask);
        try
        {
            owner.Content = builder; owner.Show(); owner.UpdateLayout();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var media = (ComboBox)typeof(PetBuilderView).GetField("pets", flags)!.GetValue(builder)!;
            var glb = typeof(PetBuilderView).GetField("glb", flags)!.GetValue(builder)!;
            var existing = (ComboBox)glb.GetType().GetField("existing", flags)!.GetValue(glb)!;
            foreach (var picker in new[] { media, existing })
            {
                var packages = picker.ItemsSource!.OfType<CharacterPackage>().ToArray(); Assert.Equal(3, packages.Length);
                Assert.All(packages, package => Assert.Null(typeof(CharacterPackage).GetField("model", flags)!.GetValue(package)));
            }
        }
        finally { owner.Close(); }
    }

    [AvaloniaFact]
    public async Task DeferredRuntimeListKeepsFallbackForADamagedSelectedModel()
    {
        using var temp = new TempDirectory(); var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        try
        {
            var path = Path.Combine(temp.Path, "source.glb"); File.WriteAllBytes(path, GlbTests.Fixture(morph: true));
            var selected = new GlbPetDraft(path).Save(runtime.Library);
            await runtime.Start(true, true); runtime.Stop(); await runtime.SelectInstalledCharacter(selected);
            File.WriteAllBytes(Path.Combine(selected.DirectoryPath, "model.glb"), new byte[64]);
            await runtime.Reload();
            Assert.NotNull(runtime.Selected); Assert.True(runtime.Selected.IsBuiltIn);
            Assert.DoesNotContain(runtime.Characters, p => p.Manifest.Id == selected.Manifest.Id);
        }
        finally { runtime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [AvaloniaFact]
    public async Task LivePlaybackReusesBitmapAndOwnsItsPixels()
    {
        var clip = GlbModel.Parse(GlbTests.Fixture(morph: true)).CreateAnimation("Idle", new("model.glb"), "Idle");
        var immutableFirst = clip[0].Image; var snapshot = immutableFirst.Pixels.ToArray();
        using var view = new AnimationView { Width = 192, Height = 192 };
        var window = new Window { Width = 192, Height = 192, Content = view };
        try
        {
            window.Show(); window.UpdateLayout(); view.SetFrames(clip, true, false);
            var field = typeof(AnimationView).GetField("bitmaps", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var bitmap = ((Bitmap[])field.GetValue(view)!)[0];
            var imageField = typeof(AnimationView).GetField("liveImage", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var observed = new HashSet<string>();
            for (var i = 0; i < 16; i++)
            {
                await Task.Delay(25, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
                var current = (PixelImage)imageField.GetValue(view)!;
                var pixels = new int[current.Pixels.Length];
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                try { bitmap.CopyPixels(new Avalonia.PixelRect(0, 0, current.Width, current.Height), handle.AddrOfPinnedObject(), pixels.Length * 4, current.Width * 4); }
                finally { handle.Free(); }
                Assert.Equal(current.Pixels, pixels.Select(p => unchecked((uint)p)));
                observed.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(current.Pixels.AsSpan()))));
            }
            Assert.Same(bitmap, ((Bitmap[])field.GetValue(view)!)[0]);
            Assert.Equal(snapshot, immutableFirst.Pixels);
            Assert.True(observed.Count > 1, "Reused bitmap must display changing frames.");
        }
        finally { view.Dispose(); window.Close(); }
    }
}
