namespace Unfold.Core;

/// <summary>Per-file cleanup applied to imported media before it is stored in a pack.</summary>
public sealed record ClipProcessing(bool RemoveBackground = false, uint KeyColor = 0, int Tolerance = ClipProcessing.DefaultTolerance, bool AlignFeet = false)
{
    public const int DefaultTolerance = 40;
    public static ClipProcessing None { get; } = new();
    public bool IsActive => RemoveBackground || AlignFeet;
}

public static class PetMediaProcessing
{
    // Tolerance 0–100 maps to a CbCr distance; luminance is ignored so screen shadows key out too.
    private const double ToleranceScale = 1.2, SpillReach = 1.6;

    public static int FootMargin(int canvas) => Math.Max(1, (int)Math.Round(canvas * .04));

    /// <summary>The most common opaque color on the first frame's border, averaged within its bucket.</summary>
    public static uint DetectKeyColor(PixelImage frame)
    {
        var buckets = new Dictionary<int, (int Count, long R, long G, long B)>();
        void Add(uint pixel)
        {
            if (pixel >> 24 < 128) return;
            var (r, g, b) = Channels(pixel);
            var key = (r >> 4) << 8 | (g >> 4) << 4 | b >> 4;
            var bucket = buckets.GetValueOrDefault(key);
            buckets[key] = (bucket.Count + 1, bucket.R + r, bucket.G + g, bucket.B + b);
        }
        for (var x = 0; x < frame.Width; x++) { Add(frame.Pixels[x]); Add(frame.Pixels[(frame.Height - 1) * frame.Width + x]); }
        for (var y = 1; y < frame.Height - 1; y++) { Add(frame.Pixels[y * frame.Width]); Add(frame.Pixels[y * frame.Width + frame.Width - 1]); }
        if (buckets.Count == 0) return 0xFF00B140;
        var best = buckets.Values.MaxBy(bucket => bucket.Count);
        return 0xFF000000 | (uint)(best.R / best.Count) << 16 | (uint)(best.G / best.Count) << 8 | (uint)(best.B / best.Count);
    }

    public static IReadOnlyList<AnimationFrame> Apply(IReadOnlyList<AnimationFrame> frames, ClipProcessing settings)
    {
        if (!settings.IsActive) return frames;
        var images = frames.Select(frame => settings.RemoveBackground
            ? RemoveBackground(frame.Image, settings.KeyColor, settings.Tolerance) : frame.Image).ToArray();
        if (images.Any(image => image.Pixels.All(pixel => pixel >> 24 == 0)))
            throw new InvalidDataException(L.T("배경을 지우면 빈 프레임이 생겨요. 허용 범위를 낮춰 주세요."));
        if (settings.AlignFeet) images = AlignFeet(images);
        return frames.Select((frame, index) => new AnimationFrame(images[index], frame.Duration)).ToArray();
    }

    private static PixelImage RemoveBackground(PixelImage image, uint key, int tolerance)
    {
        var threshold = Math.Clamp(tolerance, 0, 100) * ToleranceScale;
        var (keyCb, keyCr) = Chroma(key);
        var (kr, kg, kb) = Channels(key);
        // Spill comes from the key's strongest channel, e.g. green light on a pet's edge.
        var spill = kg >= kr && kg >= kb ? 1 : kb >= kr ? 2 : 0;
        var pixels = new uint[image.Pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            var pixel = image.Pixels[index];
            var (cb, cr) = Chroma(pixel);
            var distance = Math.Sqrt((cb - keyCb) * (cb - keyCb) + (cr - keyCr) * (cr - keyCr));
            if (distance <= threshold) { pixels[index] = 0; continue; }
            pixels[index] = distance <= threshold * SpillReach ? Despill(pixel, spill) : pixel;
        }
        return new(image.Width, image.Height, pixels);
    }

    private static uint Despill(uint pixel, int channel)
    {
        var (r, g, b) = Channels(pixel);
        switch (channel)
        {
            case 1: g = Math.Min(g, Math.Max(r, b)); break;
            case 2: b = Math.Min(b, Math.Max(r, g)); break;
            default: r = Math.Min(r, Math.Max(g, b)); break;
        }
        return pixel & 0xFF000000 | (uint)r << 16 | (uint)g << 8 | (uint)b;
    }

    /// <summary>Moves every frame by one shared offset: lowest pet row to the foot line, median column to the center.</summary>
    private static PixelImage[] AlignFeet(PixelImage[] images)
    {
        var size = images.Max(image => Math.Max(image.Width, image.Height));
        var columns = new long[images.Max(image => image.Width)];
        var bottom = 0;
        foreach (var image in images)
            for (var index = 0; index < image.Pixels.Length; index++)
            {
                if (image.Pixels[index] >> 24 == 0) continue;
                columns[index % image.Width]++; bottom = Math.Max(bottom, index / image.Width);
            }
        long half = (columns.Sum() + 1) / 2, seen = 0; var median = 0;
        for (; median < columns.Length; median++) { seen += columns[median]; if (seen >= half) break; }
        var dx = size / 2 - median; var dy = size - 1 - FootMargin(size) - bottom;
        return images.Select(image =>
        {
            var pixels = new uint[size * size];
            for (var y = 0; y < image.Height; y++)
            {
                var ty = y + dy; if (ty < 0 || ty >= size) continue;
                for (var x = 0; x < image.Width; x++)
                {
                    var tx = x + dx; if (tx < 0 || tx >= size) continue;
                    pixels[ty * size + tx] = image.Pixels[y * image.Width + x];
                }
            }
            return new PixelImage(size, size, pixels);
        }).ToArray();
    }

    private static (int R, int G, int B) Channels(uint pixel) => ((int)(pixel >> 16) & 255, (int)(pixel >> 8) & 255, (int)pixel & 255);

    private static (double Cb, double Cr) Chroma(uint pixel)
    {
        var (r, g, b) = Channels(pixel);
        return (-.168736 * r - .331264 * g + .5 * b, .5 * r - .418688 * g - .081312 * b);
    }
}
