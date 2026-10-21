using Unfold.Core;

namespace Unfold.Tests;

public sealed class PetActionFilesDraftTests
{
    private static ImportedPetClip Gif(string name) => ImportedPetClip.FromGif(name, File.ReadAllBytes(CustomPetDraftTests.Fixture()));
    private static ImportedPetClip Still(string name, uint color)
    {
        var pixels = new uint[24 * 24]; for (var i = 0; i < pixels.Length; i++) pixels[i] = i % 24 is > 4 and < 20 ? color : 0;
        return ImportedPetClip.FromImage(name, ImageCodec.EncodePng(new(24, 24, pixels)));
    }

    private static CharacterPackage ExportAndInstall(CustomPetDraft draft, TempDirectory temp, CharacterLibrary library)
    {
        var path = Path.Combine(temp.Path, Guid.NewGuid().ToString("N") + ".unfoldpet");
        draft.Export("여러 파일", path);
        using var pack = CharacterPack.Open(path);
        return library.Install(pack, null);
    }

    [Fact]
    public void ANewPackStoresEveryFileAndTheOrder()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var draft = new CustomPetDraft();
        draft.SetClip("idle", Gif("idle.gif"));
        draft.AddFile("idle", Gif("idle-b.gif")); draft.AddFile("idle", Still("pose.png", 0xFF3366CC));
        draft.SetOrder("idle", "sequence");
        draft.SetClip("click", Still("click.png", 0xFFCC6633)); draft.AddFile("click", Still("click-b.png", 0xFF33CC66));
        Assert.Equal(3, draft.Files("idle").Count);
        var installed = ExportAndInstall(draft, temp, library);
        Assert.Equal(3, installed.VariantCount("idle")); Assert.Equal("sequence", installed.Manifest.Animations["idle"].Order);
        Assert.Equal(2, installed.VariantCount("click")); Assert.Null(installed.Manifest.Animations["click"].Order);
        Assert.Single(installed.LoadAnimation("idle", 2));
        Assert.NotEqual(installed.LoadAnimation("click", 0)[0].Image.Pixels[24 * 12 + 12], installed.LoadAnimation("click", 1)[0].Image.Pixels[24 * 12 + 12]);
    }

    [Fact]
    public void EditingAnInstalledPetKeepsChangesAndUnchangedFiles()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var draft = new CustomPetDraft();
        draft.SetClip("idle", Still("idle.png", 0xFF3366CC)); draft.AddFile("idle", Gif("idle-b.gif")); draft.AddFile("idle", Still("idle-c.png", 0xFF33CC66));
        draft.SetOrder("idle", "sequence");
        var installed = ExportAndInstall(draft, temp, library);

        var editing = new CustomPetDraft(installed);
        Assert.Equal(3, editing.Files("idle").Count); Assert.Equal("sequence", editing.Order("idle"));
        editing.RemoveFile("idle", 1);
        editing.AddFile("idle", Still("idle-d.png", 0xFFCC3366));
        editing.SetOrder("idle", "random");
        var saved = editing.Save(library, "여러 파일");
        Assert.Equal(3, saved.VariantCount("idle")); Assert.Null(saved.Manifest.Animations["idle"].Order);
        Assert.All(Enumerable.Range(0, 3), index => Assert.Single(saved.LoadAnimation("idle", index)));
        Assert.Equal(0xFF33CC66u, saved.LoadAnimation("idle", 1)[0].Image.Pixels[24 * 12 + 12]);
        Assert.Equal(0xFFCC3366u, saved.LoadAnimation("idle", 2)[0].Image.Pixels[24 * 12 + 12]);
    }

    [Fact]
    public void FileCountIsLimitedAndRemovingTheFirstPromotesTheNext()
    {
        var draft = new CustomPetDraft();
        var first = Still("a.png", 0xFF111111); var second = Still("b.png", 0xFF222222);
        draft.SetClip("idle", first); draft.AddFile("idle", second);
        draft.AddFile("idle", Still("c.png", 0xFF333333)); draft.AddFile("idle", Still("d.png", 0xFF444444));
        Assert.Throws<InvalidDataException>(() => draft.AddFile("idle", Still("e.png", 0xFF555555)));
        draft.RemoveFile("idle", 0);
        Assert.Same(second, draft.Clips["idle"]); Assert.Equal(3, draft.Files("idle").Count);
        draft.RemoveClip("idle");
        Assert.Empty(draft.Files("idle"));
    }

    [Fact]
    public void CleanupReachesEveryFileAndANewFirstFileRestoresTheRest()
    {
        var draft = new CustomPetDraft();
        var first = Still("a.png", 0xFF111111); var second = Still("b.png", 0xFF222222);
        var cleanedFirst = Still("a-clean.png", 0xFF111111); var cleanedSecond = Still("b-clean.png", 0xFF222222);
        draft.SetClip("idle", first); draft.AddFile("idle", second);
        var settings = new ClipProcessing(true, 0xFF00B140);
        draft.SetProcessedClip("idle", first, settings, cleanedFirst, 0);
        draft.SetProcessedClip("idle", second, settings, cleanedSecond, 1);
        Assert.Same(second, draft.ProcessingSource("idle", 1)); Assert.Same(cleanedSecond, draft.Files("idle")[1]);
        draft.AddFile("idle", Still("c-clean.png", 0xFF333333), original: Still("c.png", 0xFF333333));
        Assert.Equal("c.png", draft.ProcessingSource("idle", 2)!.FileName);
        draft.SetClip("idle", Still("new.png", 0xFF999999));
        Assert.Equal(ClipProcessing.None, draft.Processing("idle"));
        Assert.Same(second, draft.Files("idle")[1]); Assert.Equal("c.png", draft.Files("idle")[2].FileName);
    }
}
