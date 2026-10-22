using System.Buffers.Binary;
using System.Text;

namespace Unfold.Core;

/// <summary>APNG framing/compositing; pixel decompression remains in Skia's PNG decoder.
/// This also works with the shipped Skia/FFmpeg builds that have no APNG decoder.</summary>
internal static class ApngDecoder
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private sealed record Chunk(string Type, byte[] Data);
    private sealed record Frame(int Width, int Height, int X, int Y, TimeSpan Delay, byte Dispose, byte Blend);

    internal static bool IsAnimated(byte[] bytes)
    {
        if (!bytes.AsSpan().StartsWith(Signature)) return false;
        return Chunks(bytes).Any(chunk => chunk.Type == "acTL");
    }

    internal static IReadOnlyList<AnimationFrame> Decode(byte[] bytes, CancellationToken cancellationToken)
    {
        byte[]? header = null;
        var shared = new List<Chunk>(); var frames = new List<AnimationFrame>();
        using var compressed = new MemoryStream();
        Frame? current = null;
        var width = 0; var height = 0; var count = 0; uint sequence = 0;
        var seenIdat = false; var seenFdat = false; var endedIdat = false; var ended = false;
        uint[] canvas = [];
        foreach (var chunk in Chunks(bytes))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = chunk.Data.AsSpan();
            if (seenIdat && chunk.Type != "IDAT") endedIdat = true;
            if (header is null && chunk.Type != "IHDR") throw Invalid();
            switch (chunk.Type)
            {
                case "IHDR":
                    if (header is not null || data.Length != 13) throw Invalid();
                    header = chunk.Data; width = Number(data); height = Number(data[4..]);
                    ImageCodec.ValidatePetAnimation(width, height, 1);
                    break;
                case "acTL":
                    if (count != 0 || seenIdat || data.Length != 8) throw Invalid();
                    count = Number(data); ImageCodec.ValidatePetAnimation(width, height, count);
                    canvas = new uint[width * height];
                    break;
                case "fcTL":
                    if (count == 0 || data.Length != 26 || Read(data) != sequence++) throw Invalid();
                    FinishFrame();
                    if (frames.Count >= count) throw Invalid();
                    var w = Number(data[4..]); var h = Number(data[8..]);
                    var x = Number(data[12..]); var y = Number(data[16..]);
                    if (w < 1 || h < 1 || w > width || h > height || x > width - w || y > height - h || data[24] > 2 || data[25] > 1)
                        throw Invalid();
                    if (!seenIdat && (w != width || h != height || x != 0 || y != 0)) throw Invalid();
                    var denominator = BinaryPrimitives.ReadUInt16BigEndian(data[22..]);
                    var delay = 1000.0 * BinaryPrimitives.ReadUInt16BigEndian(data[20..]) / (denominator == 0 ? 100 : denominator);
                    current = new(w, h, x, y, ImageCodec.PetFrameDuration(delay), data[24], data[25]);
                    break;
                case "IDAT":
                    if (count == 0 || endedIdat || seenFdat) throw Invalid();
                    seenIdat = true;
                    // A default image before the first fcTL is a poster, not an animation frame.
                    if (current is not null) compressed.Write(data);
                    break;
                case "fdAT":
                    if (!seenIdat || current is null || data.Length < 4 || Read(data) != sequence++) throw Invalid();
                    // The default image's frame must end at the next fcTL before fdAT begins.
                    if (frames.Count == 0 && compressed.Length > 0 && !seenFdat) throw Invalid();
                    seenFdat = true; compressed.Write(data[4..]);
                    break;
                case "IEND":
                    if (!seenIdat || data.Length != 0) throw Invalid();
                    FinishFrame(); ended = true;
                    break;
                case "PLTE": case "tRNS":
                    if (seenIdat || shared.Any(item => item.Type == chunk.Type)) throw Invalid();
                    shared.Add(chunk);
                    break;
                default:
                    if (char.IsUpper(chunk.Type[0])) throw Invalid(); // Unknown critical PNG chunk.
                    break;
            }
        }
        if (!ended || frames.Count != count || count == 0) throw Invalid();
        return frames;

        void FinishFrame()
        {
            if (current is not { } frame) return;
            if (compressed.Length == 0 || frames.Count >= count) throw Invalid();
            using var png = new MemoryStream(); png.Write(Signature);
            var frameHeader = header!.ToArray();
            BinaryPrimitives.WriteInt32BigEndian(frameHeader, frame.Width);
            BinaryPrimitives.WriteInt32BigEndian(frameHeader.AsSpan(4), frame.Height);
            WriteChunk(png, "IHDR", frameHeader);
            foreach (var item in shared) WriteChunk(png, item.Type, item.Data);
            WriteChunk(png, "IDAT", compressed.ToArray()); WriteChunk(png, "IEND", []);
            var image = ImageCodec.DecodePng(png.ToArray(), 2048, 2048);
            var previous = frame.Dispose == 2 ? canvas.ToArray() : null;
            for (var y = 0; y < frame.Height; y++)
                for (var x = 0; x < frame.Width; x++)
                {
                    var target = (y + frame.Y) * width + x + frame.X;
                    var pixel = image.Pixels[y * frame.Width + x];
                    canvas[target] = frame.Blend == 0 ? pixel : Over(pixel, canvas[target]);
                }
            frames.Add(new(new(width, height, canvas.ToArray()), frame.Delay));
            if (previous is not null) canvas = previous;
            else if (frame.Dispose == 1)
                for (var y = 0; y < frame.Height; y++) Array.Clear(canvas, (y + frame.Y) * width + frame.X, frame.Width);
            compressed.SetLength(0); current = null;
        }
    }

    private static uint Over(uint foreground, uint background)
    {
        var a = foreground >> 24; var b = background >> 24;
        var alpha = a * 255 + b * (255 - a);
        if (alpha == 0) return 0;
        uint Channel(int shift) => (((foreground >> shift & 255) * a * 255 + (background >> shift & 255) * b * (255 - a)) + alpha / 2) / alpha;
        return ((alpha + 127) / 255 << 24) | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    private static IEnumerable<Chunk> Chunks(byte[] bytes)
    {
        var position = 8;
        while (position <= bytes.Length - 12)
        {
            var length = Number(bytes.AsSpan(position));
            if (length > bytes.Length - position - 12) throw Invalid();
            var type = Encoding.ASCII.GetString(bytes, position + 4, 4);
            if (Crc(bytes.AsSpan(position + 4, length + 4)) != Read(bytes.AsSpan(position + length + 8))) throw Invalid();
            var data = bytes.AsSpan(position + 8, length).ToArray(); position += length + 12;
            yield return new(type, data);
            if (type == "IEND")
            {
                if (position != bytes.Length) throw Invalid();
                yield break;
            }
        }
        throw Invalid();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var chunk = new byte[data.Length + 12]; BinaryPrimitives.WriteInt32BigEndian(chunk, data.Length);
        Encoding.ASCII.GetBytes(type).CopyTo(chunk, 4); data.CopyTo(chunk, 8);
        BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(chunk.Length - 4), Crc(chunk.AsSpan(4, data.Length + 4)));
        stream.Write(chunk);
    }
    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(index =>
    {
        var value = (uint)index;
        for (var bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xedb88320 ^ value >> 1 : value >> 1;
        return value;
    }).ToArray();
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint value = uint.MaxValue;
        foreach (var b in data) value = CrcTable[(value ^ b) & 255] ^ value >> 8;
        return ~value;
    }
    private static uint Read(ReadOnlySpan<byte> data) => BinaryPrimitives.ReadUInt32BigEndian(data);
    private static int Number(ReadOnlySpan<byte> data) => Read(data) <= int.MaxValue ? (int)Read(data) : throw Invalid();
    private static InvalidDataException Invalid() => new(L.T("손상되거나 완전하지 않은 이미지예요."));
}
