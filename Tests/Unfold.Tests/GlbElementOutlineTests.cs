using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class GlbElementOutlineTests
{
    private sealed record Part(float Left, float Right, float Bottom = 0, float Top = 1,
        float Z = 0, string Mode = "OPAQUE", float Alpha = 1);

    private static byte[] Fixture(Part[] parts, bool separateNodes = false, bool separateMaterials = false)
    {
        using var buffer = new MemoryStream(); using var values = new BinaryWriter(buffer);
        var views = new List<object>(); var accessors = new List<object>(); var primitives = new List<object>();
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i]; var offset = (int)buffer.Position;
            float[] vertices = [p.Left, p.Bottom, p.Z, p.Right, p.Bottom, p.Z, p.Right, p.Top, p.Z,
                p.Left, p.Bottom, p.Z, p.Right, p.Top, p.Z, p.Left, p.Top, p.Z];
            foreach (var value in vertices) values.Write(value);
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = vertices.Length * 4 });
            accessors.Add(new { bufferView = i, componentType = 5126, count = 6, type = "VEC3" });
            primitives.Add(new { attributes = new { POSITION = i }, material = separateMaterials ? i : 0 });
        }
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)buffer.Length } },
            bufferViews = views, accessors,
            materials = (separateMaterials ? parts : parts[..1]).Select(p => new
            { alphaMode = p.Mode, pbrMetallicRoughness = new { baseColorFactor = new[] { 1f, 1f, 1f, p.Alpha } } }).ToArray(),
            meshes = separateNodes ? primitives.Select(p => new { primitives = new[] { p } }).ToArray()
                : new[] { new { primitives = primitives.ToArray() } },
            nodes = Enumerable.Range(0, separateNodes ? parts.Length : 1).Select(i => new { mesh = i }).ToArray(),
            scenes = new[] { new { nodes = Enumerable.Range(0, separateNodes ? parts.Length : 1).ToArray() } }, scene = 0
        });
        var padded = (json.Length + 3) / 4 * 4;
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write(28 + padded + (int)buffer.Length);
        writer.Write(padded); writer.Write(0x4E4F534Au); writer.Write(json);
        for (var i = json.Length; i < padded; i++) writer.Write((byte)' ');
        writer.Write((int)buffer.Length); writer.Write(0x004E4942u); writer.Write(buffer.ToArray());
        return output.ToArray();
    }

    private static PixelImage Render(byte[] bytes)
        => GlbModel.Parse(bytes).Render("Static", 0, new("model.glb"), "Static");

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void VisibleMeshOrMaterialBoundariesReceiveThinInternalOutlines(bool separateNodes, bool separateMaterials)
    {
        var image = Render(Fixture([new(-.5f, 0), new(0, .5f)], separateNodes, separateMaterials));
        Assert.InRange(image.Pixels[96 * 192 + 95] & 255, 240u, 245u);
        Assert.InRange(image.Pixels[96 * 192 + 96] & 255, 240u, 245u);
        Assert.Equal(uint.MaxValue, image.Pixels[96 * 192 + 90]);
        Assert.Equal(uint.MaxValue, image.Pixels[96 * 192 + 101]);
        Assert.Equal(0u, image.Pixels[0]);
    }

    [Fact]
    public void SameMaterialPrimitivesAndTriangleDiagonalsDoNotBecomeWireframes()
    {
        var image = Render(Fixture([new(-.5f, 0), new(0, .5f)]));
        // Exterior subpixel coverage is allowed; every opaque surface sample
        // must stay white, including shared primitive and triangle boundaries.
        Assert.All(image.Pixels.Where(p => p >> 24 == 255), p => Assert.Equal(uint.MaxValue, p));
        Assert.Equal(uint.MaxValue, image.Pixels[96 * 192 + 95]);
        Assert.Equal(uint.MaxValue, image.Pixels[96 * 192 + 96]);
    }

    [Theory]
    [InlineData("OPAQUE", 1f, -.1f)]
    [InlineData("BLEND", 0f, .1f)]
    [InlineData("MASK", .1f, .1f)]
    public void HiddenTransparentAndDiscardedPartsDoNotLeaveInternalLines(string mode, float alpha, float z)
    {
        Part body = new(-.5f, .5f);
        var baseline = Render(Fixture([body]));
        var image = Render(Fixture([body, new(-.2f, .2f, .2f, .8f, z, mode, alpha)],
            separateNodes: true, separateMaterials: true));
        Assert.Equal(baseline.Pixels, image.Pixels);
    }

    [Fact]
    public void PartOutlinesPreserveTranslucencyAndDoNotDarkenUnrelatedPixels()
    {
        var image = Render(Fixture([new(-.5f, 0, Mode: "BLEND", Alpha: .5f),
            new(0, .5f, Mode: "BLEND", Alpha: .5f)], separateMaterials: true));
        Assert.Equal(127u, image.Pixels[96 * 192 + 96] >> 24);
        Assert.Equal(0x7FFFFFFFu, image.Pixels[96 * 192 + 101]);
        Assert.True((image.Pixels[96 * 192 + 96] & 255) < 255);
    }

    [Fact]
    public void ElementOutlinesPersistThroughPreviewSaveReopenAndRotation()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "parts.glb");
        var bytes = Fixture([new(-.5f, 0), new(0, .5f)], separateMaterials: true);
        File.WriteAllBytes(path, bytes); var draft = new GlbPetDraft(path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var package = draft.Save(library);
        Assert.Equal(draft.Preview("idle")[0].Image.Pixels, package.LoadAnimation("idle")[0].Image.Pixels);
        Assert.Equal(draft.Preview("idle")[0].Image.Pixels, new GlbPetDraft(package).Preview("idle")[0].Image.Pixels);
        var model = draft.Model; var frames = model.CreateAnimation("Static", new("model.glb"), "Static");
        var pixels = new uint[384 * 384]; frames.RenderFrameInto(0, 384, pixels); var initial = pixels.ToArray();
        frames.RenderPreviewFrameInto(0, 384, pixels, new(0, 45, 0));
        frames.RenderFrameInto(0, 384, pixels); Assert.Equal(initial, pixels);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }
}
