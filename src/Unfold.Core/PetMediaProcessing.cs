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
    // Below NeutralChroma the key is white, black or gray and has no hue of its own to key on.
    private const double ToleranceScale = 1.2, SpillReach = 1.6, NeutralChroma = 12;

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
        if (Math.Sqrt(keyCb * keyCb + keyCr * keyCr) < NeutralChroma) return RemoveNeutralBackground(image, key, threshold);
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

    /// <summary>White, black and gray pets share their backdrop's missing hue, so brightness counts too and only
    /// backdrop pixels reachable from the frame edge are cleared. A body enclosed by its outline stays.</summary>
    private static PixelImage RemoveNeutralBackground(PixelImage image, uint key, double threshold)
    {
        var (keyCb, keyCr) = Chroma(key); var keyY = Luma(key);
        bool Matches(uint pixel)
        {
            var (cb, cr) = Chroma(pixel); var dy = Luma(pixel) - keyY;
            return (cb - keyCb) * (cb - keyCb) + (cr - keyCr) * (cr - keyCr) + dy * dy <= threshold * threshold;
        }
        int width = image.Width, height = image.Height;
        var cleared = new bool[image.Pixels.Length];
        var queue = new Queue<int>();
        void Visit(int index)
        {
            var pixel = image.Pixels[index];
            // Already transparent pixels belong to the backdrop and carry the fill through.
            if (cleared[index] || (pixel >> 24 != 0 && !Matches(pixel))) return;
            cleared[index] = true; queue.Enqueue(index);
        }
        for (var x = 0; x < width; x++) { Visit(x); Visit((height - 1) * width + x); }
        for (var y = 0; y < height; y++) { Visit(y * width); Visit(y * width + width - 1); }
        while (queue.TryDequeue(out var index))
        {
            var x = index % width;
            if (x > 0) Visit(index - 1);
            if (x < width - 1) Visit(index + 1);
            if (index >= width) Visit(index - width);
            if (index < cleared.Length - width) Visit(index + width);
        }
        var pixels = new uint[image.Pixels.Length];
        for (var index = 0; index < pixels.Length; index++) pixels[index] = cleared[index] ? 0 : image.Pixels[index];
        return new(width, height, pixels);
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

    private static double Luma(uint pixel)
    {
        var (r, g, b) = Channels(pixel);
        return .299 * r + .587 * g + .114 * b;
    }

    private static (double Cb, double Cr) Chroma(uint pixel)
    {
        var (r, g, b) = Channels(pixel);
        return (-.168736 * r - .331264 * g + .5 * b, .5 * r - .418688 * g - .081312 * b);
    }
}
