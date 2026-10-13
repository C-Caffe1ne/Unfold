using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class GlbRetiredHeldTests
{
    private static string LegacyPack(string root)
    {
        var directory = Path.Combine(root, "legacy-held"); Directory.CreateDirectory(directory);
        var bytes = GlbTests.Fixture(morph: true);
        var model = GlbModel.Parse(bytes); var definition = new GlbDefinition("model.glb");
        var preview = model.Render("Idle", 0, definition, "Idle");
        var manifest = new CharacterManifest("legacy-held", "이전 3D 펫", 1,
            new("preview.png", 1, 1, preview.Width, preview.Height),
            new()
            {
                ["idle"] = new(ModelClip: "Idle", Loop: false),
                ["pickup"] = new(ModelClip: "Idle", Loop: true, Speed: .5),
                ["held"] = new(ModelClip: "Idle", Loop: true, Speed: 1.5),
                ["land"] = new(ModelClip: "Idle", Loop: false, Speed: 2),
                ["hover"] = new(ModelClip: "Idle", Loop: false)
            }, RenderStyle: "smooth", Model: definition);
        AtomicFile.Write(Path.Combine(directory, "model.glb"), bytes);
        AtomicFile.Write(Path.Combine(directory, "preview.png"), ImageCodec.EncodePng(preview));
        AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var path = Path.Combine(root, "legacy.unfoldpet"); CharacterPack.Create(directory, "1.0.0", path);
        return path;
    }

    [Fact]
    public void LegacyHeldPacksOpenAndInstallButEditedSavesAndExportsExcludeHeld()
    {
        using var temp = new TempDirectory(); var source = LegacyPack(temp.Path);
        var originalBytes = File.ReadAllBytes(source);
        using var legacy = CharacterPack.Open(source);
        Assert.Contains("held", legacy.Character.Manifest.Animations.Keys);
        Assert.True(legacy.Character.HasPointerArt);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var installed = library.Install(legacy, null);
        Assert.Contains("held", CharacterLibrary.LoadPackage(installed.DirectoryPath).Manifest.Animations.Keys);
        var expected = installed.Manifest.Animations.Where(pair => pair.Key != "held").ToDictionary(pair => pair.Key, pair => pair.Value);
        var draft = new GlbPetDraft(installed);
        Assert.DoesNotContain("held", draft.Mappings.Keys);
        Assert.Throws<InvalidDataException>(() => draft.Set("held", "Idle"));
        var saved = draft.Save(library);
        Assert.True(saved.HasPointerArt); Assert.DoesNotContain("held", saved.Manifest.Animations.Keys);
        Assert.Equal(expected.Count, saved.Manifest.Animations.Count);
        foreach (var (action, mapping) in expected) Assert.Equal(mapping, saved.Manifest.Animations[action]);
        var edited = new GlbPetDraft(Assert.Single(library.List()));
        var output = Path.Combine(temp.Path, "edited.unfoldpet"); edited.Export(output);
        using var exported = CharacterPack.Open(output);
        Assert.DoesNotContain("held", exported.Character.Manifest.Animations.Keys);
        var importedLibrary = new CharacterLibrary(Path.Combine(temp.Path, "imported"));
        var imported = importedLibrary.Install(exported, null);
        Assert.True(imported.HasPointerArt);
        Assert.DoesNotContain("held", new GlbPetDraft(imported).Mappings.Keys);
        foreach (var (action, mapping) in expected) Assert.Equal(mapping, imported.Manifest.Animations[action]);
        Assert.Equal(originalBytes, File.ReadAllBytes(source));
        Assert.Equal(File.ReadAllBytes(Path.Combine(legacy.Character.DirectoryPath, "model.glb")),
            File.ReadAllBytes(Path.Combine(imported.DirectoryPath, "model.glb")));
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("land")]
    public void PartialGlbPointerMappingsCompleteOnlyPickupAndLand(string action)
    {
        using var temp = new TempDirectory(); var input = Path.Combine(temp.Path, "source.glb");
        File.WriteAllBytes(input, GlbTests.Fixture(morph: true));
        var draft = new GlbPetDraft(input); draft.Set(action, "Idle", true, .5);
        var output = Path.Combine(temp.Path, "pet.unfoldpet"); draft.Export(output);
        using var pack = CharacterPack.Open(output);
        Assert.True(pack.Character.HasPointerArt);
        Assert.DoesNotContain("held", pack.Character.Manifest.Animations.Keys);
        Assert.Equal(new[] { "pickup", "land" }, GlbPetDraft.PointerClips);
        Assert.Equal(3, pack.Character.Manifest.Animations.Count);
        Assert.True(pack.Character.Manifest.Animations[action].Loop);
        Assert.Equal(.5, pack.Character.Manifest.Animations[action].Speed);
        var other = action == "pickup" ? "land" : "pickup";
        Assert.False(pack.Character.Manifest.Animations[other].Loop);
        Assert.Equal("Idle", pack.Character.Manifest.Animations[other].ModelClip);
    }

    [Fact]
    public void HeldNamedClipDoesNotAutomaticallyCreateTheRetiredEventAndExportFiltersLegacyMappings()
    {
        using var temp = new TempDirectory(); var input = Path.Combine(temp.Path, "held.glb");
        var bytes = GlbTests.Fixture(morph: true);
        var jsonLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12));
        var json = Encoding.UTF8.GetString(bytes, 20, jsonLength).Replace("\"name\":\"Idle\"", "\"name\":\"Held\"", StringComparison.Ordinal);
        var renamed = Encoding.UTF8.GetBytes(json); Assert.Equal(jsonLength, renamed.Length);
        renamed.CopyTo(bytes, 20); File.WriteAllBytes(input, bytes);
        var draft = new GlbPetDraft(input);
        Assert.Equal("Held", draft.Mappings["idle"].ModelClip);
        Assert.DoesNotContain("held", draft.Mappings.Keys); Assert.DoesNotContain("held", GlbPetDraft.Actions);
        // Public mappings may contain a legacy value even though Set no longer offers this action.
        draft.Mappings["held"] = new(ModelClip: "Held", Loop: true);
        var output = Path.Combine(temp.Path, "pet.unfoldpet"); draft.Export(output);
        using var pack = CharacterPack.Open(output);
        Assert.Single(pack.Character.Manifest.Animations);
        Assert.False(pack.Character.HasPointerArt); Assert.DoesNotContain("held", pack.Character.Manifest.Animations.Keys);
    }

    [AvaloniaFact]
    public async Task InstalledLegacyHeldMappingIsAbsentFromTheGlbEditor()
    {
        using var temp = new TempDirectory(); using var pack = CharacterPack.Open(LegacyPack(temp.Path));
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var installed = library.Install(pack, null);
        var window = new Window { Width = 840, Height = 780 };
        using var page = new GlbPetView(window, library, _ => Task.CompletedTask);
        try
        {
            window.Content = page; window.Show(); Dispatcher.UIThread.RunJobs();
            await page.OpenPackage(installed); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var selector = window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == "GlbPreviewAction");
            Assert.Equal(12, selector.ItemsSource!.Cast<string>().Count());
            Assert.DoesNotContain("held", selector.ItemsSource!.Cast<string>());
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control =>
                control.Name is "GlbClip_held" or "GlbRepeat_held" or "GlbSpeed_held");
        }
        finally { window.Close(); }
    }

    [Fact]
    public void OriginalSpritesStillRequireHeldForPointerArtAndKeepTheirKoreanLabel()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var document = new SpriteFixture(4, 4) { Name = "2D 포인터 펫" }; Array.Fill(document.Pixels, 0xFFFFFFFFu);
        var package = library.InstallFixture(document);
        var manifest = package.Manifest with
        {
            BehaviorProfile = OriginalCompanion.Profile,
            Animations = OriginalCompanion.RequiredClips.Concat(new[] { "pickup", "land" }).ToDictionary(
                action => action, _ => new AnimationDefinition([0, 0], 20), StringComparer.Ordinal)
        };
        var path = Path.Combine(package.DirectoryPath, "character.json");
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        Assert.False(CharacterLibrary.LoadPackage(package.DirectoryPath).HasPointerArt);
        manifest.Animations["held"] = new([0, 0], 20);
        AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        Assert.True(CharacterLibrary.LoadPackage(package.DirectoryPath).HasPointerArt);
        Assert.Contains("held", OriginalCompanion.PointerClips);
        Assert.Equal("드래그 중", CustomPetDraft.ActionName("held"));
    }
}
