using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class GlbOutlineTests
{
    private static byte[] Fixture(string mode = "OPAQUE", float alpha = 1)
    {
        // Four rectangles surround a real transparent hole. Each rectangle has
        // two triangles, exposing unwanted outlines on internal mesh seams.
        var vertices = new List<float>();
        void Rectangle(float left, float bottom, float right, float top)
        {
            vertices.AddRange([left, bottom, 0, right, bottom, 0, right, top, 0,
                left, bottom, 0, right, top, 0, left, top, 0]);
        }
        Rectangle(-.5f, 0, -.15f, 1); Rectangle(.15f, 0, .5f, 1);
        Rectangle(-.15f, 0, .15f, .35f); Rectangle(-.15f, .65f, .15f, 1);
        using var binary = new MemoryStream(); using var values = new BinaryWriter(binary);
        foreach (var value in vertices) values.Write(value);
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)binary.Length } },
            bufferViews = new[] { new { buffer = 0, byteLength = (int)binary.Length } },
            accessors = new[] { new { bufferView = 0, componentType = 5126, count = vertices.Count / 3, type = "VEC3" } },
            materials = new[] { new { alphaMode = mode, pbrMetallicRoughness = new { baseColorFactor = new[] { 1f, 1f, 1f, alpha } } } },
            meshes = new[] { new { primitives = new[] { new { attributes = new { POSITION = 0 }, material = 0 } } } },
            nodes = new[] { new { mesh = 0 } }, scenes = new[] { new { nodes = new[] { 0 } } }, scene = 0
        });
        var padded = (json.Length + 3) / 4 * 4;
        using var output = new MemoryStream(); using var writer = new BinaryWriter(output);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write(28 + padded + (int)binary.Length);
        writer.Write(padded); writer.Write(0x4E4F534Au); writer.Write(json);
        for (var i = json.Length; i < padded; i++) writer.Write((byte)' ');
        writer.Write((int)binary.Length); writer.Write(0x004E4942u); writer.Write(binary.ToArray());
        return output.ToArray();
    }

    [Theory]
    [InlineData(192, 13)]
    [InlineData(384, 26)]
    [InlineData(576, 38)]
    [InlineData(2048, 144)]
    public void FixedOnePercentOutlineScalesWithResolutionAndStaysOutsideTheSurface(int size, int alpha)
    {
        var model = GlbModel.Parse(Fixture());
        var image = model.Render("Static", 0, new("model.glb"), "Static", size);
        var row = size / 2 * size;
        var left = Enumerable.Range(0, size).First(x => image.Pixels[row + x] == uint.MaxValue);
        var stroke = Enumerable.Range(0, left).Count(x => image.Pixels[row + x] >> 24 != 0);
        Assert.Equal(1, stroke);
        Assert.InRange((int)(image.Pixels[row + left - 1] >> 24), alpha - 1, alpha + 1);
        Assert.All(image.Pixels.Skip(row + left).Take(size / 10), p => Assert.Equal(uint.MaxValue, p));
        Assert.Equal(0u, image.Pixels[row]);
        Assert.Equal(0u, image.Pixels[row + size - 1]);
        Assert.Equal(0u, image.Pixels[row + size / 2]);
        Assert.Contains(image.Pixels, p => p != 0 && (p & 0xFFFFFF) == 0);
    }

    [Theory]
    [InlineData("BLEND", .5f, 127u)]
    [InlineData("BLEND", 0f, 0u)]
    [InlineData("MASK", .1f, 0u)]
    public void OutlineRespectsTranslucencyAndDiscardedPixels(string mode, float alpha, uint expectedAlpha)
    {
        var model = GlbModel.Parse(Fixture(mode, alpha));
        var image = model.Render("Static", 0, new("model.glb"), "Static", 384);
        Assert.All(image.Pixels, p => Assert.InRange(p >> 24, 0u, expectedAlpha));
        if (expectedAlpha == 0) Assert.All(image.Pixels, p => Assert.Equal(0u, p));
        else
        {
            Assert.Contains(image.Pixels, p => p >> 24 == expectedAlpha && (p & 0xFFFFFF) == 0xFFFFFF);
            Assert.Contains(image.Pixels, p => p != 0 && (p & 0xFFFFFF) == 0);
        }
    }

    [Fact]
    public void FirstImportPreviewSaveExportAndReopenShareTheOutlinedRenderer()
    {
        using var temp = new TempDirectory(); var input = Path.Combine(temp.Path, "new.glb");
        var bytes = Fixture(); File.WriteAllBytes(input, bytes);
        var draft = new GlbPetDraft(input); var preview = draft.Preview("idle")[0].Image;
        Assert.Contains(preview.Pixels, p => p != 0 && (p & 0xFFFFFF) == 0);
        Assert.Contains(preview.Pixels, p => p >> 24 is > 0 and < 255);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var pet = draft.Save(library);
        Assert.Equal(preview.Pixels, pet.LoadAnimation("idle")[0].Image.Pixels);
        Assert.Equal(preview.Pixels, new GlbPetDraft(Assert.Single(library.List())).Preview("idle")[0].Image.Pixels);
        var packPath = Path.Combine(temp.Path, "pet.unfoldpet"); draft.Export(packPath);
        using var pack = CharacterPack.Open(packPath);
        Assert.Equal(preview.Pixels, pack.Character.LoadAnimation("idle")[0].Image.Pixels);
        Assert.Equal(bytes, File.ReadAllBytes(input));
        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(pet.DirectoryPath, "model.glb")));
    }

    [Fact]
    public void ChangingPoseOrResolutionClearsThePreviousOutlineWithoutFrameAllocations()
    {
        var model = GlbModel.Parse(GlbTests.Fixture(morph: true));
        var clip = model.CreateAnimation("Idle", new("model.glb"), "Idle");
        var pixels = new uint[384 * 384];
        clip.RenderFrameInto(0, 384, pixels); var first = pixels.ToArray();
        clip.RenderFrameInto(clip.Count - 1, 384, pixels);
        Assert.NotEqual(first, pixels);
        _ = clip.GetFrame(0, 576);
        for (var i = 0; i < 4; i++) clip.RenderFrameInto(0, 384, pixels);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 12; i++) clip.RenderFrameInto(i % clip.Count, 384, pixels);
        Assert.InRange((GC.GetAllocatedBytesForCurrentThread() - allocated) / 12, 0, 1024);
        clip.RenderFrameInto(0, 384, pixels); Assert.Equal(first, pixels);
    }
}
