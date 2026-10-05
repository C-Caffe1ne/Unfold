using Unfold.Core;

namespace Unfold.Tests;

public class GlbMemoryTests
{
    [Theory]
    [InlineData(192)]
    [InlineData(576)]
    [InlineData(1024)]
    public void WarmRenderingAllocatesOnlyTheOutputImageAndSmallPoseData(int size)
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var clip = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        _ = clip.GetFrame(1, size);
        const int frames = 12;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < frames; i++) _ = clip.GetFrame(i % 2 + 1, size);
        var perFrame = (GC.GetAllocatedBytesForCurrentThread() - before) / frames;
        // The immutable output must remain independently owned. The 2x color,
        // depth and mesh scratch buffers must not be reallocated per frame.
        Assert.InRange(perFrame, 0, (long)size * size * 4 + 64 * 1024);
    }

    [Fact]
    public void AlternatingPreviewAndPetSizesShareOneWarmWorkspaceAcrossClips()
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var pet = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        var preview = model.CreateAnimation("Idle", new("model.glb", 90), "Idle");
        _ = pet.GetFrame(1, 576); _ = preview.GetFrame(1, 192);
        const int pairs = 8;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < pairs; i++)
        {
            _ = pet.GetFrame(i % 2 + 1, 576);
            _ = preview.GetFrame(i % 2 + 1, 192);
        }
        var perPair = (GC.GetAllocatedBytesForCurrentThread() - before) / pairs;
        Assert.InRange(perPair, 0, (576L * 576 + 192L * 192) * 4 + 128 * 1024);
    }

    [Fact]
    public async Task SharedWorkspaceDoesNotMixConcurrentPosesOrChangeReturnedImages()
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var forward = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        var angled = model.CreateAnimation("Idle", new("model.glb", 45), "Idle");
        var original = forward.GetFrame(1, 192).Image;
        var snapshot = original.Pixels.ToArray();
        var expectations = new[] { forward.GetFrame(2, 384).Image.Pixels.ToArray(), angled.GetFrame(3, 192).Image.Pixels.ToArray() };
        // Evict the one-frame caches and submit distinct clips concurrently.
        for (var i = 0; i < 8; i++)
        {
            _ = forward.GetFrame(1, 192); _ = angled.GetFrame(1, 384);
            var images = await Task.WhenAll(Task.Run(() => forward.GetFrame(2, 384)), Task.Run(() => angled.GetFrame(3, 192)));
            Assert.Equal(expectations[0], images[0].Image.Pixels);
            Assert.Equal(expectations[1], images[1].Image.Pixels);
        }
        Assert.Equal(snapshot, original.Pixels);
    }
}
