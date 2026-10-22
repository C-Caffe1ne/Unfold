using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class PetVideoFormatTests
{
    public static bool HasVideoTool => PetMediaImporter.FindFFmpeg() is not null;

    [Theory(Skip = "Prepare media tools for video conversion.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(PetVideoFormatTests))]
    [InlineData("mov")] [InlineData("m4v")] [InlineData("webm")] [InlineData("mkv")] [InlineData("avi")]
    public async Task CommonVideoContainersImportAndSurvivePackReopening(string extension)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "짧은 영상." + extension.ToUpperInvariant());
        File.Copy(CustomPetDraftTests.Fixture("pet-motion." + extension), path);
        Assert.Contains("*." + extension, PetMediaImporter.FilePatterns);
        var clip = await PetMediaImporter.Import(path, TestContext.Current.CancellationToken); File.Delete(path);
        Assert.False(clip.IsStillImage); Assert.Equal(12, clip.FrameCount);
        Assert.InRange(clip.Width, 1, 192); Assert.InRange(clip.Height, 1, 192);
        Assert.False(clip.LoadFrames()[0].Image.Pixels.SequenceEqual(clip.LoadFrames()[^1].Image.Pixels));
        var draft = new CustomPetDraft(); draft.SetClip("idle", clip);
        var output = Path.Combine(temp.Path, "video.unfoldpet"); draft.Export("영상 펫", output);
        using var pack = CharacterPack.Open(output);
        Assert.Empty(pack.Audit.Errors); Assert.Equal(12, pack.Character.LoadAnimation("idle").Count);
    }

    [Theory(Skip = "Prepare media tools for video conversion.", SkipUnless = nameof(HasVideoTool), SkipType = typeof(PetVideoFormatTests))]
    [InlineData("mov")] [InlineData("m4v")] [InlineData("webm")] [InlineData("mkv")] [InlineData("avi")]
    public async Task LongCorruptAndOversizedVideosCannotBecomePartialClips(string extension)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "long." + extension);
        var fixtureExtension = extension is "mov" or "m4v" ? "mp4" : extension;
        File.Copy(CustomPetDraftTests.Fixture("pet-too-long." + fixtureExtension), path);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import(path, TestContext.Current.CancellationToken));
        Assert.Contains("10초", error.Message);
        File.WriteAllText(path, "not a video");
        await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import(path, TestContext.Current.CancellationToken));
        using (var file = File.Create(path)) file.SetLength(PetMediaImporter.MaxVideoBytes + 1L);
        error = await Assert.ThrowsAsync<InvalidDataException>(() => PetMediaImporter.Import(path, TestContext.Current.CancellationToken));
        Assert.Contains("128 MiB", error.Message);
    }

    [Fact]
    public async Task CancelledImportDoesNotOpenTheSource()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PetMediaImporter.Import("missing.webm", cancellation.Token));
    }
}
