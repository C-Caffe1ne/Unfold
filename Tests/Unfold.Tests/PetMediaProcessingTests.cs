using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public sealed class PetMediaProcessingTests
{
    private const uint Green = 0xFF00B140, DarkGreen = 0xFF005A20, White = 0xFFFFFFFF, Black = 0xFF000000, Skin = 0xFFF2C9A0;

    private static PixelImage Frame(int width, int height, uint background, params (int X, int Y, int W, int H, uint Color)[] boxes)
    {
        var pixels = Enumerable.Repeat(background, width * height).ToArray();
        foreach (var (x0, y0, w, h, color) in boxes)
            for (var y = y0; y < y0 + h; y++) for (var x = x0; x < x0 + w; x++) pixels[y * width + x] = color;
        return new(width, height, pixels);
    }

    private static uint At(PixelImage image, int x, int y) => image.Pixels[y * image.Width + x];

    [Fact]
    public void TheKeyColorIsTheMostCommonBorderColor()
    {
        var frame = Frame(40, 30, Green, (10, 5, 20, 20, White), (0, 0, 3, 3, Black));
        Assert.Equal(Green, PetMediaProcessing.DetectKeyColor(frame));
    }

    [Fact]
    public void RemovingTheBackgroundClearsTheScreenAndItsShadowsButKeepsThePet()
    {
        var frame = Frame(40, 30, Green, (10, 5, 20, 20, White), (12, 7, 4, 4, Black), (20, 20, 6, 3, Skin), (0, 26, 40, 4, DarkGreen));
        var settings = new ClipProcessing(RemoveBackground: true, KeyColor: Green);
        var result = PetMediaProcessing.Apply([new(frame, TimeSpan.FromMilliseconds(80))], settings).Single();
        Assert.Equal(0u, At(result.Image, 1, 1) >> 24);
        Assert.Equal(0u, At(result.Image, 5, 28) >> 24); // Floor shadow on the green screen.
        Assert.Equal(White, At(result.Image, 15, 15));
        Assert.Equal(Black, At(result.Image, 13, 8));
        Assert.Equal(Skin, At(result.Image, 22, 21));
        Assert.Equal(TimeSpan.FromMilliseconds(80), result.Duration);
    }

    [Fact]
    public void ToleranceControlsHowFarFromTheKeyIsRemoved()
    {
        var frame = Frame(20, 20, Green, (5, 5, 10, 10, DarkGreen), (16, 16, 2, 2, White));
        var strict = PetMediaProcessing.Apply([new(frame, TimeSpan.FromSeconds(1))], new(true, Green, Tolerance: 5)).Single().Image;
        var loose = PetMediaProcessing.Apply([new(frame, TimeSpan.FromSeconds(1))], new(true, Green, Tolerance: 60)).Single().Image;
        Assert.NotEqual(0u, At(strict, 10, 10) >> 24);
        Assert.Equal(0u, At(loose, 10, 10) >> 24);
    }

    [Fact]
    public void KeptEdgePixelsLoseTheGreenSpill()
    {
        const uint greenishWhite = 0xFFC8FFC8;
        var frame = Frame(20, 20, Green, (5, 5, 10, 10, greenishWhite));
        var image = PetMediaProcessing.Apply([new(frame, TimeSpan.FromSeconds(1))], new(true, Green)).Single().Image;
        var pixel = At(image, 10, 10);
        Assert.NotEqual(0u, pixel >> 24);
        Assert.True(((pixel >> 8) & 0xFF) <= Math.Max((pixel >> 16) & 0xFF, pixel & 0xFF) + 8);
    }

    [Fact]
    public void FeetAlignOnASquareCanvasWithoutScalingThePet()
    {
        var first = Frame(100, 50, 0, (2, 2, 10, 20, White));
        var second = Frame(100, 50, 0, (6, 4, 10, 20, White));
        var frames = PetMediaProcessing.Apply([new(first, TimeSpan.FromSeconds(1)), new(second, TimeSpan.FromSeconds(1))],
            new(AlignFeet: true));
        var image = frames[0].Image;
        Assert.Equal((100, 100), (image.Width, image.Height));
        var opaque = Enumerable.Range(0, image.Pixels.Length).Where(i => image.Pixels[i] >> 24 != 0).ToArray();
        Assert.Equal(200, opaque.Length); // Same pixel count: translated, not scaled.
        var bottom = frames.Max(frame => Enumerable.Range(0, frame.Image.Pixels.Length)
            .Where(i => frame.Image.Pixels[i] >> 24 != 0).Max(i => i / frame.Image.Width));
        Assert.Equal(100 - 1 - PetMediaProcessing.FootMargin(100), bottom);
        var minX = frames.Min(frame => Enumerable.Range(0, frame.Image.Pixels.Length).Where(i => frame.Image.Pixels[i] >> 24 != 0).Min(i => i % frame.Image.Width));
        var maxX = frames.Max(frame => Enumerable.Range(0, frame.Image.Pixels.Length).Where(i => frame.Image.Pixels[i] >> 24 != 0).Max(i => i % frame.Image.Width));
        Assert.InRange((minX + maxX) / 2, 46, 54);
    }

    [Fact]
    public void RemovingEveryPetPixelFromAFrameIsRejectedWithARecoveryHint()
    {
        var empty = Frame(20, 20, Green);
        var error = Assert.Throws<InvalidDataException>(() =>
            PetMediaProcessing.Apply([new(Frame(20, 20, Green, (5, 5, 5, 5, White)), TimeSpan.FromSeconds(1)), new(empty, TimeSpan.FromSeconds(1))],
                new(true, Green)));
        Assert.Contains(L.T("허용 범위"), error.Message);
    }

    // Two 1×1 frames, each with a Graphic Control Extension (10 cs) and a minimal LZW image.
    private static readonly byte[] TwoFrameGif =
    [
        (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 1, 0, 1, 0, 0x80, 0, 0,
        0, 0, 0, 255, 255, 255,
        0x21, 0xF9, 4, 0, 10, 0, 0, 0, 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0,
        0x21, 0xF9, 4, 0, 10, 0, 0, 0, 0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2, 0x44, 1, 0,
        0x3B
    ];

    [Fact]
    public void GifDelaysAreRewrittenPerFrameIncludingTheLast()
    {
        var timed = GifTiming.RewriteDelays(TwoFrameGif, [8, 25]);
        var frames = ImageCodec.DecodeGif(timed);
        Assert.Equal([80d, 250d], frames.Select(frame => frame.Duration.TotalMilliseconds));
        Assert.Throws<InvalidDataException>(() => GifTiming.RewriteDelays(TwoFrameGif, [8]));
        Assert.Throws<InvalidDataException>(() => GifTiming.RewriteDelays(TwoFrameGif[..^5], [8, 25]));
    }

    [Fact]
    public void InactiveSettingsReturnTheSameFrames()
    {
        IReadOnlyList<AnimationFrame> frames = [new(Frame(4, 4, White), TimeSpan.FromSeconds(1))];
        Assert.Same(frames, PetMediaProcessing.Apply(frames, ClipProcessing.None));
    }

    [Fact]
    public async Task StillImagesAreProcessedWithoutTheVideoTool()
    {
        var png = ImageCodec.EncodePng(Frame(40, 40, Green, (10, 10, 20, 20, White)));
        var source = ImportedPetClip.FromImage("still.png", png);
        var result = await PetMediaProcessor.Apply(source, new(true, Green), TestContext.Current.CancellationToken);
        Assert.True(result.IsStillImage); Assert.Equal("still.png", result.FileName);
        var image = result.LoadFrames().Single().Image;
        Assert.Equal(0u, image.Pixels[0] >> 24);
    }

    [Fact]
    public async Task TheDraftKeepsTheOriginalSoCleanupCanChangeOrBeUndone()
    {
        var png = ImageCodec.EncodePng(Frame(40, 40, Green, (10, 10, 20, 20, White)));
        var original = ImportedPetClip.FromImage("still.png", png);
        var draft = new CustomPetDraft(); draft.SetClip("idle", original);
        var settings = new ClipProcessing(true, Green, AlignFeet: true);
        var cleaned = await PetMediaProcessor.Apply(original, settings, TestContext.Current.CancellationToken);
        draft.SetProcessedClip("idle", original, settings, cleaned);
        Assert.Same(cleaned, draft.Clips["idle"]); Assert.Same(original, draft.ProcessingSource("idle"));
        Assert.Equal(settings, draft.Processing("idle"));
        Assert.Equal(settings, draft.Snapshot().Processing("idle"));
        draft.SetProcessedClip("idle", original, ClipProcessing.None, original); // Turning cleanup off restores the file.
        Assert.Same(original, draft.Clips["idle"]); Assert.Equal(ClipProcessing.None, draft.Processing("idle"));
        draft.SetProcessedClip("idle", original, settings, cleaned);
        var replacement = ImportedPetClip.FromImage("new.png", png);
        draft.SetClip("idle", replacement); // A new file drops the old cleanup.
        Assert.Equal(ClipProcessing.None, draft.Processing("idle")); Assert.Same(replacement, draft.ProcessingSource("idle"));
        draft.SetProcessedClip("idle", replacement, settings, cleaned); draft.RemoveClip("idle");
        Assert.Null(draft.ProcessingSource("idle"));
    }

    public static bool HasVideoTool => PetMediaImporter.FindFFmpeg() is not null;

    [Fact(Skip = "Prepare media tools to run native GIF encoding.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(PetMediaProcessingTests))]
    public async Task AnimatedClipsKeepTransparencyBetweenFramesAndTheirTiming()
    {
        // A white box moves across a green screen; the old position must not linger after keying.
        int[] delays = [80, 90, 80, 250];
        var frames = Enumerable.Range(0, 4).Select(k => new AnimationFrame(
            Frame(64, 64, Green, (4 + k * 12, 20, 16, 16, White)), TimeSpan.FromMilliseconds(delays[k]))).ToArray();
        var keyed = PetMediaProcessing.Apply(frames, new(true, Green));
        var result = await PetMediaProcessor.EncodeGif("walk.mp4", keyed, TestContext.Current.CancellationToken);
        Assert.Equal("walk.mp4", result.FileName); Assert.False(result.IsStillImage);
        var decoded = result.LoadFrames();
        Assert.Equal(4, decoded.Count);
        Assert.Equal(delays.Select(delay => (double)delay), decoded.Select(frame => frame.Duration.TotalMilliseconds));
        for (var k = 1; k < 4; k++)
        {
            var image = decoded[k].Image;
            Assert.Equal(0u, image.Pixels[28 * 64 + 6] >> 24); // Where the box was in frame 0.
            Assert.Equal(256, image.Pixels.Count(pixel => pixel >> 24 != 0));
        }
    }
}
