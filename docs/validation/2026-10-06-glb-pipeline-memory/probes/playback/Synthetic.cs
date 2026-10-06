using System.Buffers.Binary;
using System.Text.Json;
static class Synthetic {
    public static byte[] Fixture(bool rootMotion = true, bool morph = false, string interpolation = "LINEAR")
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        var views = new List<object>(); var accessors = new List<object>();
        int Data(float[] data, string type, int count)
        {
            var offset = (int)buffer.Position; foreach (var value in data) writer.Write(value);
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = data.Length * 4 });
            accessors.Add(new { bufferView = views.Count - 1, componentType = 5126, count, type }); return accessors.Count - 1;
        }
        var position = Data([-.4f, 0, 0, .4f, 0, 0, 0, 1, 0], "VEC3", 3);
        // JOINTS uses unsigned bytes, unlike all other synthetic accessors.
        var jo = (int)buffer.Position; writer.Write(new byte[12]); views.Add(new { buffer = 0, byteOffset = jo, byteLength = 12 });
        accessors.Add(new { bufferView = views.Count - 1, componentType = 5121, count = 3, type = "VEC4" }); var joints = accessors.Count - 1;
        var weights = Data([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], "VEC4", 3);
        var time = Data([0, .2f], "SCALAR", 2);
        var translation = Data([0, 0, 0, 1, 0, 0], "VEC3", 2);
        var rotation = Data([0, 0, 0, 1, 0, 1, 0, 0], "VEC4", 2);
        var delta = Data([0, 0, 0, 0, 0, 0, .2f, 0, 0], "VEC3", 3);
        var morphWeights = Data([0, 1], "SCALAR", 2);
        var primitive = new Dictionary<string, object> { ["attributes"] = new { POSITION = position, JOINTS_0 = joints, WEIGHTS_0 = weights } };
        if (morph) primitive["targets"] = new[] { new { POSITION = delta } };
        var channels = new List<object>(); var samplers = new List<object>();
        void Channel(int node, string path, int output)
        { channels.Add(new { sampler = samplers.Count, target = new { node, path } }); samplers.Add(new { input = time, output, interpolation }); }
        if (rootMotion) { Channel(0, "translation", translation); Channel(0, "rotation", rotation); }
        if (morph) Channel(1, "weights", morphWeights);
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)buffer.Length } }, bufferViews = views, accessors,
            nodes = new object[] { new { name = "Root", children = new[] { 1 } }, new { name = "Body", mesh = 0, skin = 0 } },
            skins = new[] { new { joints = new[] { 0 } } }, meshes = new[] { new { primitives = new[] { primitive } } },
            scenes = new[] { new { nodes = new[] { 0 } } }, scene = 0,
            animations = new[] { new { name = "Idle", samplers, channels } }
        });
        var jsonLength = (json.Length + 3) / 4 * 4; var binary = buffer.ToArray(); var result = new byte[12 + 8 + jsonLength + 8 + binary.Length];
        using var output = new BinaryWriter(new MemoryStream(result)); output.Write(0x46546C67u); output.Write(2u); output.Write((uint)result.Length);
        output.Write((uint)jsonLength); output.Write(0x4E4F534Au); output.Write(json); for (var i = json.Length; i < jsonLength; i++) output.Write((byte)' ');
        output.Write((uint)binary.Length); output.Write(0x004E4942u); output.Write(binary); return result;
    }
    public static byte[] DenseFixture(int samples = 12000, string interpolation = "LINEAR")
    {
        using var buffer = new MemoryStream(); using var writer = new BinaryWriter(buffer);
        var views = new List<object>(); var accessors = new List<object>();
        int Data(float[] data, string type, int count)
        {
            var offset = (int)buffer.Position; foreach (var value in data) writer.Write(value);
            views.Add(new { buffer = 0, byteOffset = offset, byteLength = data.Length * 4 });
            accessors.Add(new { bufferView = views.Count - 1, componentType = 5126, count, type }); return accessors.Count - 1;
        }
        var positions = Data([-.4f, 0, 0, .4f, 0, 0, 0, 1, 0], "VEC3", 3);
        var time = Data(Enumerable.Range(0, samples).Select(i => i * 2f / (samples - 1)).ToArray(), "SCALAR", samples);
        var channels = new List<object>(); var samplers = new List<object>();
        for (var node = 0; node < 8; node++)
        {
            var cubic = interpolation == "CUBICSPLINE"; var values = new float[samples * 3 * (cubic ? 3 : 1)];
            for (var i = 0; i < samples; i++) values[(i * (cubic ? 3 : 1) + (cubic ? 1 : 0)) * 3] = MathF.Sin(i * .001f) * .1f;
            var output = Data(values, "VEC3", values.Length / 3);
            channels.Add(new { sampler = node, target = new { node, path = "translation" } });
            samplers.Add(new { input = time, output, interpolation });
        }
        var root = new
        {
            asset = new { version = "2.0" }, buffers = new[] { new { byteLength = (int)buffer.Length } }, bufferViews = views, accessors,
            nodes = Enumerable.Range(0, 8).Select(i => new { name = "Part" + i, mesh = 0 }).ToArray(),
            meshes = new[] { new { primitives = new[] { new { attributes = new { POSITION = positions } } } } },
            scenes = new[] { new { nodes = Enumerable.Range(0, 8).ToArray() } }, scene = 0,
            animations = new[] { new { name = "Motion", channels, samplers } }
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(root); var jsonSize = (json.Length + 3) / 4 * 4;
        using var outputStream = new MemoryStream(); using var outputWriter = new BinaryWriter(outputStream);
        outputWriter.Write(0x46546C67u); outputWriter.Write(2u); outputWriter.Write(28 + jsonSize + (int)buffer.Length);
        outputWriter.Write(jsonSize); outputWriter.Write(0x4E4F534Au); outputWriter.Write(json);
        for (var i = json.Length; i < jsonSize; i++) outputWriter.Write((byte)' ');
        outputWriter.Write((int)buffer.Length); outputWriter.Write(0x004E4942u); outputWriter.Write(buffer.ToArray());
        return outputStream.ToArray();
    }


}