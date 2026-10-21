using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public sealed class PetActionVariantsTests
{
    private static (CharacterLibrary Library, CharacterPackage Package) Install(TempDirectory temp, Func<CharacterManifest, CharacterManifest> edit)
    {
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var fixture = new SpriteFixture(8, 8) { Name = "Variants" };
        for (var i = 0; i < fixture.Pixels.Length; i++) fixture.Pixels[i] = 0xFFFFFFFF;
        var package = library.InstallFixture(fixture);
        File.Copy(CustomPetDraftTests.Fixture(), Path.Combine(package.DirectoryPath, "idle-2.gif"));
        var manifest = edit(package.Manifest);
        AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        return (library, CharacterLibrary.LoadPackage(package.DirectoryPath));
    }

    private static CharacterManifest WithIdleVariants(CharacterManifest manifest, AnimationDefinition[]? variants, string? order = null)
    {
        var animations = new Dictionary<string, AnimationDefinition>(manifest.Animations, StringComparer.Ordinal);
        animations["idle"] = animations["idle"] with { Variants = variants, Order = order };
        return manifest with { Animations = animations };
    }

    [Fact]
    public void EachFileOfAnActionLoadsByItsIndex()
    {
        using var temp = new TempDirectory();
        var (_, package) = Install(temp, manifest => WithIdleVariants(manifest,
            [new(Gif: "idle-2.gif"), new(Frames: [0], Fps: 1)], "sequence"));
        Assert.Equal(3, package.VariantCount("idle"));
        Assert.Equal(1, package.VariantCount("attention"));
        Assert.Equal("sequence", package.Manifest.Animations["idle"].Order);
        var gif = package.LoadAnimation("idle", 1);
        Assert.Equal(ImageCodec.DecodeGif(File.ReadAllBytes(CustomPetDraftTests.Fixture())).Count, gif.Count);
        Assert.Single(package.LoadAnimation("idle", 2));
        Assert.Equal(package.LoadAnimation("idle").Count, package.LoadAnimation("idle", 0).Count);
    }

    [Theory]
    [InlineData("nested")] [InlineData("too-many")] [InlineData("order")] [InlineData("missing")] [InlineData("model")]
    public void InvalidFileListsAreRejected(string kind)
    {
        using var temp = new TempDirectory();
        Assert.ThrowsAny<Exception>(() => Install(temp, manifest => kind switch
        {
            "nested" => WithIdleVariants(manifest, [new(Gif: "idle-2.gif", Variants: [new(Gif: "idle-2.gif")])]),
            "too-many" => WithIdleVariants(manifest, Enumerable.Repeat(new AnimationDefinition(Gif: "idle-2.gif"), 4).ToArray()),
            "order" => WithIdleVariants(manifest, [new(Gif: "idle-2.gif")], "shuffle"),
            "missing" => WithIdleVariants(manifest, [new(Gif: "missing.gif")]),
            _ => WithIdleVariants(manifest, [new(Gif: "idle-2.gif", ModelClip: "Idle")])
        }));
    }

    [Fact]
    public void PacksCarryEveryFileOfAnActionAndOlderFieldsStayOptional()
    {
        using var temp = new TempDirectory();
        var (_, package) = Install(temp, manifest => WithIdleVariants(manifest, [new(Gif: "idle-2.gif")]));
        var archive = Path.Combine(temp.Path, "variants.unfoldpet");
        CharacterPack.Create(package.DirectoryPath, "1.0.0", archive);
        using var pack = CharacterPack.Open(archive);
        var installed = new CharacterLibrary(Path.Combine(temp.Path, "other")).Install(pack, null);
        Assert.Equal(2, installed.VariantCount("idle"));
        Assert.True(File.Exists(Path.Combine(installed.DirectoryPath, "idle-2.gif")));
        Assert.Equal(1, installed.VariantCount("attention")); // Actions without the new fields keep one file.
    }

    [Fact]
    public void RandomOrderNeverRepeatsTheLastFileAndSequenceCycles()
    {
        var picker = new ClipVariantPicker(new Random(7));
        var previous = -1;
        for (var i = 0; i < 200; i++)
        {
            var next = picker.Next("idle", 3, null);
            Assert.InRange(next, 0, 2); Assert.NotEqual(previous, next); previous = next;
        }
        Assert.Equal([0, 1, 2, 0], Enumerable.Range(0, 4).Select(_ => picker.Next("stretch", 3, "sequence")));
        Assert.Equal(0, picker.Next("click", 1, null));
    }
}
