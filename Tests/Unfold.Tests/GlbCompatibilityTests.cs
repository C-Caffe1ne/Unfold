using System.Buffers.Binary;
using System.Text.Json.Nodes;
using Unfold.Core;

namespace Unfold.Tests;

public class GlbCompatibilityTests
{
    private sealed record Layer(float Z, string Mode, float R, float G, float B, float A);
    private static byte[] Pack(JsonObject root, byte[] binary)
    {
        var json = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
        var length = (json.Length + 3) / 4 * 4;
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write(0x46546C67u); writer.Write(2u); writer.Write(28 + length + binary.Length);
        writer.Write(length); writer.Write(0x4E4F534Au); writer.Write(json);
        for (var i = json.Length; i < length; i++) writer.Write((byte)' ');
        writer.Write(binary.Length); writer.Write(0x004E4942u); writer.Write(binary);
        return stream.ToArray();
    }
    private static byte[] Rewrite(byte[] bytes, Action<JsonObject, byte[]> edit)
    {
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        var root = JsonNode.Parse(bytes.AsSpan(20, length))!.AsObject();
        var binary = bytes.AsSpan(28 + length).ToArray(); edit(root, binary); return Pack(root, binary);
    }
    private static byte[] Layers(bool quad, params Layer[] layers)
    {
        float[] vertices = quad ? [-.5f, 0, 0, .5f, 0, 0, .5f, 1, 0, -.5f, 0, 0, .5f, 1, 0, -.5f, 1, 0]
            : [-.5f, 0, 0, .5f, 0, 0, 0, 1, 0];
        var binary = new byte[vertices.Length * 4];
        for (var i = 0; i < vertices.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(binary.AsSpan(i * 4), vertices[i]);
        var root = JsonNode.Parse("""
        {"asset":{"version":"2.0"},"buffers":[],"bufferViews":[],"accessors":[],"materials":[],"meshes":[],"nodes":[],"scenes":[{"nodes":[]}],"scene":0}
        """)!.AsObject();
        root["buffers"]!.AsArray().Add(new JsonObject { ["byteLength"] = binary.Length });
        root["bufferViews"]!.AsArray().Add(new JsonObject { ["buffer"] = 0, ["byteLength"] = binary.Length });
        root["accessors"]!.AsArray().Add(new JsonObject { ["bufferView"] = 0, ["componentType"] = 5126, ["count"] = vertices.Length / 3, ["type"] = "VEC3" });
        for (var i = 0; i < layers.Length; i++)
        {
            var l = layers[i];
            root["materials"]!.AsArray().Add(new JsonObject { ["alphaMode"] = l.Mode,
                ["pbrMetallicRoughness"] = new JsonObject { ["baseColorFactor"] = new JsonArray(l.R, l.G, l.B, l.A) } });
            root["meshes"]!.AsArray().Add(new JsonObject { ["primitives"] = new JsonArray(new JsonObject
                { ["attributes"] = new JsonObject { ["POSITION"] = 0 }, ["material"] = i }) });
            root["nodes"]!.AsArray().Add(new JsonObject { ["mesh"] = i, ["translation"] = new JsonArray(0, 0, l.Z) });
            root["scenes"]![0]!["nodes"]!.AsArray().Add(i);
        }
        return Pack(root, binary);
    }
    private static PixelImage Render(byte[] bytes)
    {
        var model = GlbModel.Parse(bytes); var clip = model.Animations[0].Name;
        return model.Render(clip, 0, new("model.glb"), clip);
    }
    private static uint Center(PixelImage image) => image.Pixels[130 * image.Width + 96];

    [Theory]
    [InlineData("BLEND", .5f, 0x7FFF0000u)]
    [InlineData("BLEND", 0f, 0u)]
    [InlineData("OPAQUE", .1f, 0xFFFF0000u)]
    [InlineData("MASK", .1f, 0u)]
    [InlineData("MASK", .9f, 0xFFFF0000u)]
    public void AlphaModesPreserveTransparencyAndExistingCutoutBehavior(string mode, float alpha, uint expected)
        => Assert.Equal(expected, Center(Render(Layers(false, new Layer(0, mode, 1, 0, 0, alpha)))));

    [Fact]
    public void BlendIsSortedAndOccludedByOpaqueGeometryRegardlessOfFileOrder()
    {
        Layer red = new(1, "BLEND", 1, 0, 0, .5f), blue = new(0, "OPAQUE", 0, 0, 1, 1);
        var forward = Render(Layers(false, red, blue)); var reverse = Render(Layers(false, blue, red));
        Assert.Equal(forward.Pixels, reverse.Pixels); Assert.Equal(0xFF7F0080u, Center(forward));
        Assert.Equal(0xFF0000FFu, Center(Render(Layers(false, red with { Z = -1 }, blue))));
    }
    [Fact]
    public void OverlappingBlendSurfacesRetainStraightAlphaAndSortBackToFront()
    {
        Layer red = new(1, "BLEND", 1, 0, 0, .5f), blue = new(0, "BLEND", 0, 0, 1, .5f);
        var image = Render(Layers(false, red, blue));
        Assert.Equal(0xBFAA0055u, Center(image)); Assert.Equal(image.Pixels, Render(Layers(false, blue, red)).Pixels);
    }
    [Fact]
    public void SharedTriangleEdgeDoesNotBlendTwiceAndTransparentTexelsDoNotOcclude()
    {
        var image = Render(Layers(true, new Layer(0, "BLEND", 1, 0, 0, .5f)));
        Assert.Contains(image.Pixels, p => p >> 24 == 127);
        Assert.All(image.Pixels, p => Assert.True(p == 0 || p == 0x7FFF0000u));
        var result = Render(Layers(false, new Layer(1, "BLEND", 1, 0, 0, 0), new(0, "OPAQUE", 0, 0, 1, 1)));
        Assert.Equal(0xFF0000FFu, Center(result));
    }
    [Fact]
    public void RepeatedMeshInstancesCannotExceedTheFrameTriangleBudget()
    {
        var bytes = Rewrite(Layers(false, new Layer(0, "BLEND", 1, 0, 0, .5f)), (root, _) =>
        {
            var primitives = root["meshes"]![0]!["primitives"]!.AsArray();
            var primitive = primitives[0]!.DeepClone();
            for (var i = 1; i < 100; i++) primitives.Add(primitive.DeepClone());
            var nodes = root["nodes"]!.AsArray(); var scene = root["scenes"]![0]!["nodes"]!.AsArray();
            for (var i = 1; i < 1001; i++) { nodes.Add(new JsonObject { ["mesh"] = 0 }); scene.Add(i); }
        });
        var error = Assert.Throws<InvalidDataException>(() => GlbModel.Parse(bytes));
        Assert.Contains("10만", error.Message);
    }
    [Fact]
    public void UnknownAlphaModesRemainRejected()
        => Assert.Throws<InvalidDataException>(() => GlbModel.Parse(Layers(false, new Layer(0, "INVALID", 1, 0, 0, 1))));

    [Fact]
    public void DuplicateClipNamesRemainSelectableAndPersistWithoutCollidingWithAuthoredSuffixes()
    {
        var bytes = Rewrite(GlbTests.Fixture(rootMotion: false, morph: true), (root, _) =>
        {
            var clips = root["animations"]!.AsArray(); var second = clips[0]!.DeepClone();
            second["samplers"]![0]!["interpolation"] = "STEP"; clips.Add(second);
            var third = clips[0]!.DeepClone(); third["name"] = "Idle (2)"; clips.Add(third);
        });
        var model = GlbModel.Parse(bytes);
        Assert.Equal(new[] { "Idle", "Idle (3)", "Idle (2)" }, model.Animations.Select(c => c.Name));
        Assert.NotEqual(model.Render("Idle", .1, new("model.glb"), "Idle").Pixels,
            model.Render("Idle (3)", .1, new("model.glb"), "Idle").Pixels);
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "duplicate.glb"); File.WriteAllBytes(file, bytes);
        var draft = new GlbPetDraft(file); draft.Set("click", "Idle (3)");
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); draft.Save(library);
        var reopened = new GlbPetDraft(Assert.Single(library.List()));
        Assert.Equal("Idle (3)", reopened.Mappings["click"].ModelClip);
        Assert.Equal(model.Animations, reopened.Model.Animations); Assert.Equal(bytes, File.ReadAllBytes(file));
    }
    [Fact]
    public void DuplicateTimeKeysRemainRejectedWithTheFailingAnimationName()
    {
        var bytes = Rewrite(GlbTests.Fixture(), (root, binary) =>
        {
            var input = root["animations"]![0]!["samplers"]![0]!["input"]!.GetValue<int>();
            var accessor = root["accessors"]![input]!;
            var view = root["bufferViews"]![accessor["bufferView"]!.GetValue<int>()]!;
            BinaryPrimitives.WriteSingleLittleEndian(binary.AsSpan(view["byteOffset"]!.GetValue<int>() + 4), 0);
        });
        var error = Assert.Throws<InvalidDataException>(() => GlbModel.Parse(bytes));
        Assert.Contains("Idle", error.Message); Assert.Contains("시간 키", error.Message);
    }
}
