namespace Unfold.Core;

public enum PixelTool { Pencil, Eraser, Fill, Eyedropper, Line, Rectangle, Ellipse }
public readonly record struct PixelPoint(int X, int Y);

public sealed class PixelLayer
{
    public string Name { get; set; } = "Layer 1";
    public double Opacity { get; set; } = 1;
    public List<uint[]> Frames { get; set; } = [];
    public PixelLayer Clone() => new() { Name = Name, Opacity = Opacity, Frames = Frames.Select(f => (uint[])f.Clone()).ToList() };
}

/// <summary>Top-left row-major, straight alpha 0xAARRGGBB pixels. No UI dependency.</summary>
public sealed class PixelDocument
{
    public const int MaxSide = 128, MaxFrames = 24, MaxLayers = 16;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public double Fps { get; set; } = 12;
    public string Name { get; set; } = "Unfold Character";
    public string Description { get; set; } = "";
    public List<PixelLayer> Layers { get; set; }
    public int FrameCount => Layers[0].Frames.Count;
    public long ByteCount => (long)Width * Height * FrameCount * Layers.Count * 4;

    public PixelDocument(int width = 64, int height = 64)
    {
        CheckSize(width, height);
        Width = width; Height = height;
        Layers = [new() { Frames = [new uint[width * height]] }];
    }
    public static void CheckSize(int width, int height)
    {
        if (width is < 1 or > MaxSide || height is < 1 or > MaxSide)
            throw new InvalidDataException($"Canvas must be 1–{MaxSide} pixels per side.");
    }
    public void Validate()
    {
        CheckSize(Width, Height);
        if (!double.IsFinite(Fps) || Fps is < 1 or > 24 || Layers.Count is < 1 or > MaxLayers)
            throw new InvalidDataException("Use 1–24 FPS and 1–16 layers.");
        if (FrameCount is < 1 or > MaxFrames || Layers.Any(l => !double.IsFinite(l.Opacity) || l.Opacity is < 0 or > 1 ||
                l.Frames.Count != FrameCount || l.Frames.Any(f => f.Length != Width * Height)))
            throw new InvalidDataException("Invalid layer/frame dimensions or opacity.");
    }
    public PixelDocument Clone() => new(Width, Height)
    { Name = Name, Description = Description, Fps = Fps, Layers = Layers.Select(l => l.Clone()).ToList() };
    public bool ContentEquals(PixelDocument other) => Width == other.Width && Height == other.Height && Fps == other.Fps &&
        Name == other.Name && Description == other.Description && Layers.Count == other.Layers.Count &&
        Layers.Zip(other.Layers).All(pair => pair.First.Name == pair.Second.Name && pair.First.Opacity == pair.Second.Opacity &&
            pair.First.Frames.Count == pair.Second.Frames.Count && pair.First.Frames.Zip(pair.Second.Frames).All(f => f.First.AsSpan().SequenceEqual(f.Second)));
    public bool Contains(PixelPoint p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public uint[] Composite(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        if (frame >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        var result = new uint[Width * Height];
        foreach (var layer in Layers.Where(l => l.Opacity > 0))
            for (var i = 0; i < result.Length; i++)
            {
                var source = layer.Frames[frame][i];
                if (source >> 24 == 0) continue;
                result[i] = source >> 24 == 255 && layer.Opacity == 1 ? source : Blend(source, result[i], layer.Opacity);
            }
        return result;
    }
    public static uint Blend(uint source, uint dest, double opacity)
    {
        var sa = (source >> 24) / 255d * opacity;
        var da = (dest >> 24) / 255d;
        var a = sa + da * (1 - sa);
        if (a <= 0) return 0;
        uint Channel(int shift)
        {
            var value = (((source >> shift) & 255) * sa + ((dest >> shift) & 255) * da * (1 - sa)) / a;
            return (uint)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255);
        }
        return (uint)Math.Round(a * 255, MidpointRounding.AwayFromZero) << 24 | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    public void Draw(PixelTool tool, PixelPoint start, PixelPoint end, uint color, int brush, int layer, int frame)
    {
        if (!Contains(start) || !Contains(end)) return;
        if (tool == PixelTool.Eyedropper) return;
        var pixels = Layers[layer].Frames[frame];
        if (tool == PixelTool.Fill) { Flood(pixels, end, color); return; }
        var ink = tool == PixelTool.Eraser ? 0 : color;
        var size = Math.Clamp(brush, 1, 8); var offset = (size - 1) / 2;
        void Stamp(int x, int y)
        {
            for (var py = Math.Max(0, y - offset); py < Math.Min(Height, y - offset + size); py++)
                for (var px = Math.Max(0, x - offset); px < Math.Min(Width, x - offset + size); px++)
                    pixels[py * Width + px] = ink;
        }
        var left = Math.Min(start.X, end.X); var right = Math.Max(start.X, end.X);
        var top = Math.Min(start.Y, end.Y); var bottom = Math.Max(start.Y, end.Y);
        if (tool == PixelTool.Rectangle)
        {
            for (var x = left; x <= right; x++) { Stamp(x, top); Stamp(x, bottom); }
            for (var y = top; y <= bottom; y++) { Stamp(left, y); Stamp(right, y); }
        }
        else if (tool == PixelTool.Ellipse && left != right && top != bottom)
        {
            var rx = (right - left) / 2d; var ry = (bottom - top) / 2d; var cx = left + rx; var cy = top + ry;
            for (var x = left; x <= right; x++)
            {
                var dy = ry * Math.Sqrt(Math.Max(0, 1 - Math.Pow((x - cx) / rx, 2)));
                Stamp(x, (int)Math.Round(cy - dy)); Stamp(x, (int)Math.Round(cy + dy));
            }
            for (var y = top; y <= bottom; y++)
            {
                var dx = rx * Math.Sqrt(Math.Max(0, 1 - Math.Pow((y - cy) / ry, 2)));
                Stamp((int)Math.Round(cx - dx), y); Stamp((int)Math.Round(cx + dx), y);
            }
        }
        else
        {
            var x = start.X; var y = start.Y; var dx = Math.Abs(end.X - x); var dy = -Math.Abs(end.Y - y);
            var sx = x < end.X ? 1 : -1; var sy = y < end.Y ? 1 : -1; var error = dx + dy;
            while (true)
            {
                Stamp(x, y); if (x == end.X && y == end.Y) break;
                var twice = error * 2;
                if (twice >= dy) { error += dy; x += sx; }
                if (twice <= dx) { error += dx; y += sy; }
            }
        }
    }
    private void Flood(uint[] pixels, PixelPoint point, uint color)
    {
        var origin = point.Y * Width + point.X; var old = pixels[origin];
        if (old == color) return;
        var queue = new Queue<int>(); queue.Enqueue(origin); pixels[origin] = color;
        void Visit(int index) { if (pixels[index] == old) { pixels[index] = color; queue.Enqueue(index); } }
        while (queue.TryDequeue(out var index))
        {
            var x = index % Width; var y = index / Width;
            if (x > 0) Visit(index - 1); if (x < Width - 1) Visit(index + 1);
            if (y > 0) Visit(index - Width); if (y < Height - 1) Visit(index + Width);
        }
    }
    public void Resize(int width, int height)
    {
        CheckSize(width, height);
        foreach (var layer in Layers)
            for (var frame = 0; frame < FrameCount; frame++)
            {
                var next = new uint[width * height];
                for (var y = 0; y < Math.Min(height, Height); y++)
                    Array.Copy(layer.Frames[frame], y * Width, next, y * width, Math.Min(width, Width));
                layer.Frames[frame] = next;
            }
        Width = width; Height = height;
    }
    public void AddFrame(int after, bool duplicate)
    {
        if (FrameCount >= MaxFrames) return;
        foreach (var layer in Layers) layer.Frames.Insert(after + 1, duplicate ? (uint[])layer.Frames[after].Clone() : new uint[Width * Height]);
    }
    public void DeleteFrame(int index)
    {
        if (FrameCount <= 1) return;
        foreach (var layer in Layers) layer.Frames.RemoveAt(index);
    }
    public void MoveFrame(int from, int to)
    {
        if (to < 0 || to >= FrameCount) return;
        foreach (var layer in Layers) { var frame = layer.Frames[from]; layer.Frames.RemoveAt(from); layer.Frames.Insert(to, frame); }
    }
    public void AddLayer()
    {
        if (Layers.Count >= MaxLayers) return;
        Layers.Add(new() { Name = $"Layer {Layers.Count + 1}", Frames = Enumerable.Range(0, FrameCount).Select(_ => new uint[Width * Height]).ToList() });
    }
    public void Flip(int layer, int frame, bool horizontal)
    {
        var old = Layers[layer].Frames[frame]; var next = new uint[old.Length];
        for (var y = 0; y < Height; y++) for (var x = 0; x < Width; x++)
            next[y * Width + x] = old[(horizontal ? y : Height - y - 1) * Width + (horizontal ? Width - x - 1 : x)];
        Layers[layer].Frames[frame] = next;
    }
}
