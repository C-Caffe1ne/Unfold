using System.Text;
using System.Text.Json;

namespace Unfold.Core;

public static class PiskelCodec
{
    public const int MaxSourceBytes = 48 * 1024 * 1024;
    private const string Prefix = "data:image/png;base64,";
    public static PixelDocument Load(string path) => Decode(ImageCodec.ReadBounded(path, MaxSourceBytes));
    public static PixelDocument Decode(byte[] data)
    {
        if (data.Length > MaxSourceBytes) throw new InvalidDataException("Piskel source exceeds 48 MiB.");
        using var json = JsonDocument.Parse(data, new() { MaxDepth = 32 });
        var root = json.RootElement;
        if (root.GetProperty("modelVersion").GetInt32() != 2) throw new InvalidDataException("Export this file as Piskel model version 2 first.");
        var sprite = root.GetProperty("piskel");
        var document = new PixelDocument(sprite.GetProperty("width").GetInt32(), sprite.GetProperty("height").GetInt32())
        {
            Name = sprite.TryGetProperty("name", out var name) ? name.GetString() ?? "Unfold Character" : "Unfold Character",
            Description = sprite.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "",
            Fps = sprite.TryGetProperty("fps", out var fps) ? fps.GetDouble() : 12
        };
        if (sprite.TryGetProperty("hiddenFrames", out var hidden) && hidden.GetArrayLength() > 0)
            throw new InvalidDataException("Unhide timeline frames in Piskel before importing.");
        var layers = sprite.GetProperty("layers");
        if (layers.GetArrayLength() is < 1 or > PixelDocument.MaxLayers) throw new InvalidDataException("Use 1–16 layers.");
        document.Layers.Clear();
        int? expectedCount = null;
        foreach (var value in layers.EnumerateArray())
        {
            using var layerJson = JsonDocument.Parse(value.GetString() ?? "", new() { MaxDepth = 16 });
            var layer = layerJson.RootElement;
            var count = layer.GetProperty("frameCount").GetInt32();
            if (count is < 1 or > PixelDocument.MaxFrames || (expectedCount is not null && count != expectedCount))
                throw new InvalidDataException("Every layer needs the same 1–24 frames.");
            expectedCount = count;
            var decoded = new PixelLayer
            {
                Name = layer.GetProperty("name").GetString() ?? "Layer",
                Opacity = layer.TryGetProperty("opacity", out var opacity) ? opacity.GetDouble() : 1,
                Frames = Enumerable.Range(0, count).Select(_ => new uint[document.Width * document.Height]).ToList()
            };
            var seen = new HashSet<int>();
            void ReadChunk(int[][] layout, string png)
            {
                var columns = layout.Length; var rows = columns > 0 ? layout[0].Length : 0;
                if (columns < 1 || rows < 1 || columns > count || rows > count || columns * rows > count ||
                    layout.Any(c => c.Length != rows) || png.Length > 8 * 1024 * 1024 || !png.StartsWith(Prefix, StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid Piskel chunk layout or image.");
                var image = ImageCodec.DecodePng(Convert.FromBase64String(png[Prefix.Length..]), document.Width * columns, document.Height * rows, PixelDocument.MaxSide * PixelDocument.MaxSide * count);
                if (image.Width != document.Width * columns || image.Height != document.Height * rows)
                    throw new InvalidDataException("Piskel chunk geometry mismatch.");
                for (var x = 0; x < columns; x++) for (var y = 0; y < rows; y++)
                {
                    var index = layout[x][y];
                    if (index < 0 || index >= count || !seen.Add(index)) throw new InvalidDataException("Duplicate or invalid frame index.");
                    for (var py = 0; py < document.Height; py++)
                        Array.Copy(image.Pixels, (y * document.Height + py) * image.Width + x * document.Width,
                            decoded.Frames[index], py * document.Width, document.Width);
                }
            }
            if (layer.TryGetProperty("chunks", out var chunks))
            {
                if (chunks.GetArrayLength() is < 1 || chunks.GetArrayLength() > count) throw new InvalidDataException("Invalid chunk count.");
                foreach (var chunk in chunks.EnumerateArray())
                    ReadChunk(chunk.GetProperty("layout").EnumerateArray().Select(c => c.EnumerateArray().Select(i => i.GetInt32()).ToArray()).ToArray(),
                        chunk.GetProperty("base64PNG").GetString() ?? "");
            }
            else ReadChunk(Enumerable.Range(0, count).Select(i => new[] { i }).ToArray(), layer.GetProperty("base64PNG").GetString() ?? "");
            if (seen.Count != count) throw new InvalidDataException("Missing animation frames.");
            document.Layers.Add(decoded);
        }
        document.Validate(); return document;
    }
    public static byte[] Encode(PixelDocument document)
    {
        document.Validate();
        var layers = document.Layers.Select(layer => JsonSerializer.Serialize(new
        {
            name = layer.Name, opacity = layer.Opacity, frameCount = document.FrameCount,
            chunks = new[] { new { layout = Enumerable.Range(0, document.FrameCount).Select(i => new[] { i }).ToArray(),
                base64PNG = Prefix + Convert.ToBase64String(ImageCodec.EncodePng(Sheet(document.Width, document.Height, layer.Frames))) } }
        })).ToArray();
        var data = JsonSerializer.SerializeToUtf8Bytes(new { modelVersion = 2, piskel = new {
            name = document.Name, description = document.Description, fps = document.Fps,
            width = document.Width, height = document.Height, layers, hiddenFrames = Array.Empty<int>() } });
        if (data.Length > MaxSourceBytes) throw new InvalidDataException("Piskel source exceeds 48 MiB.");
        return data;
    }
    public static PixelImage CompositeSheet(PixelDocument document) => Sheet(document.Width, document.Height, Enumerable.Range(0, document.FrameCount).Select(document.Composite).ToList());
    private static PixelImage Sheet(int width, int height, IReadOnlyList<uint[]> frames)
    {
        var sheetWidth = width * frames.Count; var pixels = new uint[sheetWidth * height];
        for (var frame = 0; frame < frames.Count; frame++) for (var y = 0; y < height; y++)
            Array.Copy(frames[frame], y * width, pixels, y * sheetWidth + frame * width, width);
        return new(sheetWidth, height, pixels);
    }
    public static PixelDocument ImportPng(string path)
    {
        var image = ImageCodec.DecodePng(ImageCodec.ReadBounded(path), PixelDocument.MaxSide, PixelDocument.MaxSide);
        var document = new PixelDocument(image.Width, image.Height) { Name = Path.GetFileNameWithoutExtension(path) };
        document.Layers[0].Frames[0] = image.Pixels; return document;
    }
}
