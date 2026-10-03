using SkiaSharp;

namespace Unfold.Core;

public sealed record PixelImage(int Width, int Height, uint[] Pixels);
public sealed record AnimationFrame(PixelImage Image, TimeSpan Duration);

public static class ImageCodec
{
    public const int MaxFileBytes = 32 * 1024 * 1024;
    public const long MaxDecodedAnimationBytes = 128 * 1024 * 1024;
    public const int MaxPetImageDimension = 512;
    /// <summary>Normalizes a still image, including EXIF orientation, without enlarging it.</summary>
    public static unsafe PixelImage DecodePetImage(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaxFileBytes) throw new InvalidDataException("이미지는 32 MiB 이하로 선택해 주세요.");
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException("이미지 파일을 읽지 못했어요.");
        if (codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp or SKEncodedImageFormat.Bmp))
            throw new InvalidDataException("PNG, JPG, WEBP 또는 BMP 이미지를 선택해 주세요.");
        if (codec.EncodedFormat == SKEncodedImageFormat.Png && bytes.AsSpan().LastIndexOf(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 }) < 0)
            throw new InvalidDataException("손상되거나 완전하지 않은 이미지예요.");
        if (codec.FrameCount > 1) throw new InvalidDataException("움직이는 이미지는 GIF 또는 MP4로 가져와 주세요.");
        var info = codec.Info;
        if (info.Width is < 1 or > 8192 || info.Height is < 1 or > 8192 || (long)info.Width * info.Height > 16 * 1024 * 1024)
            throw new InvalidDataException("이미지가 너무 커요. 1,600만 픽셀 이하로 줄여 주세요.");
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("손상되거나 완전하지 않은 이미지예요.");
        var scale = Math.Min(1, MaxPetImageDimension / (double)Math.Max(info.Width, info.Height));
        var width = Math.Max(1, (int)Math.Round(info.Width * scale));
        var height = Math.Max(1, (int)Math.Round(info.Height * scale));
        using var resized = scale < 1 ? bitmap.Resize(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul),
            new SKSamplingOptions(SKFilterMode.Linear)) : null;
        var source = scale < 1 ? resized ?? throw new InvalidDataException("이미지 크기를 조절하지 못했어요.") : bitmap;
        var origin = codec.EncodedOrigin;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outputWidth = swap ? height : width; var outputHeight = swap ? width : height;
        var pixels = new uint[outputWidth * outputHeight];
        for (var y = 0; y < height; y++)
        {
            var row = new ReadOnlySpan<uint>((byte*)source.GetPixels() + y * source.RowBytes, width);
            for (var x = 0; x < width; x++)
            {
                var (dx, dy) = origin switch
                {
                    SKEncodedOrigin.TopRight => (width - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (width - 1 - x, height - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, height - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (height - 1 - y, x),
                    SKEncodedOrigin.RightBottom => (height - 1 - y, width - 1 - x),
                    SKEncodedOrigin.LeftBottom => (y, width - 1 - x),
                    _ => (x, y)
                };
                pixels[dy * outputWidth + dx] = row[x];
            }
        }
        return new(outputWidth, outputHeight, pixels);
    }
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
        if (info.Width is < 1 or > 2048 || info.Height is < 1 or > 2048 || count > 512 || (long)info.Width * info.Height * count * 4 > MaxDecodedAnimationBytes)
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
