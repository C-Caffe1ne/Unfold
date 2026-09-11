using Unfold.Core;

namespace Unfold.Tests;

public class CatExpansionTests
{
    private static readonly string[] CharacterIds = ["miso-tabby", "luna-blue", "coco-siamese"];

    public static IEnumerable<object[]> Animations()
    {
        foreach (var id in CharacterIds)
        foreach (var key in new[] { "idle", "stretch", "click" })
            yield return [id, key];
    }

    [Theory]
    [InlineData("miso-tabby")]
    [InlineData("luna-blue")]
    [InlineData("coco-siamese")]
    public void BundledPackageHasMatchingIdentityAndSpriteSheet(string id)
    {
        var package = Load(id);
        var sprite = package.Manifest.SpriteSheet;

        Assert.True(package.IsBuiltIn);
        Assert.Equal(id, package.Manifest.Id);
        Assert.False(string.IsNullOrWhiteSpace(package.Manifest.Name));
        Assert.Equal(384, sprite.FrameWidth);
        Assert.Equal(384, sprite.FrameHeight);
        Assert.True(sprite.Columns > 0);
        Assert.True(sprite.Rows > 0);
        Assert.Equal(sprite.Columns * sprite.FrameWidth, package.Sheet.Width);
        Assert.Equal(sprite.Rows * sprite.FrameHeight, package.Sheet.Height);
    }

    [Theory]
    [MemberData(nameof(Animations))]
    public void BundledGifHasVisibleMotionAndTransparentFrames(string id, string key)
    {
        var package = Load(id);
        Assert.True(package.Manifest.Animations.TryGetValue(key, out var definition), $"{id} is missing {key}.");
        Assert.NotNull(definition);
        Assert.Equal($"{key}.gif", definition.Gif);
        Assert.Equal(key == "idle", definition.Loop);

        var frames = package.LoadAnimation(key);
        Assert.True(frames.Count > 1, $"{id}/{key} must contain animation frames.");
        Assert.True(frames.Skip(1).Any(frame => !frame.Image.Pixels.SequenceEqual(frames[0].Image.Pixels)),
            $"{id}/{key} repeats one still image without visible pixel changes.");

        Assert.All(frames, frame =>
        {
            var image = frame.Image;
            Assert.Equal(384, image.Width);
            Assert.Equal(384, image.Height);
            Assert.True(frame.Duration.TotalMilliseconds > 10, $"{id}/{key} contains an unusably short frame.");
            Assert.Contains(image.Pixels, pixel => pixel >> 24 == 255);

            // Check every outer-edge pixel so an opaque GIF background cannot hide
            // behind a single transparent corner while covering the desktop.
            for (var x = 0; x < image.Width; x++)
            {
                Assert.Equal(0u, image.Pixels[x] >> 24);
                Assert.Equal(0u, image.Pixels[(image.Height - 1) * image.Width + x] >> 24);
            }
            for (var y = 0; y < image.Height; y++)
            {
                Assert.Equal(0u, image.Pixels[y * image.Width] >> 24);
                Assert.Equal(0u, image.Pixels[y * image.Width + image.Width - 1] >> 24);
            }
        });

        Assert.Equal(frames[0].Image.Pixels, frames[^1].Image.Pixels);
        if (!definition.Loop)
            Assert.True(frames.Sum(frame => frame.Duration.TotalSeconds) < 10,
                $"{id}/{key} must finish its one-shot animation in under ten seconds.");
    }

    [Theory]
    [InlineData("default-cat")]
    [InlineData("miso-tabby")]
    [InlineData("luna-blue")]
    [InlineData("coco-siamese")]
    public void NeutralPoseWorksAsAPickerPortrait(string id)
    {
        var package = Load(id); var sprite = package.Manifest.SpriteSheet;
        var portrait = package.Frame(0);

        Assert.Equal(sprite.FrameWidth, portrait.Width);
        Assert.Equal(sprite.FrameHeight, portrait.Height);
        // A card drawn from an empty cell would leave the character unidentifiable.
        Assert.True(portrait.Pixels.Count(pixel => pixel >> 24 > 128) > portrait.Pixels.Length / 100,
            $"{id} cell 0 is too sparse to identify the character in the picker.");
        Assert.Throws<ArgumentOutOfRangeException>(() => package.Frame(sprite.Columns * sprite.Rows));
        Assert.Throws<ArgumentOutOfRangeException>(() => package.Frame(-1));

        // Sheet-driven characters must crop through exactly the same path as playback.
        if (package.Manifest.Animations["idle"].Frames is { Length: > 0 } frames)
            Assert.Equal(package.LoadAnimation("idle")[0].Image.Pixels, package.Frame(frames[0]).Pixels);
    }

    private static CharacterPackage Load(string id) =>
        CharacterLibrary.LoadPackage(Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", id), true);
}
