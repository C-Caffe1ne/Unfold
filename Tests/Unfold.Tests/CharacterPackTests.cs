using System.IO.Compression;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class CharacterPackTests
{
    internal static string CreatePack(string root, string version = "1.0.0", string id = "test-pet", uint color = 0xFFF4B860)
    {
        var directory = Path.Combine(root, "source", id); Directory.CreateDirectory(directory);
        var pixels = new uint[32 * 16];
        for (var y = 3; y < 13; y++) for (var x = 3; x < 13; x++) { pixels[y * 32 + x] = color; pixels[y * 32 + x + 16] = color; }
        pixels[8 * 32 + 8] = 0xFF222222;
        File.WriteAllBytes(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(new(32, 16, pixels)));
        var manifest = new CharacterManifest(id, "Pack test companion", 1, new("spritesheet.png", 2, 1, 16, 16),
            new() { ["idle"] = new([0, 1], 8), ["attention"] = new([1, 0], 8, Loop: false),
                ["stretch"] = new([0, 1], 8, Loop: false), ["click"] = new([1, 0], 8, Loop: false), ["celebrate"] = new([0, 1], 8, Loop: false) });
        File.WriteAllBytes(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var path = Path.Combine(root, $"{Guid.NewGuid():N}.unfoldpet"); CharacterPack.Create(directory, version, path); return path;
    }
    private static void Rewrite(string path, Action<Dictionary<string, byte[]>> change)
    {
        var files = new Dictionary<string, byte[]>();
        using (var archive = ZipFile.OpenRead(path)) foreach (var entry in archive.Entries)
        { using var input = entry.Open(); using var buffer = new MemoryStream(); input.CopyTo(buffer); files.Add(entry.FullName, buffer.ToArray()); }
        change(files); File.Delete(path);
        using var output = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in files) { using var stream = output.CreateEntry(name).Open(); stream.Write(bytes); }
    }
    [Fact]
    public void PreviewInstallUpdateAndReinstallRepairPreserveIdentity()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var first = CharacterPack.Open(CreatePack(temp.Path));
        Assert.Empty(library.List()); Assert.Equal("Install", library.InspectInstall(first).Action);
        var saved = library.Install(first, null); Assert.Equal(first.Id, saved.Manifest.Id);
        var secondFile = CreatePack(temp.Path, "1.1.0", color: 0xFF88AACC); using var second = CharacterPack.Open(secondFile);
        var info = library.InspectInstall(second); Assert.Equal("Update", info.Action); Assert.Equal("1.0.0", info.InstalledVersion);
        library.Install(second, info.Revision);
        var sheet = Path.Combine(saved.DirectoryPath, "spritesheet.png"); File.WriteAllText(sheet, "damaged");
        Assert.Empty(library.List()); info = library.InspectInstall(second); Assert.Equal("Reinstall", info.Action);
        library.Install(second, info.Revision);
        Assert.Equal(0xFF88AACCu, Assert.Single(library.List()).LoadAnimation("idle")[0].Image.Pixels[4 * 16 + 4]);
        Assert.Throws<InvalidDataException>(() => library.InspectInstall(first));
        Assert.DoesNotContain(Directory.EnumerateDirectories(library.Root), path => Path.GetFileName(path).StartsWith('.'));
    }
    [Fact]
    public void BuiltInAndExistingArtworkCannotBeOverwritten()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"), ["test-pet"]);
        using var pack = CharacterPack.Open(CreatePack(temp.Path));
        Assert.Throws<InvalidDataException>(() => library.Install(pack, null));
        var own = library.Save(CodecTests.Fixture()); var revision = CharacterLibrary.Revision(own.DirectoryPath);
        using var colliding = CharacterPack.Open(CreatePack(temp.Path, id: own.Manifest.Id));
        Assert.Throws<InvalidDataException>(() => library.InspectInstall(colliding));
        Assert.Equal(revision, CharacterLibrary.Revision(own.DirectoryPath));
    }
    [Fact]
    public void ChangedContentMustUseANewVersion()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var original = CharacterPack.Open(CreatePack(temp.Path)); library.Install(original, null);
        using var changed = CharacterPack.Open(CreatePack(temp.Path, color: 0xFF9988AA));
        Assert.Throws<InvalidDataException>(() => library.InspectInstall(changed));
        Assert.Equal("Reinstall", library.InspectInstall(original).Action);
    }
    [Fact]
    public void PreviewIsASnapshotAndDisposalDoesNotInstallAnything()
    {
        using var temp = new TempDirectory(); var path = CreatePack(temp.Path);
        var pack = CharacterPack.Open(path); var directory = pack.Character.DirectoryPath;
        File.Delete(path); Assert.Equal(2, pack.Character.LoadAnimation("idle").Count);
        pack.Dispose(); Assert.False(Directory.Exists(directory));
    }
    [Fact]
    public void ChangedPreviewCannotReplaceTheInstalledVersion()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var first = CharacterPack.Open(CreatePack(temp.Path)); var saved = library.Install(first, null);
        using var second = CharacterPack.Open(CreatePack(temp.Path, "2.0.0")); var info = library.InspectInstall(second);
        File.WriteAllText(Path.Combine(second.Character.DirectoryPath, "spritesheet.png"), "changed");
        Assert.Throws<InvalidDataException>(() => library.Install(second, info.Revision));
        Assert.Equal("1.0.0", library.InspectInstall(first).InstalledVersion);
        Assert.Equal(first.Character.LoadAnimation("idle")[0].Image.Pixels, CharacterLibrary.LoadPackage(saved.DirectoryPath).LoadAnimation("idle")[0].Image.Pixels);
    }
    [Fact]
    public void ChangesAfterPreviewRequireOpeningThePreviewAgain()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var first = CharacterPack.Open(CreatePack(temp.Path)); var saved = library.Install(first, null);
        using var update = CharacterPack.Open(CreatePack(temp.Path, "2.0.0")); var info = library.InspectInstall(update);
        var manifest = Path.Combine(saved.DirectoryPath, "character.json"); File.AppendAllText(manifest, " ");
        Assert.Throws<IOException>(() => library.Install(update, info.Revision));
        Assert.EndsWith(" ", File.ReadAllText(manifest));
    }
    [Fact]
    public void InterruptedReplacementRestoresOldPackBeforeReinstalling()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        using var pack = CharacterPack.Open(CreatePack(temp.Path)); var saved = library.Install(pack, null);
        Directory.Move(saved.DirectoryPath, Path.Combine(library.Root, ".backup-" + pack.Id));
        var stage = Path.Combine(library.Root, $".stage-{Guid.NewGuid():N}"); Directory.CreateDirectory(stage); File.WriteAllText(Path.Combine(stage, "partial"), "unfinished");
        Assert.Equal(pack.Id, Assert.Single(library.List()).Manifest.Id); Assert.False(Directory.Exists(stage));
        library.Install(pack, library.InspectInstall(pack).Revision); Assert.Single(library.List());
    }
    [Theory]
    [InlineData("../escape.png")]
    [InlineData("/absolute.png")]
    [InlineData("folder\\escape.png")]
    [InlineData("CON.png")]
    [InlineData("spritesheet.PNG")]
    public void UnsafeOrCaseCollidingArchiveNamesAreRejected(string name)
    {
        using var temp = new TempDirectory(); var path = CreatePack(temp.Path); Rewrite(path, files => files.Add(name, [1]));
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
    }
    [Fact]
    public void HashMismatchAndUnlistedFilesAreRejected()
    {
        using var temp = new TempDirectory(); var path = CreatePack(temp.Path); Rewrite(path, files => files["spritesheet.png"] = [1]);
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
        path = CreatePack(temp.Path); Rewrite(path, files => files.Add("unexpected.exe", [1]));
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
    }
    [Fact]
    public void LinkedAndOversizedZipEntriesAreRejectedBeforeExtraction()
    {
        using var temp = new TempDirectory(); var path = CreatePack(temp.Path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update)) archive.GetEntry("spritesheet.png")!.ExternalAttributes = unchecked((int)0xA1FF0000);
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
        path = CreatePack(temp.Path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            archive.GetEntry("spritesheet.png")!.Delete(); using var stream = archive.CreateEntry("spritesheet.png").Open();
            var zeros = new byte[1024 * 1024]; for (var i = 0; i < 33; i++) stream.Write(zeros);
        }
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
    }
    [Fact]
    public void InvalidClipIsRejectedEvenWithMatchingHashes()
    {
        using var temp = new TempDirectory(); var path = CreatePack(temp.Path);
        Rewrite(path, files =>
        {
            var manifest = JsonSerializer.Deserialize<CharacterManifest>(files["character.json"], CharacterLibrary.JsonOptions)!;
            manifest.Animations["stretch"] = new(Gif: "bad.gif", Loop: false);
            files["character.json"] = JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions); files["bad.gif"] = [1, 2, 3];
            var metadata = JsonSerializer.Deserialize<CharacterPackMetadata>(files["pack.json"], CharacterLibrary.JsonOptions)!;
            foreach (var name in new[] { "character.json", "bad.gif" }) metadata.Files[name] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(files[name])).ToLowerInvariant();
            files["pack.json"] = JsonSerializer.SerializeToUtf8Bytes(metadata, CharacterLibrary.JsonOptions);
        });
        Assert.Throws<InvalidDataException>(() => CharacterPack.Open(path));
    }
}
