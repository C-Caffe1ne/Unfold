using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AnimatedPetMediaTests
{
    internal static byte[] Apng(bool poster = false)
    {
        using var output = new MemoryStream();
        var first = ImageCodec.EncodePng(new(2, 2, [0xffff0000, 0xffff0000, 0xffff0000, 0xffff0000]));
        output.Write(first.AsSpan(0, 8));
        PngChunk(output, "IHDR", PngParts(first).Single(p => p.Type == "IHDR").Data);
        PngChunk(output, "acTL", [0, 0, 0, 4, 0, 0, 0, 0]);
        if (poster)
            foreach (var part in PngParts(ImageCodec.EncodePng(new(2, 2, [0xffffff00, 0xffffff00, 0xffffff00, 0xffffff00]))).Where(p => p.Type == "IDAT"))
                PngChunk(output, "IDAT", part.Data);
        uint sequence = 0;
        var images = new[] { first, ImageCodec.EncodePng(new(1, 1, [0x800000ff])),
            ImageCodec.EncodePng(new(1, 1, [0xff00ff00])), ImageCodec.EncodePng(new(1, 1, [0xff0000ff])) };
        int[] delays = [80, 120, 200, 300];
        for (var i = 0; i < 4; i++)
        {
            var control = new byte[26];
            BinaryPrimitives.WriteUInt32BigEndian(control, sequence++);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(4), i == 0 ? 2 : 1);
            BinaryPrimitives.WriteInt32BigEndian(control.AsSpan(8), i == 0 ? 2 : 1);
            if (i == 2) { control[15] = 1; control[19] = 1; }
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(20), (ushort)delays[i]);
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(22), 1000);
            control[24] = i == 1 ? (byte)2 : i == 2 ? (byte)1 : (byte)0;
            control[25] = i == 1 ? (byte)1 : (byte)0;
            PngChunk(output, "fcTL", control);
            foreach (var part in PngParts(images[i]).Where(p => p.Type == "IDAT"))
                if (i == 0 && !poster) PngChunk(output, "IDAT", part.Data);
                else
                {
                    var data = new byte[part.Data.Length + 4];
                    BinaryPrimitives.WriteUInt32BigEndian(data, sequence++); part.Data.CopyTo(data, 4);
                    PngChunk(output, "fdAT", data);
                }
        }
        PngChunk(output, "IEND", []); return output.ToArray();
    }

    internal static byte[] Webp()
    {
        using var body = new MemoryStream(); body.Write("WEBP"u8);
        RiffChunk(body, "VP8X", [0x12, 0, 0, 0, 31, 0, 0, 23, 0, 0]);
        RiffChunk(body, "ANIM", new byte[6]);
        int[] delays = [80, 120, 200, 300];
        for (var i = 0; i < 4; i++)
        {
            using var bitmap = new SKBitmap(32, 24);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.Red })
            { canvas.Clear(SKColors.Transparent); canvas.DrawRect(2 + i * 6, 8, 6, 8, paint); }
            using var pixels = bitmap.PeekPixels();
            using var encoded = pixels.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100)) ?? throw new InvalidOperationException("WebP fixture encoding failed.");
            var still = encoded.ToArray();
            using var frame = new MemoryStream(); var header = new byte[16];
            header[6] = 31; header[9] = 23; header[12] = (byte)delays[i]; header[13] = (byte)(delays[i] >> 8);
            header[15] = 2; // Full canvas source replacement, no blending.
            frame.Write(header);
            for (var p = 12; p < still.Length;)
            {
                var size = BinaryPrimitives.ReadInt32LittleEndian(still.AsSpan(p + 4));
                var total = 8 + size + (size & 1);
                if (Encoding.ASCII.GetString(still, p, 4) is "VP8 " or "VP8L" or "ALPH") frame.Write(still.AsSpan(p, total));
                p += total;
            }
            RiffChunk(body, "ANMF", frame.ToArray());
        }
        using var output = new MemoryStream(); output.Write("RIFF"u8);
        var length = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(length, (int)body.Length);
        output.Write(length); body.WriteTo(output); return output.ToArray();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ApngCompositesOffsetsAlphaAndBothDisposalModesAndExcludesThePoster(bool poster)
    {
        var frames = ImageCodec.DecodePetAnimation(Apng(poster), TestContext.Current.CancellationToken)!;
        Assert.Equal(4, frames.Count);
        Assert.Equal(new double[] { 80, 120, 200, 300 }, frames.Select(f => f.Duration.TotalMilliseconds));
        Assert.Equal(new uint[] { 0xffff0000, 0xffff0000, 0xffff0000, 0xffff0000 }, frames[0].Image.Pixels);
        Assert.Equal(0xff7f0080u, frames[1].Image.Pixels[0]);
        Assert.Equal(new uint[] { 0xffff0000, 0xffff0000, 0xffff0000, 0xff00ff00 }, frames[2].Image.Pixels);
        Assert.Equal(new uint[] { 0xff0000ff, 0xffff0000, 0xffff0000, 0 }, frames[3].Image.Pixels);
        Assert.Throws<InvalidDataException>(() => ImportedPetClip.FromImage("animation.png", Apng(poster)));
    }

    [Fact]
    public void WebpKeepsMovingTransparentFramesAndTheirIndividualTiming()
    {
        var frames = ImageCodec.DecodePetAnimation(Webp(), TestContext.Current.CancellationToken)!;
        Assert.Equal(4, frames.Count);
        Assert.Equal(new double[] { 80, 120, 200, 300 }, frames.Select(f => f.Duration.TotalMilliseconds));
        for (var i = 0; i < frames.Count; i++)
        {
            Assert.Equal(48, frames[i].Image.Pixels.Count(pixel => pixel >> 24 != 0));
            Assert.Equal(0xffff0000u, frames[i].Image.Pixels[10 * 32 + 4 + i * 6]);
            if (i > 0) Assert.Equal(0u, frames[i].Image.Pixels[10 * 32 + 4]);
        }
    }

    [Theory]
    [InlineData("crc")] [InlineData("truncated")] [InlineData("count")]
    [InlineData("sequence")] [InlineData("bounds")] [InlineData("frames")]
    [InlineData("dimensions")] [InlineData("budget")]
    public void CorruptOrOverBudgetApngIsRejectedBeforeImport(string failure)
    {
        var bytes = Apng();
        if (failure == "crc") bytes[^1] ^= 1;
        else if (failure == "truncated") bytes = bytes[..^12];
        else bytes = RewritePng(bytes, (type, data) =>
        {
            if (type == "acTL" && failure == "count") data[3] = 3;
            if (type == "acTL" && failure == "frames") BinaryPrimitives.WriteInt32BigEndian(data, 513);
            if (type == "IHDR" && failure == "dimensions") BinaryPrimitives.WriteInt32BigEndian(data, 2049);
            if (type == "IHDR" && failure == "budget")
            { BinaryPrimitives.WriteInt32BigEndian(data, 2048); BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), 2048); }
            if (type == "acTL" && failure == "budget") data[3] = 9;
            if (type == "fcTL" && failure == "sequence") data[3] = 20;
            if (type == "fcTL" && failure == "bounds") data[15] = 10;
        });
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePetAnimation(bytes, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ApngFromAnIndependentEncoderPreservesTheGifSourceFrames()
    {
        var bytes = File.ReadAllBytes(CustomPetDraftTests.Fixture("pet-motion.apng"));
        var frames = ImageCodec.DecodePetAnimation(bytes, TestContext.Current.CancellationToken)!;
        var original = ImageCodec.DecodeGif(File.ReadAllBytes(CustomPetDraftTests.Fixture()));
        Assert.Equal(original.Count, frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            Assert.Equal(original[i].Duration, frames[i].Duration);
            Assert.Equal(original[i].Image.Pixels, frames[i].Image.Pixels);
        }
    }

    [Fact]
    public void TruncatedWebpAndCancelledDecodingDoNotReturnPartialAnimation()
    {
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePetAnimation(Webp()[..^10], TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => ImageCodec.DecodePetAnimation(Apng(), cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => ImageCodec.DecodePetAnimation(Webp(), cancellation.Token));
    }

    public static bool HasVideoTool => PetMediaImporter.FindFFmpeg() is not null;

    [Theory(Skip = "Prepare media tools for animated image conversion.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(AnimatedPetMediaTests))]
    [InlineData(".apng")] [InlineData(".png")] [InlineData(".WEBP")]
    public async Task AnimatedImagesImportAndRoundTripThroughAnEditablePack(string extension)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "움직이는 펫" + extension);
        File.WriteAllBytes(path, extension == ".WEBP" ? Webp() : Apng());
        Assert.Contains("*" + extension.ToLowerInvariant(), PetMediaImporter.FilePatterns);
        var clip = await PetMediaImporter.Import(path, TestContext.Current.CancellationToken);
        File.Delete(path);
        Assert.False(clip.IsStillImage); Assert.Equal(4, clip.FrameCount);
        Assert.Equal(new double[] { 80, 120, 200, 300 }, clip.LoadFrames().Select(f => f.Duration.TotalMilliseconds));
        var source = ImageCodec.DecodePetAnimation(extension == ".WEBP" ? Webp() : Apng(), TestContext.Current.CancellationToken)!;
        for (var i = 0; i < 4; i++) Assert.Equal(source[i].Image.Pixels, clip.LoadFrames()[i].Image.Pixels);
        var draft = new CustomPetDraft(); draft.SetClip("idle", clip);
        var output = Path.Combine(temp.Path, "animation.unfoldpet"); draft.Export("움직이는 펫", output);
        using var pack = CharacterPack.Open(output);
        var installed = new CharacterLibrary(Path.Combine(temp.Path, "library")).Install(pack, null);
        Assert.Empty(pack.Audit.Errors);
        var reopened = new CustomPetDraft(installed).Clips["idle"].LoadFrames();
        Assert.Equal(4, reopened.Count);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(clip.LoadFrames()[i].Duration, reopened[i].Duration);
            Assert.Equal(clip.LoadFrames()[i].Image.Pixels, reopened[i].Image.Pixels);
        }
        Assert.Contains(reopened[^1].Image.Pixels, p => p >> 24 == 0);
    }

    internal static byte[] RewritePng(byte[] bytes, Action<string, byte[]> change)
    {
        using var output = new MemoryStream(); output.Write(bytes.AsSpan(0, 8));
        foreach (var (type, data) in PngParts(bytes)) { change(type, data); PngChunk(output, type, data); }
        return output.ToArray();
    }
    private static IEnumerable<(string Type, byte[] Data)> PngParts(byte[] bytes)
    {
        for (var p = 8; p < bytes.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(p));
            yield return (Encoding.ASCII.GetString(bytes, p + 4, 4), bytes.AsSpan(p + 8, length).ToArray());
            p += 12 + length;
        }
    }
    private static void PngChunk(Stream stream, string type, byte[] data)
    {
        var bytes = new byte[data.Length + 12];
        BinaryPrimitives.WriteInt32BigEndian(bytes, data.Length); Encoding.ASCII.GetBytes(type).CopyTo(bytes, 4); data.CopyTo(bytes, 8);
        uint crc = 0xffffffff;
        foreach (var b in bytes.AsSpan(4, data.Length + 4))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++) crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(bytes.Length - 4), ~crc); stream.Write(bytes);
    }
    private static void RiffChunk(Stream stream, string type, byte[] data)
    {
        stream.Write(Encoding.ASCII.GetBytes(type)); var length = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, data.Length); stream.Write(length); stream.Write(data);
        if ((data.Length & 1) != 0) stream.WriteByte(0);
    }
}
