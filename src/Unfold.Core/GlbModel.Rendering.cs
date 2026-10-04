using System.Collections;
using System.Numerics;

namespace Unfold.Core;

public sealed partial class GlbModel
{
    private static float[] Sample(Channel channel, float time)
    {
        var times = channel.Times; var cubic = channel.Interpolation == "CUBICSPLINE";
        var right = System.Array.BinarySearch(times, time);
        if (right >= 0) return channel.Values[cubic ? right * 3 + 1 : right];
        right = ~right;
        if (right == 0) return channel.Values[cubic ? 1 : 0];
        if (right >= times.Length) return channel.Values[cubic ? (times.Length - 1) * 3 + 1 : times.Length - 1];
        var left = right - 1; var span = times[right] - times[left]; var t = (time - times[left]) / span;
        var a = channel.Values[cubic ? left * 3 + 1 : left]; var b = channel.Values[cubic ? right * 3 + 1 : right];
        if (channel.Interpolation == "STEP") return a;
        if (!cubic && channel.Path == "rotation")
        { var q = Quaternion.Slerp(Q(a), Q(b), t); return [q.X, q.Y, q.Z, q.W]; }
        var result = new float[a.Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = cubic ? (2 * t * t * t - 3 * t * t + 1) * a[i] + (t * t * t - 2 * t * t + t) * span * channel.Values[left * 3 + 2][i]
                + (-2 * t * t * t + 3 * t * t) * b[i] + (t * t * t - t * t) * span * channel.Values[right * 3][i] : a[i] + (b[i] - a[i]) * t;
        return result;
    }
    private (Matrix4x4[] World, float[][] Weights) Pose(Clip clip, float time, int lockedRoot, Clip reference)
    {
        var positions = nodes.Select(n => n.Position).ToArray(); var rotations = nodes.Select(n => n.Rotation).ToArray();
        var scales = nodes.Select(n => n.Scale).ToArray(); var weights = nodes.Select(n => n.Weights).ToArray();
        foreach (var c in clip.Channels)
        {
            var v = Sample(c, time);
            switch (c.Path) { case "translation": positions[c.Node] = V3(v); break; case "rotation": rotations[c.Node] = Q(v); break;
                case "scale": scales[c.Node] = V3(v); break; case "weights": weights[c.Node] = v; break; }
        }
        if (lockedRoot >= 0)
        {
            positions[lockedRoot] = nodes[lockedRoot].Position; rotations[lockedRoot] = nodes[lockedRoot].Rotation;
            foreach (var c in reference.Channels.Where(c => c.Node == lockedRoot))
            { var v = Sample(c, reference.Start); if (c.Path == "translation") positions[lockedRoot] = V3(v); else if (c.Path == "rotation") rotations[lockedRoot] = Q(v); }
        }
        var world = new Matrix4x4[nodes.Length];
        foreach (var i in order)
        {
            var local = nodes[i].Matrix ?? Matrix4x4.CreateScale(scales[i]) * Matrix4x4.CreateFromQuaternion(rotations[i]) * Matrix4x4.CreateTranslation(positions[i]);
            world[i] = parents[i] < 0 ? local : local * world[parents[i]];
        }
        return (world, weights);
    }
    private Vector3[] Vertices(int node, Primitive primitive, Matrix4x4[] world, float[] morphWeights)
    {
        var n = nodes[node]; var result = new Vector3[primitive.Positions.Length];
        Matrix4x4[] joints = n.Skin < 0 ? [] : skins[n.Skin].Joints.Select((j, i) => skins[n.Skin].Inverse[i] * world[j]).ToArray();
        for (var i = 0; i < result.Length; i++)
        {
            var pos = primitive.Positions[i];
            for (var m = 0; m < primitive.Morphs.Length && m < morphWeights.Length; m++) pos += primitive.Morphs[m][i] * morphWeights[m];
            if (joints.Length == 0 || primitive.Joints.Length == 0) { result[i] = Vector3.Transform(pos, world[node]); continue; }
            var js = primitive.Joints[i]; var ws = primitive.Weights[i]; var total = ws.X + ws.Y + ws.Z + ws.W;
            result[i] = total <= .00001f ? Vector3.Transform(pos, world[node]) :
                (Vector3.Transform(pos, joints[(int)js.X]) * ws.X + Vector3.Transform(pos, joints[(int)js.Y]) * ws.Y +
                 Vector3.Transform(pos, joints[(int)js.Z]) * ws.Z + Vector3.Transform(pos, joints[(int)js.W]) * ws.W) / total;
        }
        return result;
    }
    public GlbAnimationFrames CreateAnimation(string name, GlbDefinition definition, string referenceClip, double speed = 1)
        => new(this, name, definition, referenceClip, speed);
    private Clip FindClip(string name) => clips.FirstOrDefault(c => c.Name == name) ?? throw new InvalidDataException($"GLB 동작을 찾지 못했어요: {name}");
    private int LockedRoot(GlbDefinition definition)
    {
        if (definition.RootNode is null) return defaultRoot;
        var found = System.Array.FindIndex(nodes, n => n.Name == definition.RootNode);
        return found >= 0 ? found : throw new InvalidDataException("고정할 GLB 루트 뼈대를 찾지 못했어요.");
    }
    internal (Vector3 Min, Vector3 Max) Bounds(GlbDefinition definition, string referenceName)
    {
        var reference = FindClip(referenceName); var pose = Pose(reference, reference.Start, LockedRoot(definition), reference);
        var heading = Matrix4x4.CreateRotationY(definition.Heading * MathF.PI / 180); var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        foreach (var node in visible) foreach (var p in meshes[nodes[node].Mesh]) foreach (var v in Vertices(node, p, pose.World, pose.Weights[node]))
        { var point = Vector3.Transform(v, heading); min = Vector3.Min(min, point); max = Vector3.Max(max, point); }
        if (!float.IsFinite(min.X) || !float.IsFinite(max.Y) || !float.IsFinite(max.X - min.X) || !float.IsFinite(max.Y - min.Y) || Math.Max(max.X - min.X, max.Y - min.Y) < .00001f) throw new InvalidDataException("GLB 모델의 표시 크기가 올바르지 않아요.");
        return (min, max);
    }
    public PixelImage Render(string name, double seconds, GlbDefinition definition, string referenceClip, int size = 192)
        => Render(name, seconds, definition, referenceClip, Bounds(definition, referenceClip), size);
    internal PixelImage Render(string name, double seconds, GlbDefinition definition, string referenceName, (Vector3 Min, Vector3 Max) bounds, int size)
    {
        if (size is < 32 or > 2048 || !double.IsFinite(seconds) || !float.IsFinite(definition.Heading)) throw new InvalidDataException("Invalid GLB render settings.");
        var clip = FindClip(name); var reference = FindClip(referenceName);
        var pose = Pose(clip, clip.Start + (float)Math.Clamp(seconds, 0, clip.End - clip.Start), LockedRoot(definition), reference);
        var heading = Matrix4x4.CreateRotationY(definition.Heading * MathF.PI / 180);
        var width = bounds.Max.X - bounds.Min.X; var height = bounds.Max.Y - bounds.Min.Y;
        var scale = size * .78f / Math.Max(width, height); var center = (bounds.Min.X + bounds.Max.X) * .5f;
        var pixels = new uint[size * size]; var depth = Enumerable.Repeat(float.NegativeInfinity, size * size).ToArray();
        foreach (var node in visible) foreach (var primitive in meshes[nodes[node].Mesh])
        {
            var vertices = Vertices(node, primitive, pose.World, pose.Weights[node]);
            for (var i = 0; i < vertices.Length; i++)
            { var v = Vector3.Transform(vertices[i], heading); vertices[i] = new((v.X - center) * scale + size / 2f, size * .90f - (v.Y - bounds.Min.Y) * scale, v.Z); }
            var material = materials[primitive.Material];
            for (var i = 0; i < primitive.Indices.Length; i += 3)
            {
                var ai = primitive.Indices[i]; var bi = primitive.Indices[i + 1]; var ci = primitive.Indices[i + 2];
                var a = vertices[ai]; var b = vertices[bi]; var c = vertices[ci];
                var area = Edge(a, b, c.X, c.Y); if (!float.IsFinite(area) || Math.Abs(area) < .00001f) continue;
                var left = Math.Clamp((int)MathF.Floor(Math.Min(a.X, Math.Min(b.X, c.X))), 0, size - 1);
                var right = Math.Clamp((int)MathF.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))), 0, size - 1);
                var top = Math.Clamp((int)MathF.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))), 0, size - 1);
                var bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))), 0, size - 1);
                for (var y = top; y <= bottom; y++) for (var x = left; x <= right; x++)
                {
                    var wa = Edge(b, c, x + .5f, y + .5f) / area; var wb = Edge(c, a, x + .5f, y + .5f) / area; var wc = 1 - wa - wb;
                    if (wa < 0 || wb < 0 || wc < 0) continue;
                    var z = wa * a.Z + wb * b.Z + wc * c.Z; var index = y * size + x; if (z <= depth[index]) continue;
                    var uv = primitive.Uvs[ai] * wa + primitive.Uvs[bi] * wb + primitive.Uvs[ci] * wc;
                    var color = Texture(material, uv);
                    if (material.Alpha == "MASK" && (color >> 24) / 255f < material.Cutoff) continue;
                    pixels[index] = color | 0xFF000000; depth[index] = z;
                }
            }
        }
        return new(size, size, pixels);
    }
    private static float Edge(Vector3 a, Vector3 b, float x, float y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);
    private static float Wrap(float value, int mode) => mode == 33071 ? Math.Clamp(value, 0, 1) : mode == 33648 ? 1 - Math.Abs(value - 2 * MathF.Floor(value / 2) - 1) : value - MathF.Floor(value);
    private static uint Texture(Material m, Vector2 uv)
    {
        var color = uint.MaxValue;
        if (m.Texture is { } image)
        {
            var x = Math.Clamp((int)(Wrap(uv.X, m.WrapS) * image.Width), 0, image.Width - 1);
            var y = Math.Clamp((int)(Wrap(uv.Y, m.WrapT) * image.Height), 0, image.Height - 1); color = image.Pixels[y * image.Width + x];
        }
        uint Channel(uint value, float factor) => (uint)Math.Clamp((int)(value * factor), 0, 255);
        return Channel(color >> 24, m.Color.W) << 24 | Channel((color >> 16) & 255, m.Color.X) << 16 | Channel((color >> 8) & 255, m.Color.Y) << 8 | Channel(color & 255, m.Color.Z);
    }
}

/// <summary>Live, bounded frame source: only two rendered images retained, never a pre-baked atlas.</summary>
public sealed class GlbAnimationFrames : IReadOnlyList<AnimationFrame>
{
    public const int MaxFrameSize = 1024;
    private readonly GlbModel model;
    private readonly string name, reference;
    private readonly GlbDefinition definition;
    private readonly double speed;
    private readonly (Vector3 Min, Vector3 Max) bounds;
    private readonly AnimationFrame firstFrame;
    private readonly Dictionary<(int Index, int Size), AnimationFrame> cache = [];
    private readonly object gate = new();
    public int Count { get; }
    public double DurationSeconds { get; }
    public TimeSpan FrameDuration => TimeSpan.FromSeconds(DurationSeconds / Count);
    public GlbAnimationFrames(GlbModel model, string name, GlbDefinition definition, string reference, double speed = 1)
    {
        if (!double.IsFinite(speed) || speed is < .25 or > 3) throw new InvalidDataException("GLB 재생 속도는 0.25~3배로 지정해 주세요.");
        this.model = model; this.name = name; this.definition = definition; this.reference = reference; this.speed = speed;
        DurationSeconds = (model.Animations.FirstOrDefault(c => c.Name == name)?.Duration ?? throw new InvalidDataException("Unknown GLB clip.")) / speed;
        Count = Math.Max(1, (int)Math.Ceiling(DurationSeconds * 20)); bounds = model.Bounds(definition, reference);
        firstFrame = RenderFrame(0, 192);
    }
    public AnimationFrame this[int index] => GetFrame(index, 192);
    public AnimationFrame GetFrame(int index, int pixelSize)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (pixelSize is < 32 or > MaxFrameSize) throw new ArgumentOutOfRangeException(nameof(pixelSize));
        if (index == 0 && pixelSize == 192) return firstFrame;
        lock (gate)
        {
            var key = (index, pixelSize);
            if (cache.TryGetValue(key, out var frame)) return frame;
            frame = RenderFrame(index, pixelSize);
            cache.Clear(); cache[key] = frame; return frame;
        }
    }
    private AnimationFrame RenderFrame(int index, int size)
    {
        // Four coverage samples per output pixel; average premultiplied colors so
        // transparent edges stay clean on both light and dark desktop backgrounds.
        var image = model.Render(name, index * FrameDuration.TotalSeconds * speed, definition, reference, bounds, size * 2);
        var pixels = new uint[size * size];
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            uint alpha = 0, red = 0, green = 0, blue = 0;
            for (var dy = 0; dy < 2; dy++) for (var dx = 0; dx < 2; dx++)
            {
                var color = image.Pixels[(y * 2 + dy) * image.Width + x * 2 + dx]; var a = color >> 24;
                alpha += a; red += ((color >> 16) & 255) * a; green += ((color >> 8) & 255) * a; blue += (color & 255) * a;
            }
            if (alpha == 0) continue;
            pixels[y * size + x] = ((alpha + 2) / 4) << 24 | ((red + alpha / 2) / alpha) << 16 |
                ((green + alpha / 2) / alpha) << 8 | (blue + alpha / 2) / alpha;
        }
        return new(new PixelImage(size, size, pixels), FrameDuration);
    }
    public IEnumerator<AnimationFrame> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
