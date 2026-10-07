using System.Runtime.CompilerServices;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class MemoryBudgetTests
{
    private static IReadOnlyList<AnimationFrame> Frames(int pixels = 1) =>
        new[] { new AnimationFrame(new PixelImage(pixels, 1, new uint[pixels]), TimeSpan.FromMilliseconds(100)) };

    [Fact]
    public async Task ClipCacheBoundsRetainedPixelsAndSharesOversizedActiveClips()
    {
        var cache = new AnimationClipCache(8);
        await cache.Get("first", () => Frames(2));
        await cache.Get("second", () => Frames(2));
        Assert.Equal(8, cache.RetainedBytes);
        var decodes = 0;
        var big = await cache.Get("big", () => { decodes++; return Frames(4); });
        var preview = await cache.Get("big", () => { decodes++; return Frames(4); });
        Assert.Same(big, preview); Assert.Equal(1, decodes);
        Assert.InRange(cache.RetainedBytes, 0, 8);
        cache.Clear(); Assert.Equal(0, cache.RetainedBytes);
        Assert.NotSame(big, await cache.Get("big", () => Frames(4)));
    }

    [Fact]
    public async Task PendingDecodeIsSharedAndClearPreventsItsLateReinsertion()
    {
        var cache = new AnimationClipCache(8);
        using var ready = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var old = cache.Get("clip", () => { ready.Set(); release.Wait(); return Frames(); });
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(old, cache.Get("clip", () => throw new InvalidOperationException("Duplicate decoder.")));
            cache.Clear();
        }
        finally { release.Set(); }
        await old; Assert.Equal(0, cache.RetainedBytes);
        var replacement = cache.Get("clip", () => Frames());
        Assert.NotSame(old, replacement);
        await replacement;
    }

    [Fact]
    public async Task JustCompletedEvictedClipRemainsSharedWithItsActiveViewer()
    {
        var cache = new AnimationClipCache(12);
        var first = await cache.Get("first", () => Frames());
        using var ready = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var pending = cache.Get("second", () => { ready.Set(); release.Wait(); return Frames(3); });
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Same(first, await cache.Get("first", () => throw new InvalidOperationException("First was cached.")));
        }
        finally { release.Set(); }
        var second = await pending;
        Assert.Same(second, await cache.Get("second", () => throw new InvalidOperationException("Active clip was decoded twice.")));
        Assert.Equal(4, cache.RetainedBytes);
    }

    [Fact]
    public async Task FailedDecodeCanRetryAndRepeatedPixelBuffersCountOnce()
    {
        var cache = new AnimationClipCache(8);
        await Assert.ThrowsAsync<InvalidDataException>(() => cache.Get("broken", () => throw new InvalidDataException()));
        var frames = Frames(2);
        var repeated = Enumerable.Repeat(frames[0], 512).ToArray();
        Assert.Same(repeated, await cache.Get("broken", () => repeated));
        Assert.Equal(8, cache.RetainedBytes);
    }

    [Fact]
    public void SpriteTimelineSharesRepeatedCellsWithoutChangingFramesOrDuration()
    {
        using var temp = new TempDirectory();
        var manifest = new CharacterManifest("memory-sprite", "memory", 1, new("spritesheet.png", 2, 1, 1, 1),
            new() { ["idle"] = new(Frames: [0, 1, 0, 1], Fps: 20) });
        File.WriteAllBytes(Path.Combine(temp.Path, "spritesheet.png"), ImageCodec.EncodePng(new(2, 1, [0xFFFF0000u, 0xFF0000FFu])));
        File.WriteAllText(Path.Combine(temp.Path, "character.json"), System.Text.Json.JsonSerializer.Serialize(manifest, CharacterLibrary.JsonOptions));
        var package = CharacterLibrary.LoadPackage(temp.Path);
        var frames = package.LoadAnimation("idle");
        Assert.Equal(4, frames.Count); Assert.Same(frames[0].Image, frames[2].Image); Assert.Same(frames[1].Image, frames[3].Image);
        Assert.Equal(new uint[] { 0xFFFF0000u, 0xFF0000FFu, 0xFFFF0000u, 0xFF0000FFu }, frames.Select(frame => frame.Image.Pixels[0]));
        Assert.All(frames, frame => Assert.Equal(TimeSpan.FromMilliseconds(50), frame.Duration));
        var weak = SheetReference(package);
        // A reachability test, separate from all native memory benchmarks (which do not force GC).
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        File.WriteAllBytes(Path.Combine(temp.Path, "spritesheet.png"), ImageCodec.EncodePng(new(1, 1, [0xFF00FF00u])));
        Assert.Throws<InvalidDataException>(() => package.Sheet);
        File.WriteAllBytes(Path.Combine(temp.Path, "spritesheet.png"), ImageCodec.EncodePng(new(2, 1, [0xFFFF0000u, 0xFF0000FFu])));
        Assert.Equal(0xFFFF0000u, package.Sheet.Pixels[0]);
        File.Delete(Path.Combine(temp.Path, "spritesheet.png"));
        Assert.Equal(0xFFFF0000u, Assert.Single(package.StillImage.Pixels));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PixelImage> SheetReference(CharacterPackage package) => new(package.Sheet);

    [Fact]
    public void SavedPackageRetainsValidatedFallbackAfterItsSheetIsRemoved()
    {
        using var temp = new TempDirectory();
        var library = new CharacterLibrary(temp.Path);
        var document = new PixelDocument(2, 1) { Name = "fallback" };
        document.Layers[0].Frames[0] = [0xFFFF0000u, 0xFF0000FFu];
        var package = library.Save(document);
        File.Delete(Path.Combine(package.DirectoryPath, package.Manifest.SpriteSheet.File));
        Assert.Equal(new uint[] { 0xFFFF0000u, 0xFF0000FFu }, package.StillImage.Pixels);
    }

    [Fact]
    public void WarmModelValidationReadsContentWithoutAllocatingAnotherFullGlbBuffer()
    {
        using var temp = new TempDirectory();
        var original = GlbTests.Fixture();
        var bytes = new byte[original.Length + 8 + 4 * 1024 * 1024];
        original.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)bytes.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(original.Length), 4 * 1024 * 1024);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(original.Length + 4), 0xDEADBEEF);
        var path = Path.Combine(temp.Path, "large.glb"); File.WriteAllBytes(path, bytes);
        var model = GlbModel.Load(path); _ = GlbModel.Load(path);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 3; i++) Assert.Same(model, GlbModel.Load(path));
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 128 * 1024);
    }

    [Fact]
    public void ImportedClipReusesActiveFramesAndThumbnailCannotChangeTheExportSnapshot()
    {
        var clip = ImportedPetClip.FromGif("pet.gif", File.ReadAllBytes(CustomPetDraftTests.Fixture()));
        var frames = clip.LoadFrames();
        Assert.Same(frames, clip.LoadFrames());
        var thumbnail = clip.Thumbnail;
        Assert.Equal(frames[0].Image.Pixels, thumbnail.Pixels);
        Array.Fill(thumbnail.Pixels, 0u);
        Assert.Equal(frames[0].Image.Pixels, clip.Thumbnail.Pixels);
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = clip.Thumbnail;
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, (long)clip.Width * clip.Height * 4 + 512);
    }

    [Fact]
    public void IdenticalGifContentSharesDecodedPixelsAcrossRuntimeAndDraftImports()
    {
        var bytes = File.ReadAllBytes(CustomPetDraftTests.Fixture());
        var runtime = ImageCodec.DecodeGif(bytes);
        var draft = ImportedPetClip.FromGif("different-name.gif", bytes.ToArray());
        Assert.Same(runtime, draft.LoadFrames());
        Array.Fill(bytes, (byte)0);
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodeGif(bytes));
        Assert.True(runtime.Count > 1);
    }
}
