using SkiaSharp;

namespace Unfold.Core;

public sealed record PixelImage(int Width, int Height, uint[] Pixels);
public sealed record AnimationFrame(PixelImage Image, TimeSpan Duration);

public static class ImageCodec
{
    public const int MaxFileBytes = 32 * 1024 * 1024;
    public static unsafe PixelImage DecodePng(byte[] bytes, int maxWidth = 4096, int maxHeight = 4096, int maxPixels = 16 * 1024 * 1024)
    {
        if (bytes.Length > MaxFileBytes || bytes.Length < 20 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            bytes.AsSpan().LastIndexOf(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }) < 0)
            throw new InvalidDataException("The file is not a complete PNG or exceeds 32 MiB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Cannot decode the PNG.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > maxWidth || info.Height > maxHeight || (long)info.Width * info.Height > maxPixels)
            throw new InvalidDataException("Image dimensions exceed the supported limits.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException("Incomplete PNG pixels.");
        var pixels = new uint[info.Width * info.Height];
        for (var y = 0; y < info.Height; y++)
            new ReadOnlySpan<uint>((byte*)bitmap.GetPixels() + y * bitmap.RowBytes, info.Width).CopyTo(pixels.AsSpan(y * info.Width));
        return new(info.Width, info.Height, pixels);
    }
    public static unsafe byte[] EncodePng(PixelImage image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Pixels.Length != (long)image.Width * image.Height)
            throw new InvalidDataException("Invalid image geometry.");
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        for (var y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(new Span<uint>((byte*)bitmap.GetPixels() + y * bitmap.RowBytes, image.Width));
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
    public static unsafe IReadOnlyList<AnimationFrame> DecodeGif(byte[] bytes)
    {
        if (bytes.Length > MaxFileBytes) throw new InvalidDataException("GIF exceeds 32 MiB.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("Cannot decode the GIF.");
        if (codec.EncodedFormat != SKEncodedImageFormat.Gif) throw new InvalidDataException("Expected a GIF.");
        var info = codec.Info; var count = Math.Max(1, codec.FrameCount);
        if (info.Width is < 1 or > 2048 || info.Height is < 1 or > 2048 || count > 512 || (long)info.Width * info.Height * count * 4 > 128 * 1024 * 1024)
            throw new InvalidDataException("Decoded GIF exceeds the animation budget.");
        var frames = new List<AnimationFrame>(count);
        var metadata = codec.FrameInfo;
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        for (var index = 0; index < count; index++)
        {
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(index)) != SKCodecResult.Success)
                throw new InvalidDataException("Incomplete GIF frame.");
            var pixels = new uint[info.Width * info.Height];
            for (var y = 0; y < info.Height; y++)
                new ReadOnlySpan<uint>((byte*)bitmap.GetPixels() + y * bitmap.RowBytes, info.Width).CopyTo(pixels.AsSpan(y * info.Width));
            var duration = metadata.Length > index ? metadata[index].Duration : 100;
            frames.Add(new(new(info.Width, info.Height, pixels), TimeSpan.FromMilliseconds(duration <= 10 ? 100 : duration)));
        }
        return frames;
    }
    public static byte[] ReadBounded(string path, int maxBytes = MaxFileBytes)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > maxBytes) throw new InvalidDataException("File exceeds the size limit.");
        var data = new byte[checked((int)stream.Length)]; stream.ReadExactly(data); return data;
    }
}
