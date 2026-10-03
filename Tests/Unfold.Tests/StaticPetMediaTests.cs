using System.Buffers.Binary;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class StaticPetMediaTests
{
    internal static byte[] Image(string extension, int width = 24, int height = 12)
    {
        if (extension == ".bmp")
        {
            var stride = (width * 3 + 3) & ~3; var bytes = new byte[54 + stride * height];
            bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2), bytes.Length);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(10), 54);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), 40);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), width);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(22), height);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 1);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 24);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++) bytes[54 + y * stride + x * 3 + 2] = 255;
            return bytes;
        }
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        canvas.DrawRect(0, 0, width / 2, height / 2, paint);
        using var data = bitmap.Encode(extension switch
        {
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp, _ => SKEncodedImageFormat.Png
        }, 100);
        return data.ToArray();
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".webp")]
    [InlineData(".bmp")]
    [InlineData(".JPG")]
    public async Task CommonImagesImportAsStillFramesAndSurviveSourceRemoval(string extension)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "사진" + extension);
        File.WriteAllBytes(path, Image(extension.ToLowerInvariant()));
        var clip = await PetMediaImporter.Import(path, TestContext.Current.CancellationToken); File.Delete(path);
        Assert.True(clip.IsStillImage); Assert.Equal(1, clip.FrameCount);
        Assert.Equal(24, clip.Width); Assert.Equal(12, clip.Height); Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
        var draft = new CustomPetDraft(); draft.SetClip("idle", clip); draft.SetClip("click", clip);
        var output = Path.Combine(temp.Path, "사진.unfoldpet"); draft.Export("사진 펫", output);
        using var pack = CharacterPack.Open(output);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "Characters")); var installed = library.Install(pack, null);
        foreach (var action in new[] { "idle", "click" })
        {
            Assert.Null(installed.Manifest.Animations[action].Gif);
            Assert.Equal(clip.LoadFrames()[0].Image.Pixels, Assert.Single(installed.LoadAnimation(action)).Image.Pixels);
            Assert.Equal(action == "idle", installed.Manifest.Animations[action].Loop);
        }
        Assert.Equal("Reinstall", library.InspectInstall(pack).Action);
    }

    [Fact]
    public void TransparentPixelsAndAspectRatioSurviveNormalizationAndTheSourceIsASnapshot()
    {
        var image = new PixelImage(1024, 512, Enumerable.Repeat(0x80805020u, 1024 * 512).ToArray()); image.Pixels[0] = 0;
        var bytes = ImageCodec.EncodePng(image); var clip = ImportedPetClip.FromImage("alpha.png", bytes);
        Array.Clear(bytes);
        Assert.Equal(512, clip.Width); Assert.Equal(256, clip.Height);
        var normalized = clip.LoadFrames()[0].Image;
        Assert.InRange(normalized.Pixels[^1] >> 24, 127u, 129u);
        Assert.Equal(512 * 256 * 4, clip.DecodedBytes);
        var small = new PixelImage(2, 1, [0x00000000, 0x80805020]);
        Assert.Equal(small.Pixels, ImportedPetClip.FromImage("small.png", ImageCodec.EncodePng(small)).LoadFrames()[0].Image.Pixels);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void JpegPhotoExifRotationAndMirroringAreApplied(int orientation)
    {
        var bytes = Image(".jpg"); var source = ImageCodec.DecodePetImage(bytes);
        var payload = new byte[32]; "Exif\0\0"u8.CopyTo(payload); "II"u8.CopyTo(payload.AsSpan(6));
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), 42);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(10), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(16), 0x0112);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(18), 3);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(24), (ushort)orientation);
        var rotated = ImageCodec.DecodePetImage([.. bytes[..2], 0xff, 0xe1, 0, 34, .. payload, .. bytes[2..]]);
        Assert.Equal(orientation >= 5 ? source.Height : source.Width, rotated.Width);
        Assert.Equal(orientation >= 5 ? source.Width : source.Height, rotated.Height);
        for (var y = 0; y < source.Height; y++)
            for (var x = 0; x < source.Width; x++)
            {
                var (dx, dy) = orientation switch
                {
                    2 => (source.Width - 1 - x, y), 3 => (source.Width - 1 - x, source.Height - 1 - y),
                    4 => (x, source.Height - 1 - y), 5 => (y, x), 6 => (source.Height - 1 - y, x),
                    7 => (source.Height - 1 - y, source.Width - 1 - x), 8 => (y, source.Width - 1 - x), _ => (x, y)
                };
                Assert.Equal(source.Pixels[y * source.Width + x], rotated.Pixels[dy * rotated.Width + dx]);
            }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void DifferentSizedImagesAndGifClipsShareAnInstallablePack(bool gifIdle)
    {
        using var temp = new TempDirectory(); var draft = new CustomPetDraft();
        foreach (var (action, index) in CustomPetDraft.Actions.Select((action, index) => (action, index)))
            draft.SetClip(action, action == (gifIdle ? "idle" : "click")
                ? ImportedPetClip.FromGif("motion.gif", File.ReadAllBytes(CustomPetDraftTests.Fixture()))
                : ImportedPetClip.FromImage("photo.png", Image(".png", 20 + index * 4, 10 + index * 2)));
        draft.SetPlayback("idle", 0); draft.SetPlayback("attention", 2);
        draft.SetPlayback("hover", 1); draft.SetPlayback("pointerUp", 2);
        var output = Path.Combine(temp.Path, "mixed.unfoldpet"); draft.Export("혼합 펫", output);
        using var pack = CharacterPack.Open(output);
        var installed = new CharacterLibrary(Path.Combine(temp.Path, "Characters")).Install(pack, null);
        Assert.Empty(pack.Audit.Errors); Assert.Equal(1, installed.Manifest.Version);
        Assert.False(installed.Manifest.Animations["idle"].Loop);
        Assert.True(installed.Manifest.Animations["attention"].PingPong);
        Assert.True(installed.Manifest.Animations["hover"].Loop);
        Assert.True(installed.Manifest.Animations["pointerUp"].PingPong);
        foreach (var (action, clip) in draft.Clips)
        {
            var actual = installed.LoadAnimation(action); Assert.Equal(clip.FrameCount, actual.Count);
            var expected = clip.LoadFrames()[0].Image;
            var image = actual[0].Image;
            var x = (image.Width - expected.Width) / 2; var y = (image.Height - expected.Height) / 2;
            for (var row = 0; row < expected.Height; row++)
                Assert.Equal(expected.Pixels.AsSpan(row * expected.Width, expected.Width).ToArray(),
                    image.Pixels.AsSpan((y + row) * image.Width + x, expected.Width).ToArray());
        }
    }

    [Fact]
    public void CorruptOversizedUnsupportedAndInvisibleImagesCannotReplaceAValidClip()
    {
        var draft = new CustomPetDraft(); var clip = ImportedPetClip.FromImage("good.png", Image(".png")); draft.SetClip("idle", clip);
        foreach (var bytes in new[] { new byte[33 * 1024 * 1024], "not an image"u8.ToArray(),
            File.ReadAllBytes(CustomPetDraftTests.Fixture()), ImageCodec.EncodePng(new(1, 1, [0])),
            Image(".png")[..^12],
            ImageCodec.EncodePng(new(8193, 1, new uint[8193])) })
            Assert.Throws<InvalidDataException>(() => draft.SetClip("idle", ImportedPetClip.FromImage("bad.png", bytes)));
        Assert.Same(clip, draft.Clips["idle"]);
    }
}

[Collection("Timer settings")]
public class StaticPetWindowTests
{
    [AvaloniaFact]
    public async Task ImageAssignmentPreviewAndExportUseTheExistingActionCards()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "사진.jpeg"); File.WriteAllBytes(path, StaticPetMediaTests.Image(".jpeg"));
        var output = Path.Combine(temp.Path, "static.unfoldpet");
        var window = new CustomPetWindow(path, () => Task.FromResult<string?>(path), () => Task.FromResult<string?>(output));
        T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        void Press(string name) => Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        async Task Until(Func<bool> ready)
        {
            for (var i = 0; i < 300 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
            Assert.True(ready());
        }
        try
        {
            window.Show(); Find<TextBox>("CustomPetName").Text = "사진 펫"; Press("AssignPetMedia");
            await Until(() => Find<Button>("CreateCustomPetPack").IsEnabled);
            Assert.Equal("사진.jpeg", Find<TextBlock>("CustomPetLabel_idle").Text);
            Assert.Equal("사진.jpeg", ToolTip.GetTip(Find<TextBlock>("CustomPetLabel_idle")));
            Assert.NotNull(Find<Image>("CustomPetThumbnail_idle").Source);
            Find<ComboBox>("CustomPetPlayback_idle").SelectedIndex = 2;
            Press("CustomPetPreview_idle"); File.Delete(path); Press("CreateCustomPetPack");
            await Until(() => window.CreatedPackPath is not null);
            using var pack = CharacterPack.Open(output); Assert.Single(pack.Character.LoadAnimation("idle")); Assert.Empty(pack.Audit.Errors);
            Assert.True(pack.Character.Manifest.Animations["idle"].PingPong);
        }
        finally { window.Close(); }
    }
}
