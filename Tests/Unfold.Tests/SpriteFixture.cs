using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

// Test artwork uses the supported PNG/manifest contract, with no source editor or source codec.
internal sealed class SpriteFixture(int width = 4, int height = 4)
{
    public int Width { get; } = width;
    public int Height { get; } = height;
    public uint[] Pixels { get; set; } = new uint[checked(width * height)];
    public string Name { get; set; } = "Fixture";
}

internal static class SpriteFixtureLibrary
{
    public static CharacterPackage InstallFixture(this CharacterLibrary library, SpriteFixture artwork)
    {
        var id = "user-" + Guid.NewGuid().ToString("N");
        var directory = library.PackagePath(id); Directory.CreateDirectory(directory);
        var manifest = new CharacterManifest(id, artwork.Name, 1, new("spritesheet.png", 1, 1, artwork.Width, artwork.Height),
            new() { ["idle"] = new([0], 12) }, "pawprint.fill", "pixel");
        File.WriteAllBytes(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(new(artwork.Width, artwork.Height, artwork.Pixels)));
        File.WriteAllBytes(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        return CharacterLibrary.LoadPackage(directory);
    }
}
