using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;

namespace Unfold.Core;

public sealed record GlbClipInfo(string Name, double Duration);
public sealed record GlbDefinition(string File, float Heading = 0, string? RootNode = null);

/// <summary>Bounded, embedded glTF 2.0 reader. No URI, script or network execution.</summary>
public sealed partial class GlbModel
{
    private sealed record Node(string Name, Vector3 Position, Quaternion Rotation, Vector3 Scale,
        Matrix4x4? Matrix, int[] Children, int Mesh, int Skin, float[] Weights);
    private sealed record Skin(int[] Joints, Matrix4x4[] Inverse);
    private sealed record Primitive(Vector3[] Positions, Vector2[] Uvs, Vector4[] Joints, Vector4[] Weights,
        Vector3[][] Morphs, int[] Indices, int Material);
    private sealed record Material(PixelImage? Texture, Vector4 Color, string Alpha, float Cutoff, int WrapS, int WrapT);
    private sealed record Channel(int Node, string Path, string Interpolation, float[] Times, AccessorData Values);
    private sealed record Clip(string Name, Channel[] Channels, float Start, float End);
    private readonly Node[] nodes;
    private readonly Skin[] skins;
    private readonly Primitive[][] meshes;
    private readonly Material[] materials;
    private readonly Clip[] clips;
    private readonly int[] order, visible;
    private readonly int[] parents;
    private readonly int defaultRoot;
    public IReadOnlyList<GlbClipInfo> Animations { get; }
    public IReadOnlyList<string> NodeNames => nodes.Select(n => n.Name).ToArray();
    public int TriangleCount { get; }
    public int TextureCount { get; }
    public int JointCount => skins.SelectMany(s => s.Joints).Distinct().Count();
    public static GlbModel Load(string path) => FromFile(path);
    public static GlbModel Parse(byte[] bytes)
    {
        try { return new GlbModel(bytes); }
        catch (Exception e) when (e is JsonException or ArgumentException or IndexOutOfRangeException or OverflowException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("GLB 데이터가 손상됐거나 지원하지 않는 형식이에요.", e); }
    }
    private GlbModel(byte[] bytes)
    {
        if (bytes.Length is < 28 or > ImageCodec.MaxFileBytes || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x46546C67 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) != 2 || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)) != bytes.Length)
            throw new InvalidDataException("32 MiB 이하의 GLB 2.0 파일을 선택해 주세요.");
        var offset = 12; ReadOnlyMemory<byte> json = default, binary = default;
        while (offset < bytes.Length)
        {
            if (offset + 8 > bytes.Length) throw new InvalidDataException("Incomplete GLB chunk.");
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4)); offset += 8;
            if (length <= 0 || length % 4 != 0 || length > bytes.Length - offset) throw new InvalidDataException("Invalid GLB chunk.");
            if (json.IsEmpty && type != 0x4E4F534A) throw new InvalidDataException("GLB JSON must be first.");
            if (type == 0x4E4F534A) { if (!json.IsEmpty) throw new InvalidDataException("Duplicate GLB JSON."); json = bytes.AsMemory(offset, length); }
            else if (type == 0x004E4942) { if (!binary.IsEmpty) throw new InvalidDataException("Duplicate GLB buffer."); binary = bytes.AsMemory(offset, length); }
            offset += length;
        }
        if (json.IsEmpty || binary.IsEmpty || json.Length > 8 * 1024 * 1024) throw new InvalidDataException("GLB needs embedded JSON and binary data.");
        using var document = JsonDocument.Parse(json, new() { MaxDepth = 64 }); var root = document.RootElement;
        if (root.GetProperty("asset").GetProperty("version").GetString() != "2.0" || Array(root, "extensionsRequired").Length != 0)
            throw new InvalidDataException("필수 확장이 없는 glTF 2.0 모델을 사용해 주세요.");
        var buffers = Array(root, "buffers");
        if (buffers.Length != 1 || buffers[0].TryGetProperty("uri", out _) || Int(buffers[0], "byteLength") <= 0 || Int(buffers[0], "byteLength") > binary.Length || binary.Length - Int(buffers[0], "byteLength") > 3)
            throw new InvalidDataException("GLB에 포함된 하나의 버퍼만 지원해요.");
        var views = Array(root, "bufferViews"); var accessors = Array(root, "accessors");
        if (accessors.Length > 20000 || views.Length > 20000) throw new InvalidDataException("Too many GLB data blocks.");
        foreach (var view in views)
            if (Int(view, "buffer") != 0 || Int(view, "byteOffset") < 0 || Int(view, "byteLength") <= 0 ||
                (long)Int(view, "byteOffset") + Int(view, "byteLength") > Int(buffers[0], "byteLength")) throw new InvalidDataException("GLB buffer range is invalid.");
        var reader = new AccessorReader(binary, views, accessors);
        var images = Array(root, "images"); TextureCount = images.Length;
        if (images.Length > 64) throw new InvalidDataException("GLB textures exceed 64.");
        long decoded = 0;
        var pixels = images.Select(im =>
        {
            if (im.TryGetProperty("uri", out _) || im.GetProperty("mimeType").GetString() != "image/png")
                throw new InvalidDataException("현재 GLB 텍스처는 파일 내부의 PNG만 지원해요.");
            var v = views[im.GetProperty("bufferView").GetInt32()];
            var image = ImageCodec.DecodePng(binary.Span.Slice(Int(v, "byteOffset"), Int(v, "byteLength")).ToArray(), 2048, 2048, 4 * 1024 * 1024);
            decoded += (long)image.Width * image.Height * 4;
            if (decoded > 64 * 1024 * 1024) throw new InvalidDataException("GLB textures exceed 64 MiB decoded.");
            return image;
        }).ToArray();
        var textures = Array(root, "textures"); var samplers = Array(root, "samplers");
        var mats = Array(root, "materials");
        if (mats.Length > 128) throw new InvalidDataException("Too many materials.");
        materials = mats.Select(m =>
        {
            var p = m.TryGetProperty("pbrMetallicRoughness", out var property) ? property : default;
            PixelImage? texture = null; var ws = 10497; var wt = 10497;
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("baseColorTexture", out var tx))
            {
                if (Int(tx, "texCoord") != 0 || tx.TryGetProperty("extensions", out _)) throw new InvalidDataException("GLB texture transforms and UV sets other than UV0 are not supported.");
                var t = textures[tx.GetProperty("index").GetInt32()]; texture = pixels[t.GetProperty("source").GetInt32()];
                if (t.TryGetProperty("sampler", out var si)) { var s = samplers[si.GetInt32()]; ws = Int(s, "wrapS", 10497); wt = Int(s, "wrapT", 10497); }
            }
            var alpha = String(m, "alphaMode", "OPAQUE");
            if (alpha is not ("OPAQUE" or "MASK" or "BLEND")) throw new InvalidDataException("지원하지 않는 GLB 알파 재질 형식이에요.");
            return new Material(texture, Vec4(p, "baseColorFactor", Vector4.One), alpha, Float(m, "alphaCutoff", .5f), ws, wt);
        }).Append(new(null, Vector4.One, "OPAQUE", .5f, 10497, 10497)).ToArray();
        var meshJson = Array(root, "meshes");
        if (meshJson.Length is < 1 or > 256) throw new InvalidDataException("GLB meshes exceed limits.");
        var vertexTotal = 0; var triangleTotal = 0;
        meshes = meshJson.Select(m => Array(m, "primitives").Select(p =>
        {
            if (Int(p, "mode", 4) != 4 || p.TryGetProperty("extensions", out _)) throw new InvalidDataException("압축되지 않은 삼각형 GLB 모델만 지원해요.");
            var a = p.GetProperty("attributes"); var pos = reader.Read(a.GetProperty("POSITION").GetInt32(), 3).Convert(V3);
            vertexTotal += pos.Length;
            var uv = a.TryGetProperty("TEXCOORD_0", out var ui) ? reader.Read(ui.GetInt32(), 2).Convert(v => new Vector2(v[0], v[1])) : new Vector2[pos.Length];
            var joints = a.TryGetProperty("JOINTS_0", out var ji) ? reader.Read(ji.GetInt32(), 4).Convert(V4) : [];
            var weights = a.TryGetProperty("WEIGHTS_0", out var wi) ? reader.Read(wi.GetInt32(), 4).Convert(V4) : [];
            if (a.TryGetProperty("JOINTS_1", out _) || joints.Length != weights.Length || uv.Length != pos.Length || (joints.Length != 0 && joints.Length != pos.Length))
                throw new InvalidDataException("GLB vertex attributes are inconsistent or use more than four joints.");
            var morphs = Array(p, "targets").Select(t => t.TryGetProperty("POSITION", out var mi) ? reader.Read(mi.GetInt32(), 3).Convert(V3) : new Vector3[pos.Length]).ToArray();
            if (morphs.Length > 32 || morphs.Any(x => x.Length != pos.Length)) throw new InvalidDataException("Invalid GLB morph targets.");
            var indices = p.TryGetProperty("indices", out var ii) ? reader.Read(ii.GetInt32(), 1).Convert(v => checked((int)v[0])) : Enumerable.Range(0, pos.Length).ToArray();
            if (indices.Length % 3 != 0 || indices.Any(i => i < 0 || i >= pos.Length)) throw new InvalidDataException("Invalid GLB triangle indices.");
            triangleTotal += indices.Length / 3;
            if (vertexTotal > 300000 || triangleTotal > 100000) throw new InvalidDataException("펫 모델은 삼각형 10만 개 이하로 줄여 주세요.");
            var material = Int(p, "material", materials.Length - 1);
            if (material < 0 || material >= materials.Length) throw new InvalidDataException("Invalid material reference.");
            return new Primitive(pos, uv, joints, weights, morphs, indices, material);
        }).ToArray()).ToArray(); TriangleCount = triangleTotal;
        if (meshes.Any(m => m.Length == 0)) throw new InvalidDataException("GLB meshes must contain primitives.");
        var nodeJson = Array(root, "nodes");
        if (nodeJson.Length is < 1 or > 2048) throw new InvalidDataException("Too many GLB nodes.");
        nodes = nodeJson.Select((n, i) => new Node(String(n, "name", $"Node_{i}"), V3(Floats(n, "translation", [0, 0, 0])),
            Q(Floats(n, "rotation", [0, 0, 0, 1])), V3(Floats(n, "scale", [1, 1, 1])),
            n.TryGetProperty("matrix", out var matrix) ? Matrix(matrix.EnumerateArray().Select(x => x.GetSingle()).ToArray()) : null,
            Array(n, "children").Select(x => x.GetInt32()).ToArray(), Int(n, "mesh", -1), Int(n, "skin", -1),
            Floats(n, "weights", Int(n, "mesh", -1) is var mesh && mesh >= 0 ? Floats(meshJson[mesh], "weights", []) : []))).ToArray();
        parents = Enumerable.Repeat(-1, nodes.Length).ToArray();
        for (var i = 0; i < nodes.Length; i++) foreach (var child in nodes[i].Children)
        {
            if (child < 0 || child >= nodes.Length || parents[child] != -1) throw new InvalidDataException("GLB node graph has invalid or shared children.");
            parents[child] = i;
        }
        var ordered = new List<int>(); var states = new byte[nodes.Length];
        void Visit(int i) { if (states[i] == 1) throw new InvalidDataException("GLB node graph contains a cycle."); if (states[i] == 2) return;
            states[i] = 1; if (parents[i] >= 0) Visit(parents[i]); states[i] = 2; ordered.Add(i); }
        for (var i = 0; i < nodes.Length; i++) Visit(i); order = ordered.ToArray();
        var scenes = Array(root, "scenes"); if (scenes.Length == 0) throw new InvalidDataException("GLB scene is missing.");
        var scene = scenes[Int(root, "scene")]; var seen = new HashSet<int>();
        void Show(int i) { if (i < 0 || i >= nodes.Length) throw new InvalidDataException("Invalid scene node."); if (!seen.Add(i)) return; foreach (var c in nodes[i].Children) Show(c); }
        foreach (var i in Array(scene, "nodes")) Show(i.GetInt32()); visible = seen.Where(i => nodes[i].Mesh >= 0).ToArray();
        if (visible.Length == 0) throw new InvalidDataException("GLB scene contains no visible mesh.");
        skins = Array(root, "skins").Select(s =>
        {
            var joints = Array(s, "joints").Select(x => x.GetInt32()).ToArray();
            if (joints.Length is < 1 or > 512 || joints.Any(i => i < 0 || i >= nodes.Length)) throw new InvalidDataException("Invalid GLB skeleton.");
            var inverse = s.TryGetProperty("inverseBindMatrices", out var ib) ? reader.Read(ib.GetInt32(), 16).Convert(Matrix) : Enumerable.Repeat(Matrix4x4.Identity, joints.Length).ToArray();
            if (inverse.Length != joints.Length) throw new InvalidDataException("GLB bind matrices differ from joints.");
            return new Skin(joints, inverse);
        }).ToArray();
        foreach (var n in nodes)
        {
            if (n.Mesh >= meshes.Length || n.Skin >= skins.Length || n.Mesh < -1 || n.Skin < -1) throw new InvalidDataException("Invalid GLB mesh or skin reference.");
            if (n.Mesh < 0 || n.Skin < 0) continue;
            foreach (var p in meshes[n.Mesh]) foreach (var j in p.Joints)
                if (new[] { j.X, j.Y, j.Z, j.W }.Any(v => v < 0 || v >= skins[n.Skin].Joints.Length || v != MathF.Truncate(v))) throw new InvalidDataException("Invalid vertex joint.");
        }
        // Mesh instances also count toward the draw budget, bounding the reusable
        // transparent triangle list as well as the amount of raster work per frame.
        if (visible.Sum(i => meshes[nodes[i].Mesh].Sum(p => (long)p.Indices.Length / 3)) > 100000)
            throw new InvalidDataException("화면에 표시되는 펫 모델의 삼각형을 10만 개 이하로 줄여 주세요.");
        var animations = Array(root, "animations");
        if (animations.Length > 128) throw new InvalidDataException("GLB animations exceed 128.");
        clips = animations.Select((a, index) =>
        {
            var sam = Array(a, "samplers"); var channels = Array(a, "channels");
            if (channels.Length > 8192) throw new InvalidDataException("Too many GLB animation channels.");
            var result = channels.Select(c =>
            {
                var target = c.GetProperty("target"); var node = target.GetProperty("node").GetInt32(); var path = target.GetProperty("path").GetString()!;
                if (node < 0 || node >= nodes.Length || path is not ("translation" or "rotation" or "scale" or "weights")) throw new InvalidDataException("Unsupported GLB animation target.");
                var s = sam[c.GetProperty("sampler").GetInt32()]; var interpolation = String(s, "interpolation", "LINEAR");
                if (interpolation is not ("LINEAR" or "STEP" or "CUBICSPLINE")) throw new InvalidDataException("Unsupported GLB interpolation.");
                var times = reader.Read(s.GetProperty("input").GetInt32(), 1).Data;
                if (times.Length == 0 || times[0] < 0 || times[^1] > 120 || times.Zip(times.Skip(1)).Any(p => p.First >= p.Second)) throw new InvalidDataException($"GLB 애니메이션 '{String(a, "name", $"Animation_{index + 1}")}'의 시간 키가 중복·역순이거나 0~120초 범위를 벗어났어요. 원본에서 시간 키를 정리해 다시 내보내 주세요.");
                var width = path == "rotation" ? 4 : path == "weights" ? meshes[nodes[node].Mesh][0].Morphs.Length : 3;
                if (width < 1 || nodes[node].Matrix is not null) throw new InvalidDataException("Invalid animated GLB node.");
                var values = reader.Read(s.GetProperty("output").GetInt32(), path == "weights" ? 1 : width);
                if (path == "weights") values = values.Reshape(width);
                if (values.Length != times.Length * (interpolation == "CUBICSPLINE" ? 3 : 1)) throw new InvalidDataException("GLB animation sample count differs.");
                return new Channel(node, path, interpolation, times, values);
            }).ToArray();
            if (result.Select(c => (c.Node, c.Path)).Distinct().Count() != result.Length) throw new InvalidDataException("Duplicate GLB animation channel.");
            var start = result.Length == 0 ? 0 : result.Min(c => c.Times[0]); var end = result.Length == 0 ? 0 : result.Max(c => c.Times[^1]);
            return new Clip(String(a, "name", $"Animation_{index + 1}"), result, start, end);
        }).ToArray();
        // glTF names are display labels, not unique IDs. Keep the first spelling
        // and reserve every authored name before adding stable suffixes for duplicates.
        var reservedNames = clips.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < clips.Length; i++)
        {
            var originalName = clips[i].Name;
            if (usedNames.Add(originalName)) continue;
            var suffix = 2; string uniqueName;
            do { uniqueName = $"{originalName} ({suffix++})"; }
            while (!reservedNames.Add(uniqueName));
            usedNames.Add(uniqueName); clips[i] = clips[i] with { Name = uniqueName };
        }
        if (clips.Length == 0) clips = [new("Static", [], 0, 1)];
        Animations = clips.Select(c => new GlbClipInfo(c.Name, Math.Max(.05, c.End - c.Start))).ToArray();
        // Highest animated joint of the largest skin is the locomotion/orientation root.
        var body = skins.OrderByDescending(s => s.Joints.Length).FirstOrDefault();
        var animated = clips.SelectMany(c => c.Channels).Where(c => c.Path != "weights").Select(c => c.Node).ToHashSet();
        defaultRoot = body is null ? animated.Where(i => visible.Any(v => IsAncestor(i, v))).OrderBy(Depth).FirstOrDefault(-1) : body.Joints.Where(animated.Contains).OrderBy(Depth).FirstOrDefault(-1);
    }
    private bool IsAncestor(int ancestor, int node) { while (node >= 0) { if (node == ancestor) return true; node = parents[node]; } return false; }
    private int Depth(int i) { var count = 0; while (parents[i] >= 0) { i = parents[i]; count++; } return count; }
    private static JsonElement[] Array(JsonElement e, string key) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v.EnumerateArray().ToArray() : [];
    private static int Int(JsonElement e, string key, int fallback = 0) => e.TryGetProperty(key, out var v) ? v.GetInt32() : fallback;
    private static float Float(JsonElement e, string key, float fallback) => e.TryGetProperty(key, out var v) ? v.GetSingle() : fallback;
    private static string String(JsonElement e, string key, string fallback) => e.TryGetProperty(key, out var v) ? v.GetString() ?? fallback : fallback;
    private static float[] Floats(JsonElement e, string key, float[] fallback)
    {
        var result = e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var v) ? v.EnumerateArray().Select(x => x.GetSingle()).ToArray() : fallback;
        if (result.Any(x => !float.IsFinite(x))) throw new InvalidDataException("Nonfinite GLB value."); return result;
    }
    private static Vector3 V3(ReadOnlySpan<float> v) => v.Length == 3 ? new(v[0], v[1], v[2]) : throw new InvalidDataException("Invalid GLB vector.");
    private static Vector4 V4(ReadOnlySpan<float> v) => v.Length == 4 ? new(v[0], v[1], v[2], v[3]) : throw new InvalidDataException("Invalid GLB vector.");
    private static Vector4 Vec4(JsonElement e, string key, Vector4 fallback) => V4(Floats(e, key, [fallback.X, fallback.Y, fallback.Z, fallback.W]));
    private static Quaternion Q(ReadOnlySpan<float> v) { var q = V4(v); var result = new Quaternion(q.X, q.Y, q.Z, q.W); if (result.LengthSquared() < .00001f) throw new InvalidDataException("Invalid GLB quaternion."); return Quaternion.Normalize(result); }
    private static Matrix4x4 Matrix(ReadOnlySpan<float> v) => v.Length == 16 && Finite(v) ? new(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]) : throw new InvalidDataException("Invalid GLB matrix.");

    private static bool Finite(ReadOnlySpan<float> values)
    { foreach (var value in values) if (!float.IsFinite(value)) return false; return true; }

    // A single contiguous array per accessor. Animation channels can share the
    // decoded data without allocating one managed object per key or vertex.
    private sealed record AccessorData(float[] Data, int Width)
    {
        public int Length => Data.Length / Width;
        public ReadOnlySpan<float> this[int index] => Data.AsSpan(index * Width, Width);
        public AccessorData Reshape(int width) => Data.Length % width == 0
            ? new(Data, width) : throw new InvalidDataException("Invalid morph weights.");
        public T[] Convert<T>(Func<ReadOnlySpan<float>, T> convert)
        {
            var result = new T[Length];
            for (var i = 0; i < result.Length; i++) result[i] = convert(this[i]);
            return result;
        }
    }

    private sealed class AccessorReader(ReadOnlyMemory<byte> binary, JsonElement[] views, JsonElement[] accessors)
    {
        private readonly Dictionary<int, AccessorData> cache = [];
        private long elements;
        public AccessorData Read(int index, int expected)
        {
            var a = accessors[index]; var width = a.GetProperty("type").GetString() switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT4" => 16, _ => 0 };
            if (width != expected || a.TryGetProperty("sparse", out _)) throw new InvalidDataException("GLB sparse accessors or mismatched types are not supported.");
            if (cache.TryGetValue(index, out var cached)) return cached;
            var count = Int(a, "count"); if (count is < 1 or > 300000 || (elements += (long)count * width) > 8_000_000) throw new InvalidDataException("GLB decoded data exceeds limits.");
            var component = Int(a, "componentType"); var size = component switch { 5120 or 5121 => 1, 5122 or 5123 => 2, 5125 or 5126 => 4, _ => 0 };
            if (size == 0) throw new InvalidDataException("Unsupported GLB component.");
            var v = views[a.GetProperty("bufferView").GetInt32()]; var stride = Int(v, "byteStride", size * width); var start = Int(a, "byteOffset");
            if (start < 0 || stride < size * width || stride > 252 || (long)start + (long)(count - 1) * stride + size * width > Int(v, "byteLength")) throw new InvalidDataException("GLB accessor exceeds its buffer view.");
            start += Int(v, "byteOffset"); var normalized = a.TryGetProperty("normalized", out var norm) && norm.GetBoolean(); var result = new float[count * width];
            for (var i = 0; i < count; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    var o = start + i * stride + j * size; var span = binary.Span.Slice(o, size);
                    float value = component switch { 5120 => (sbyte)binary.Span[o], 5121 => binary.Span[o], 5122 => BinaryPrimitives.ReadInt16LittleEndian(span), 5123 => BinaryPrimitives.ReadUInt16LittleEndian(span), 5125 => BinaryPrimitives.ReadUInt32LittleEndian(span), _ => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(span)) };
                    if (normalized) value = component switch { 5120 => Math.Max(-1, value / 127), 5121 => value / 255, 5122 => Math.Max(-1, value / 32767), 5123 => value / 65535, _ => value };
                    if (!float.IsFinite(value)) throw new InvalidDataException("Nonfinite GLB data."); result[i * width + j] = value;
                }
            }
            var decoded = new AccessorData(result, width); cache[index] = decoded; return decoded;
        }
    }
}
