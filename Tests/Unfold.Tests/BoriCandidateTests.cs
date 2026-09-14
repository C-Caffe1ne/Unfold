using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public class BoriCandidateTests
{
    private static string Root => Path.Combine(AppContext.BaseDirectory, "Candidates");
    private static string Runtime => Path.Combine(Root, "bori-rabbit");
    [Fact]
    public void CandidateSuppliesAllFiveClipsWithinProductionMemoryAndDurationBudgets()
    {
        var audit = CharacterAssetAudit.InspectPackage(Runtime);
        Assert.Empty(audit.Errors); Assert.Empty(audit.Warnings);
        Assert.Equal(new[] { "attention", "celebrate", "click", "idle", "stretch" }, audit.Clips.Select(clip => clip.Key));
        Assert.True(audit.Clips.Sum(clip => clip.DecodedBytes) <= 64 * 1024 * 1024);
        Assert.All(audit.Clips, clip =>
        {
            Assert.InRange(clip.DurationMs, 500, 10000); Assert.Equal(256, clip.Width); Assert.Equal(256, clip.Height);
            Assert.Equal(clip.Frames, clip.TransparentFrames); Assert.Equal(0, clip.EmptyFrames);
        });
    }
    [Fact]
    public void SourceIsPreservedAndMatchesTheProvenanceLedger()
    {
        var original = File.ReadAllBytes(Path.Combine(Root, "bori-source.png"));
        Assert.Equal(original, File.ReadAllBytes(Path.Combine(Runtime, "spritesheet.png")));
        using var ledger = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "bori-resource.json")));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant(), ledger.RootElement.GetProperty("sourceSha256").GetString());
        Assert.Equal("art-candidate", ledger.RootElement.GetProperty("releaseStatus").GetString());
        Assert.Equal("commercial-review-pending", ledger.RootElement.GetProperty("rightsStatus").GetString());
    }
    [Fact]
    public void AllSourceCellsHaveTransparentMarginsAndAStableVisibleBaseline()
    {
        var sheet = CharacterLibrary.LoadPackage(Runtime).Sheet; var baselines = new List<int>();
        for (var frame = 0; frame < 24; frame++)
        {
            var left = 256; var top = 256; var right = -1; var bottom = -1; var clear = 0; var nearOpaque = 0;
            for (var y = 0; y < 256; y++) for (var x = 0; x < 256; x++)
            {
                var alpha = sheet.Pixels[(frame / 4 * 256 + y) * sheet.Width + frame % 4 * 256 + x] >> 24;
                if (alpha == 0) clear++; if (alpha >= 240) nearOpaque++;
                if (x is 0 or 255 || y is 0 or 255) Assert.InRange(alpha, 0u, 1u);
                if (alpha < 26) continue;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            Assert.InRange(clear, 256 * 256 / 2, 256 * 256 * 9 / 10); Assert.True(nearOpaque > 256 * 256 / 10);
            Assert.InRange(left, 12, 200); Assert.InRange(top, 12, 200);
            Assert.InRange(right, left + 50, 243); Assert.InRange(bottom, top + 50, 243); baselines.Add(bottom);
        }
        Assert.InRange(baselines.Max() - baselines.Min(), 0, 2);
    }
    [Fact]
    public void ReactionsHaveDistinctPosesAndReturnToTheExactIdleImage()
    {
        var package = CharacterLibrary.LoadPackage(Runtime); var idle = package.LoadAnimation("idle");
        Assert.Equal(idle[0].Image.Pixels, idle[^1].Image.Pixels);
        foreach (var (key, definition) in package.Manifest.Animations.Where(pair => pair.Key != "idle"))
        {
            Assert.False(definition.Loop); var frames = package.LoadAnimation(key);
            Assert.Equal(idle[0].Image.Pixels, frames[0].Image.Pixels); Assert.Equal(idle[0].Image.Pixels, frames[^1].Image.Pixels);
            Assert.Contains(frames, frame => !frame.Image.Pixels.SequenceEqual(idle[0].Image.Pixels));
        }
    }
    [Fact]
    public void ExportedCandidateInstallsAndReopensWithoutShippingAuthoringFiles()
    {
        using var temp = new TempDirectory(); var archive = Path.Combine(temp.Path, "Bori.unfoldpet");
        CharacterPack.Create(Runtime, "0.1.0", archive);
        using var pack = CharacterPack.Open(archive); var library = new CharacterLibrary(Path.Combine(temp.Path, "library"), ["default-cat"]);
        var installed = library.Install(pack, null); var reopened = Assert.Single(new CharacterLibrary(library.Root).List());
        Assert.Equal("bori-rabbit", reopened.Manifest.Id); Assert.Equal("Bori · 보리", reopened.Manifest.Name);
        Assert.Equal("Reinstall", library.InspectInstall(pack).Action);
        Assert.Equal(new[] { "character.json", "pack.json", "spritesheet.png" }, Directory.GetFiles(installed.DirectoryPath).Select(Path.GetFileName).Order());
        Assert.Equal(pack.Character.LoadAnimation("stretch")[4].Image.Pixels, reopened.LoadAnimation("stretch")[4].Image.Pixels);
    }
}
