using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetPlaybackSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryGlbActionPreservesPlaybackThroughSaveExportInstallAndReopen(bool loop)
    {
        using var temp = new TempDirectory();
        var input = Path.Combine(temp.Path, "source.glb"); var bytes = GlbTests.Fixture(morph: true);
        File.WriteAllBytes(input, bytes);
        var draft = new GlbPetDraft(input) { Name = "재생 설정" };
        Assert.Equal(13, GlbPetDraft.Actions.Count);
        foreach (var action in GlbPetDraft.Actions) draft.Set(action, "Idle", loop, .5);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var saved = draft.Save(library);
        AssertGlbPlayback(saved, loop);
        var reopened = new GlbPetDraft(Assert.Single(library.List()));
        Assert.All(GlbPetDraft.Actions, action => Assert.Equal(loop, reopened.Mappings[action].Loop));
        var output = Path.Combine(temp.Path, "pet.unfoldpet"); reopened.Export(output);
        using var pack = CharacterPack.Open(output);
        AssertGlbPlayback(pack.Character, loop);
        var importedLibrary = new CharacterLibrary(Path.Combine(temp.Path, "imported"));
        AssertGlbPlayback(importedLibrary.Install(pack, null), loop);
        var imported = new GlbPetDraft(Assert.Single(importedLibrary.List()));
        Assert.All(GlbPetDraft.Actions, action => Assert.Equal(loop, imported.Mappings[action].Loop));
        Assert.Equal(bytes, File.ReadAllBytes(input));
    }

    private static void AssertGlbPlayback(CharacterPackage package, bool loop)
    {
        Assert.Equal(13, package.Manifest.Animations.Count);
        Assert.All(GlbPetDraft.Actions, action =>
        {
            var mapping = package.Manifest.Animations[action];
            Assert.Equal(loop, mapping.Loop); Assert.Equal("Idle", mapping.ModelClip); Assert.Equal(.5, mapping.Speed);
        });
        Assert.Empty(CharacterAssetAudit.InspectPackage(package.DirectoryPath).Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OriginalSpriteActionsPreservePlaybackThroughSaveExportInstallAndReopen(int mode)
    {
        using var temp = new TempDirectory();
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var document = new SpriteFixture(4, 4) { Name = "원본 행동 펫" };
        Array.Fill(document.Pixels, 0xFFFFFFFFu);
        var original = library.InstallFixture(document);
        var actions = OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips).ToArray();
        var manifest = original.Manifest with
        {
            BehaviorProfile = OriginalCompanion.Profile,
            Animations = actions.ToDictionary(action => action, action => new AnimationDefinition([0, 0], 20,
                Loop: action is "idle" or "walk" or "held"), StringComparer.Ordinal)
        };
        AtomicFile.Write(Path.Combine(original.DirectoryPath, "character.json"),
            JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        original = CharacterLibrary.LoadPackage(original.DirectoryPath);
        var source = File.ReadAllBytes(CharacterLibrary.AssetPath(original.DirectoryPath, manifest.SpriteSheet.File));
        var draft = new CustomPetDraft(original);
        foreach (var action in actions) draft.SetPlayback(action, mode);
        var saved = draft.Save(library, "원본 행동 펫");
        AssertSpritePlayback(saved, actions, mode);
        var reopened = new CustomPetDraft(Assert.Single(library.List()));
        Assert.All(actions, action => Assert.Equal(mode, reopened.Playback(action)));
        var output = Path.Combine(temp.Path, "sprite.unfoldpet"); reopened.Export("원본 행동 펫", output);
        using var pack = CharacterPack.Open(output);
        AssertSpritePlayback(pack.Character, actions, mode);
        var importedLibrary = new CharacterLibrary(Path.Combine(temp.Path, "imported"));
        var imported = importedLibrary.Install(pack, null);
        AssertSpritePlayback(imported, actions, mode);
        var importedDraft = new CustomPetDraft(Assert.Single(importedLibrary.List()));
        Assert.All(actions, action => Assert.Equal(mode, importedDraft.Playback(action)));
        Assert.Equal(source, File.ReadAllBytes(CharacterLibrary.AssetPath(imported.DirectoryPath, manifest.SpriteSheet.File)));
    }

    private static void AssertSpritePlayback(CharacterPackage package, string[] actions, int mode)
    {
        Assert.Equal(OriginalCompanion.Profile, package.Manifest.BehaviorProfile);
        Assert.True(package.HasOriginalBehavior); Assert.True(package.HasPointerArt);
        Assert.Equal(actions.Length, package.Manifest.Animations.Count);
        Assert.All(actions, action =>
        {
            var mapping = package.Manifest.Animations[action];
            Assert.Equal(mode != 0, mapping.Loop); Assert.Equal(mode == 2, mapping.PingPong);
            Assert.Equal(new[] { 0, 0 }, mapping.Frames); Assert.Equal(20, mapping.Fps);
        });
        Assert.Empty(CharacterAssetAudit.InspectPackage(package.DirectoryPath).Errors);
    }

    [Theory]
    [InlineData("missing-idle")]
    [InlineData("unknown-event")]
    [InlineData("missing-clip")]
    [InlineData("invalid-speed")]
    public void RelaxedGlbPlaybackKeepsEventAndMappingValidation(string defect)
    {
        using var temp = new TempDirectory(); var input = Path.Combine(temp.Path, "source.glb");
        File.WriteAllBytes(input, GlbTests.Fixture(morph: true));
        var draft = new GlbPetDraft(input); draft.Set("idle", "Idle", false);
        var package = draft.Save(new CharacterLibrary(Path.Combine(temp.Path, "library")));
        var manifest = package.Manifest with { Animations = new(package.Manifest.Animations) };
        switch (defect)
        {
            case "missing-idle": manifest.Animations.Remove("idle"); break;
            case "unknown-event": manifest.Animations["unsupported"] = manifest.Animations["idle"]; break;
            case "missing-clip": manifest.Animations["idle"] = manifest.Animations["idle"] with { ModelClip = "Missing" }; break;
            case "invalid-speed": manifest.Animations["idle"] = manifest.Animations["idle"] with { Speed = .2 }; break;
        }
        AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"),
            JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        Assert.Throws<InvalidDataException>(() => CharacterLibrary.LoadPackage(package.DirectoryPath));
        Assert.NotEmpty(CharacterAssetAudit.InspectPackage(package.DirectoryPath).Errors);
    }
}
