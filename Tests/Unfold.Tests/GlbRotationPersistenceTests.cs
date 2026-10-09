using System.Text.Json.Nodes;
using Unfold.Core;

namespace Unfold.Tests;

public class GlbRotationPersistenceTests
{
    [Fact]
    public void SavedAndExportedPetsRenderExactlyLikeThePreviewForEveryAxis()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "pet.glb"); var bytes = GlbTests.Fixture(morph: true);
        File.WriteAllBytes(source, bytes);
        var draft = new GlbPetDraft(source); draft.Set("click", "Idle", false, 1.25);
        var idle = new GlbRotation(25, -40, 35); var click = new GlbRotation(-30, 65, -25);
        draft.SetRotation("idle", idle); draft.SetRotation("click", click);
        draft.Set("click", "Idle", true, .5); // Other controls retain the chosen orientation.
        Assert.Equal(click, draft.RotationFor("click"));
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var package = draft.Save(library);
        Verify(package);
        var reopened = new GlbPetDraft(package);
        Assert.Equal(idle, reopened.RotationFor("idle")); Assert.Equal(click, reopened.RotationFor("click"));
        var output = Path.Combine(temp.Path, "pet.unfoldpet"); reopened.Export(output);
        using var pack = CharacterPack.Open(output); Verify(pack.Character);
        var installed = new CharacterLibrary(Path.Combine(temp.Path, "imported")).Install(pack, null); Verify(installed);
        Assert.Equal(bytes, File.ReadAllBytes(source));
        void Verify(CharacterPackage pet)
        {
            foreach (var (action, rotation) in new[] { ("idle", idle), ("click", click) })
            {
                Assert.Equal(rotation, pet.Manifest.Animations[action].Rotation);
                var preview = draft.Preview(action); var live = Assert.IsType<GlbAnimationFrames>(pet.LoadAnimation(action));
                var expected = new uint[192 * 192]; var actual = new uint[expected.Length];
                foreach (var index in new[] { 0, preview.Count / 2 })
                {
                    preview.RenderPreviewFrameInto(index, 192, expected, rotation); live.RenderFrameInto(index, 192, actual);
                    Assert.Equal(expected, actual);
                }
                preview.RenderPreviewFrameInto(preview.Count - 1, 192, expected, rotation, terminal: true);
                live.RenderTerminalFrameInto(192, actual); Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void LegacyHeadingsRemainUnchangedUntilAnActionIsRotated()
    {
        using var temp = new TempDirectory(); var source = Path.Combine(temp.Path, "pet.glb");
        File.WriteAllBytes(source, GlbTests.Fixture());
        var draft = new GlbPetDraft(source) { Heading = -35 };
        draft.Set("click", "Idle", heading: 72);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var pet = draft.Save(library);
        var reopened = new GlbPetDraft(pet);
        Assert.Equal(new GlbRotation(0, -35, 0), reopened.RotationFor("idle"));
        Assert.Equal(new GlbRotation(0, 72, 0), reopened.RotationFor("click"));
        foreach (var (action, angle) in new[] { ("idle", -35f), ("click", 72f) })
        {
            var legacy = draft.Model.CreateAnimation("Idle", new("model.glb", angle), "Idle");
            Assert.Equal(legacy[0].Image.Pixels, pet.LoadAnimation(action)[0].Image.Pixels);
            Assert.Null(pet.Manifest.Animations[action].Rotation);
        }
        reopened.SetRotation("click", new(15, 72, 25)); var saved = reopened.Save(library);
        Assert.Null(saved.Manifest.Animations["idle"].Rotation);
        Assert.Equal(new GlbRotation(15, 72, 25), saved.Manifest.Animations["click"].Rotation);
        Assert.Equal(pet.LoadAnimation("idle")[0].Image.Pixels, saved.LoadAnimation("idle")[0].Image.Pixels);
    }

    [Theory]
    [InlineData("x", 181)] [InlineData("y", -181)] [InlineData("z", 10000)]
    public void InvalidRotationIsRejectedEvenWhenLoadingMetadataOnly(string axis, int angle)
    {
        using var temp = new TempDirectory(); var source = Path.Combine(temp.Path, "pet.glb");
        File.WriteAllBytes(source, GlbTests.Fixture());
        var pet = new GlbPetDraft(source).Save(new(Path.Combine(temp.Path, "library")));
        var file = Path.Combine(pet.DirectoryPath, "character.json");
        var json = JsonNode.Parse(File.ReadAllText(file))!;
        json["animations"]!["idle"]!["rotation"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 };
        json["animations"]!["idle"]!["rotation"]![axis] = angle;
        File.WriteAllText(file, json.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CharacterLibrary.LoadPackage(pet.DirectoryPath));
        Assert.Empty(new CharacterLibrary(Path.Combine(temp.Path, "library")).List(loadModels: false));
    }

    [Fact]
    public void RotationPrecedenceAndWrapAreExplicit()
    {
        var model = new GlbDefinition("model.glb", 90, Rotation: new(10, 20, 30));
        Assert.Equal(new GlbRotation(10, 20, 30), model.ForAnimation(new()).EffectiveRotation);
        Assert.Equal(new GlbRotation(0, -90, 0), model.ForAnimation(new(Heading: -90)).EffectiveRotation);
        Assert.Equal(new GlbRotation(5, 6, 7), model.ForAnimation(new(Heading: 180, Rotation: new(5, 6, 7))).EffectiveRotation);
        Assert.Equal(new GlbRotation(30, -15, 180), new GlbRotation(390, 345, 540).Normalize());
        Assert.False(new GlbRotation(float.PositiveInfinity).IsValid);
    }
}
