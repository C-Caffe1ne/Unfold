using System.Buffers.Binary;
using SkiaSharp;

namespace Unfold.Core;

public static partial class ImageCodec
{
    /// <summary>Returns null for a still image. APNG is detected by its chunks, including files named .png.</summary>
    public static unsafe IReadOnlyList<AnimationFrame>? DecodePetAnimation(byte[] bytes, CancellationToken cancellationToken = default)
    {
        if (bytes.Length is 0 or > MaxFileBytes) throw new InvalidDataException(L.T("이미지는 32 MiB 이하로 선택해 주세요."));
        if (ApngDecoder.IsAnimated(bytes)) return ApngDecoder.Decode(bytes, cancellationToken);
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data) ?? throw new InvalidDataException(L.T("이미지 파일을 읽지 못했어요."));
        if (codec.EncodedFormat != SKEncodedImageFormat.Webp) return null;
        if (bytes.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != bytes.Length - 8)
            throw new InvalidDataException(L.T("손상되거나 완전하지 않은 이미지예요."));
        var animated = bytes.Length >= 30 && bytes.AsSpan(12, 4).SequenceEqual("VP8X"u8) && (bytes[20] & 2) != 0;
        if (!animated) return null;
        var info = codec.Info; var count = codec.FrameCount;
        ValidatePetAnimation(info.Width, info.Height, count);
        var metadata = codec.FrameInfo;
        var frames = new List<AnimationFrame>(count);
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // No prior-frame hint: Skia applies the required frame dependencies and disposal itself.
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels(), new SKCodecOptions(index)) != SKCodecResult.Success)
                throw new InvalidDataException(L.T("손상되거나 완전하지 않은 이미지예요."));
            var pixels = new uint[info.Width * info.Height];
            for (var y = 0; y < info.Height; y++)
                new ReadOnlySpan<uint>((byte*)bitmap.GetPixels() + y * bitmap.RowBytes, info.Width).CopyTo(pixels.AsSpan(y * info.Width));
            frames.Add(new(new(info.Width, info.Height, pixels), PetFrameDuration(metadata[index].Duration)));
        }
        return frames;
    }

    internal static void ValidatePetAnimation(int width, int height, int count)
    {
        if (width is < 1 or > 2048 || height is < 1 or > 2048 || count is < 1 or > 512 ||
            (long)width * height * count * 4 > MaxDecodedAnimationBytes)
            throw new InvalidDataException(L.T("움직이는 이미지의 크기나 프레임 수가 너무 커요."));
    }

    internal static TimeSpan PetFrameDuration(double milliseconds)
    {
        if (milliseconds > ushort.MaxValue * 10)
            throw new InvalidDataException(L.T("움직이는 이미지의 프레임 시간이 너무 길어요."));
        return TimeSpan.FromMilliseconds(Math.Max(20, milliseconds));
    }
}
