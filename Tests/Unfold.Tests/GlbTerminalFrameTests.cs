using System.Reflection;
using Unfold.Core;

namespace Unfold.Tests;

public class GlbTerminalFrameTests
{
    [Theory]
    [InlineData(.25, .2f)]
    [InlineData(1.25, .2f)]
    [InlineData(3, .2f)]
    [InlineData(3, .02f)]
    public void TerminalRenderingPreservesLoopSamplesAndConsumerOwnedBuffers(double speed, float duration)
    {
        var model = GlbModel.Parse(GlbTests.Fixture(rootMotion: false, morph: true, duration: duration));
        var definition = new GlbDefinition("model.glb");
        var clip = model.CreateAnimation("Idle", definition, "Idle", speed);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var bounds = typeof(GlbModel).GetMethod("Bounds", flags)!.Invoke(model, [definition, "Idle"]);
        PixelImage Sample(double seconds, int size) => (PixelImage)typeof(GlbModel).GetMethod("RenderAntialiased", flags)!.Invoke(model,
            ["Idle", seconds, definition, "Idle", bounds!, size])!;
        var count = clip.Count; var frameDuration = clip.FrameDuration;
        var immutable = clip.GetFrame(count - 1, 192).Image; var snapshot = immutable.Pixels.ToArray();
        var pixels = new uint[192 * 192];
        clip.RenderTerminalFrameInto(192, pixels);
        Assert.Equal(Sample(model.Animations[0].Duration, 192).Pixels, pixels);
        Assert.NotEqual(snapshot, pixels);
        // The regular frame API still samples before the wrap boundary.
        clip.RenderFrameInto(count - 1, 192, pixels);
        Assert.Equal(Sample((count - 1) * frameDuration.TotalSeconds * speed, 192).Pixels, pixels);
        Assert.Equal(snapshot, immutable.Pixels);
        Assert.Equal(count, clip.Count); Assert.Equal(frameDuration, clip.FrameDuration);
        var other = new uint[384 * 384]; clip.RenderTerminalFrameInto(384, other);
        Assert.Equal(Sample(model.Animations[0].Duration, 384).Pixels, other);
        var otherSnapshot = other.ToArray();
        for (var i = 0; i < 4; i++) clip.RenderTerminalFrameInto(192, pixels);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++) clip.RenderTerminalFrameInto(192, pixels);
        Assert.InRange((GC.GetAllocatedBytesForCurrentThread() - before) / 20, 0, 1024);
        Assert.Equal(otherSnapshot, other); Assert.Equal(snapshot, immutable.Pixels);
        Assert.Throws<ArgumentException>(() => clip.RenderTerminalFrameInto(192, new uint[1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => clip.RenderTerminalFrameInto(GlbAnimationFrames.MaxFrameSize + 1, pixels));
    }
}
