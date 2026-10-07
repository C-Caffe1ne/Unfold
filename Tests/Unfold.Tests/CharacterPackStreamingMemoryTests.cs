using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Unfold.Core;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class CharacterPackStreamingMemoryTests
{
    [Fact]
    public void LargeWarmPackCreateOpenAndInstallDoNotAllocatePayloadSizedSnapshots()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "large-pet"); Directory.CreateDirectory(directory);
        var modelPath = Path.Combine(directory, "model.glb"); WriteLargeModel(modelPath);
        var model = GlbModel.Load(modelPath);
        _ = model.CreateAnimation("Idle", new("model.glb"), "Idle").GetFrame(2, 192);
        const uint color = 0xFF446688;
        File.WriteAllBytes(Path.Combine(directory, "preview.png"), ImageCodec.EncodePng(new(16, 16, Enumerable.Repeat(color, 16 * 16).ToArray())));
        var manifest = new CharacterManifest("large-pet", "Large streaming fixture", 1, new("preview.png", 1, 1, 16, 16),
            new() { ["idle"] = new(ModelClip: "Idle", Loop: true) }, RenderStyle: "smooth", Model: new("model.glb"));
        File.WriteAllBytes(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var archivePath = Path.Combine(temp.Path, "large.unfoldpet");
        var before = GC.GetAllocatedBytesForCurrentThread();
        CharacterPack.Create(directory, "1.0.0", archivePath);
        var createdBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(new FileInfo(archivePath).Length > 15 * 1024 * 1024);
        Assert.InRange(createdBytes, 0, 8 * 1024 * 1024);

        before = GC.GetAllocatedBytesForCurrentThread();
        using var pack = CharacterPack.Open(archivePath);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 4 * 1024 * 1024);
        File.Delete(archivePath); // The preview remains a private, complete snapshot.
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        before = GC.GetAllocatedBytesForCurrentThread();
        var installed = library.Install(pack, null);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 4 * 1024 * 1024);
        Assert.NotNull(installed.StillImage);
        Assert.Equal(color, installed.StillImage.Pixels[0]);
        File.Delete(Path.Combine(installed.DirectoryPath, "preview.png"));
        Assert.Equal(color, installed.StillImage.Pixels[0]);
        Assert.DoesNotContain(Directory.EnumerateDirectories(library.Root), path => Path.GetFileName(path).StartsWith('.'));
        GC.KeepAlive(model);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void DeclaredEntrySizeMustMatchTheActualDecompressedBytes(int delta)
    {
        using var temp = new TempDirectory(); var path = CharacterPackTests.CreatePack(temp.Path);
        var bytes = File.ReadAllBytes(path); var found = false;
        for (var offset = 0; offset <= bytes.Length - 46; offset++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) != 0x02014B50) continue;
            var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 28));
            if (offset + 46 + nameLength > bytes.Length || Encoding.UTF8.GetString(bytes, offset + 46, nameLength) != "spritesheet.png") continue;
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 24));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset + 24), length + delta); found = true; break;
        }
        Assert.True(found); File.WriteAllBytes(path, bytes);
        var error = Record.Exception(() => CharacterPack.Open(path));
        Assert.NotNull(error);
        Assert.True(error is InvalidDataException or IOException, $"Unexpected size validation exception: {error}");
    }

    [Fact]
    public void OversizedMetadataIsRejectedBeforeADataSnapshotIsAllocated()
    {
        using var temp = new TempDirectory(); var path = CharacterPackTests.CreatePack(temp.Path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            archive.GetEntry("pack.json")!.Delete();
            using var stream = archive.CreateEntry("pack.json").Open(); stream.Write(new byte[64 * 1024 + 1]);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Contains("64 KiB", error.Message);
        Assert.InRange(allocated, 0, 512 * 1024);
    }

    [Fact]
    public void StreamingCreateCannotOverwriteAnExistingOutputAndCleansItsOutputTemporary()
    {
        using var temp = new TempDirectory(); var path = CharacterPackTests.CreatePack(temp.Path);
        var original = File.ReadAllBytes(path);
        Assert.Throws<IOException>(() => CharacterPack.Create(Path.Combine(temp.Path, "source", "test-pet"), "2.0.0", path));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(temp.Path, Path.GetFileName(path) + ".*.tmp"));
    }

    private static void WriteLargeModel(string path)
    {
        const int binaryBytes = 16 * 1024 * 1024;
        var original = GlbTests.Fixture();
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(12));
        var root = JsonNode.Parse(original.AsSpan(20, jsonLength))!.AsObject();
        root["buffers"]![0]!["byteLength"] = binaryBytes;
        var json = Encoding.UTF8.GetBytes(root.ToJsonString()); var paddedJsonLength = (json.Length + 3) / 4 * 4;
        using var output = new BinaryWriter(File.Create(path));
        output.Write(0x46546C67u); output.Write(2u); output.Write(28 + paddedJsonLength + binaryBytes);
        output.Write(paddedJsonLength); output.Write(0x4E4F534Au); output.Write(json);
        for (var index = json.Length; index < paddedJsonLength; index++) output.Write((byte)' ');
        output.Write(binaryBytes); output.Write(0x004E4942u);
        var binary = original.AsSpan(28 + jsonLength); output.Write(binary);
        // Valid unused embedded bytes make both the entry and ZIP large without
        // requiring a large render or retaining a payload-sized fixture array.
        var random = new Random(713); var buffer = new byte[16 * 1024]; var remaining = binaryBytes - binary.Length;
        while (remaining > 0)
        {
            var count = Math.Min(buffer.Length, remaining); random.NextBytes(buffer.AsSpan(0, count));
            output.Write(buffer.AsSpan(0, count)); remaining -= count;
        }
    }
}
