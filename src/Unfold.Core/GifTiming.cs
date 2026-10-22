namespace Unfold.Core;

/// <summary>Rewrites per-frame GIF delays in place without re-encoding image data.</summary>
public static class GifTiming
{
    /// <summary>Sets frame <c>i</c>'s Graphic Control Extension delay to <paramref name="centiseconds"/>[i].</summary>
    public static byte[] RewriteDelays(byte[] gif, IReadOnlyList<int> centiseconds, bool clearBetweenFrames = false)
    {
        var data = gif.ToArray();
        if (data.Length < 13 || data[0] != 'G' || data[1] != 'I' || data[2] != 'F') throw new InvalidDataException("Expected a GIF.");
        var position = 13;
        if ((data[10] & 0x80) != 0) position += 3 << ((data[10] & 7) + 1);
        int? pendingDelay = null; var frame = 0;
        while (true)
        {
            Require(data, position, 1);
            switch (data[position])
            {
                case 0x21:
                    Require(data, position, 3);
                    if (data[position + 1] == 0xF9 && data[position + 2] == 4) pendingDelay = position + 4;
                    position = SkipSubBlocks(data, position + 2);
                    break;
                case 0x2C:
                    Require(data, position, 10);
                    var packed = data[position + 9];
                    position += 10;
                    if ((packed & 0x80) != 0) position += 3 << ((packed & 7) + 1);
                    position = SkipSubBlocks(data, position + 1); // LZW minimum code size, then image data.
                    if (pendingDelay is not { } delay || frame >= centiseconds.Count) throw new InvalidDataException("GIF frames do not match their timing.");
                    var value = Math.Clamp(centiseconds[frame], 2, ushort.MaxValue);
                    data[delay] = (byte)value; data[delay + 1] = (byte)(value >> 8);
                    // Full-canvas frames must clear even when only a later frame uses transparency.
                    if (clearBetweenFrames) data[delay - 1] = (byte)((data[delay - 1] & ~0x1c) | 0x08);
                    pendingDelay = null; frame++;
                    break;
                case 0x3B:
                    if (frame != centiseconds.Count) throw new InvalidDataException("GIF frames do not match their timing.");
                    return data;
                default:
                    throw new InvalidDataException("Unexpected GIF block.");
            }
        }
    }

    private static int SkipSubBlocks(byte[] data, int position)
    {
        while (true)
        {
            Require(data, position, 1);
            var length = data[position];
            position += 1 + length;
            if (length == 0) return position;
        }
    }

    private static void Require(byte[] data, int position, int count)
    {
        if (position < 0 || position + count > data.Length) throw new InvalidDataException("Truncated GIF.");
    }
}
