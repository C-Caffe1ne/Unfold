using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class PetPackImportTests
{
    [Fact]
    public async Task CancelledImportLeavesExistingArtworkAndSelectionUntouched()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var path = CharacterPackTests.CreatePack(temp.Path); using var pack = CharacterPack.Open(path);
        var saved = library.Install(pack, null); var revision = CharacterLibrary.PetRevision(saved.DirectoryPath);
        var selected = false;
        var result = await PetPackImport.Save(path, library, _ => { selected = true; return Task.CompletedTask; }, (_, _) => Task.FromResult(false));
        Assert.Null(result); Assert.False(selected); Assert.Equal(revision, CharacterLibrary.PetRevision(saved.DirectoryPath));
    }

    [Fact]
    public async Task ImportPreservesInstallUpdateAndRepairWithoutAPreviewWindow()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        CharacterPackage? selected = null;
        async Task<CharacterPackage?> Save(string path, string action) => await PetPackImport.Save(path, library,
            pet => { selected = pet; return Task.CompletedTask; }, (_, target) => { Assert.Equal(action, target.Action); return Task.FromResult(true); });
        var initial = CharacterPackTests.CreatePack(temp.Path, "1.0.0");
        var saved = await Save(initial, "Install"); Assert.Same(saved, selected);
        var update = CharacterPackTests.CreatePack(temp.Path, "2.0.0", color: 0xFFABCDEF);
        var latest = await Save(update, "Update"); Assert.Same(latest, selected);
        var pixels = latest!.StillImage.Pixels.ToArray();
        File.WriteAllText(Path.Combine(latest.DirectoryPath, "spritesheet.png"), "broken");
        var repaired = await Save(update, "Reinstall"); Assert.Equal(pixels, repaired!.StillImage.Pixels);
        Assert.Single(library.List());
    }

    [Fact]
    public async Task AnEditDuringConfirmationCannotBeOverwritten()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var initial = CharacterPackTests.CreatePack(temp.Path); using var pack = CharacterPack.Open(initial);
        var saved = library.Install(pack, null); var selected = false;
        var update = CharacterPackTests.CreatePack(temp.Path, "2.0.0");
        await Assert.ThrowsAsync<IOException>(() => PetPackImport.Save(update, library,
            _ => { selected = true; return Task.CompletedTask; }, (_, _) =>
            { File.AppendAllText(Path.Combine(saved.DirectoryPath, "character.json"), " "); return Task.FromResult(true); }));
        Assert.False(selected); Assert.Equal("1.0.0", library.InspectInstall(pack).InstalledVersion);
    }

    [Fact]
    public async Task InvalidPackCannotRequestConfirmationOrInstall()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var path = Path.Combine(temp.Path, "broken.unfoldpet"); File.WriteAllText(path, "broken");
        var confirmed = false;
        await Assert.ThrowsAsync<InvalidDataException>(() => PetPackImport.Save(path, library, _ => Task.CompletedTask,
            (_, _) => { confirmed = true; return Task.FromResult(true); }));
        Assert.False(confirmed); Assert.Empty(library.List());
    }
}
