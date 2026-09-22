using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class CharacterAssetAuditTests
{
    [Fact]
    public void BundledAssetsAreDecodedAndInventoryIncludesHashes()
    {
        var report = CharacterAssetAudit.Inspect(Path.Combine(AppContext.BaseDirectory, "Assets", "Characters"));
        Assert.True(report.Success, JsonSerializer.Serialize(report));
        Assert.Equal(new[] { "bori-rabbit", "default-cat", "hedgehog", "penguin", "puppy-dog" }, report.Characters.Select(item => item.Id).Order());
        var cat = report.Characters.Single(item => item.Id == "default-cat");
        Assert.Contains(cat.Clips, clip => clip.Key == "stretch" && clip.Frames > 1 && clip.EmptyFrames == 0);
        Assert.All(cat.Files, file => { Assert.True(file.Bytes > 0); Assert.Equal(64, file.Sha256.Length); });
    }
    [Fact]
    public void ExistingButCorruptGifFailsTheAudit()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path);
        var package = library.Save(CodecTests.Fixture());
        var manifest = package.Manifest;
        manifest.Animations["stretch"] = new(Gif: "broken.gif", Loop: false);
        File.WriteAllText(Path.Combine(package.DirectoryPath, "broken.gif"), "not a gif");
        AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var report = CharacterAssetAudit.Inspect(temp.Path);
        Assert.False(report.Success); Assert.Contains(Assert.Single(report.Characters).Errors, error => error.StartsWith("stretch:"));
    }
    [Fact]
    public void SpriteDecodeBudgetRejectsHugeFrameExpansionBeforeAllocation()
    {
        using var temp = new TempDirectory(); var directory = Path.Combine(temp.Path, "large-cat"); Directory.CreateDirectory(directory);
        AtomicFile.Write(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(new(256, 256, new uint[256 * 256])));
        var manifest = new CharacterManifest("large-cat", "Large cat", 1, new("spritesheet.png", 1, 1, 256, 256),
            new() { ["idle"] = new(new int[513], 12) });
        AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var package = CharacterLibrary.LoadPackage(directory);
        Assert.Throws<InvalidDataException>(() => package.LoadAnimation("idle"));
        Assert.False(CharacterAssetAudit.Inspect(temp.Path).Success);
    }
    [Fact]
    public void EmptyDirectoryIsNotAValidRelease()
    {
        using var temp = new TempDirectory(); Assert.False(CharacterAssetAudit.Inspect(temp.Path).Success);
    }
}
