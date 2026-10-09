using System.Text.Json;
using Unfold.Core;

namespace Unfold.Tests;

public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "unfold-tests-" + Guid.NewGuid().ToString("N"));
    public TempDirectory() { Directory.CreateDirectory(Path); }
    public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
}

public class CodecTests
{
    internal static SpriteFixture Fixture() => new(2, 3)
        { Name = "마리 pixel", Pixels = [0xFFFF0000, 0, 0xFF00FF00, 0x80FF0000, 0, 0xFF0000FF] };


    [Fact] public void CorruptLayoutsAndTruncatedPngAreRejected()
    {
        var bytes = ImageCodec.EncodePng(new(2, 2, new uint[4]));
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePng(bytes[..^12]));
        Assert.Throws<InvalidDataException>(() => ImageCodec.DecodePng(bytes, 1, 1));
    }
    [Fact] public void ActualBundledCatAndGifDecode()
    {
        var package = CharacterLibrary.LoadPackage(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "default-cat"), true);
        Assert.Equal(8, package.LoadAnimation("idle").Count);
        var gif = package.LoadAnimation("stretch"); Assert.True(gif.Count > 1);
        Assert.All(gif, f => Assert.True(f.Duration.TotalMilliseconds >= 20));
        Assert.Contains(gif[0].Image.Pixels, p => p >> 24 != 0);
    }
}

public class LibraryTests
{


    [Fact] public void RecoveryRestoresBackupAfterInterruptedDirectorySwap()
    {
        using var temp = new TempDirectory(); var library = new CharacterLibrary(temp.Path); var saved = library.InstallFixture(CodecTests.Fixture());
        Directory.Move(saved.DirectoryPath, System.IO.Path.Combine(temp.Path, ".backup-" + saved.Manifest.Id));
        var stage = System.IO.Path.Combine(temp.Path, ".stage-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage); File.WriteAllText(System.IO.Path.Combine(stage, "partial"), "incomplete");
        var recovered = new CharacterLibrary(temp.Path).List(); Assert.Single(recovered); Assert.True(Directory.Exists(saved.DirectoryPath)); Assert.False(Directory.Exists(stage));
    }
    [Theory] [InlineData("../outside.png")] [InlineData("C:\\outside.png")] [InlineData("/outside.png")] [InlineData("a/../../outside.png")] [InlineData("a\\..\\outside.png")]
    public void RejectsTraversal(string relative)
    { using var temp = new TempDirectory(); Assert.Throws<InvalidDataException>(() => CharacterLibrary.AssetPath(temp.Path, relative)); }
    [Fact] public void AtomicSettingsRoundTrip()
    {
        using var temp = new TempDirectory(); var file = System.IO.Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { IntervalMinutes = 45, PetX = -300, PetY = 100 }; settings.Save(file); Assert.Equal(settings, AppSettings.Load(file));
    }
}

public class ClockTests
{
    [Fact] public void TracksActiveTimeAndFiresOnce()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Tick(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        var fired = 0; for (var i = 1; i <= 305; i++) if (clock.Tick(TimeSpan.FromSeconds(i), TimeSpan.Zero, TimeSpan.FromMinutes(5))) fired++;
        Assert.Equal(1, fired); Assert.Equal(TimeSpan.FromSeconds(295), clock.Remaining);
    }
    [Fact] public void PausedIdleAndSleepGapsDoNotCount()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Start(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(1)); clock.Tick(TimeSpan.FromSeconds(2), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.TogglePause(TimeSpan.FromSeconds(2)); clock.Tick(TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromMinutes(5), clock.Remaining);
        clock.Tick(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromMinutes(5)); Assert.Equal(TimeSpan.FromSeconds(299), clock.Remaining);
    }
    [Fact] public void IdleThresholdCrossingOnlyExcludesExcessTime()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(5)); clock.Start(TimeSpan.Zero);
        clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(300.4), TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.FromSeconds(299.4), clock.Remaining);
    }
}
