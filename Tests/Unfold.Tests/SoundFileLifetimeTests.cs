using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class SoundFileLifetimeTests
{
    [Fact]
    public async Task PlayingCopyIsProtectedAcrossLibraryInstancesUntilPlaybackActuallyEnds()
    {
        using var temp = new TempDirectory(); var directory = Path.Combine(temp.Path, "Sounds");
        var library = new ReminderSounds(directory); var collector = new ReminderSounds(directory);
        var source = Path.Combine(temp.Path, "original.wav"); File.WriteAllBytes(source, ReminderSounds.Default(ReminderSound.Due));
        var id = library.Import(source); var path = library.Resolve(ReminderSound.Due, id);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var player = new ReminderSoundPlayer(library, (_, _, _) => ended.Task);
        var playing = player.Preview(ReminderSound.Due, new() { ReminderSoundId = id }, TestContext.Current.CancellationToken);
        collector.RemoveUnused([]); Assert.True(File.Exists(path));
        player.Stop(); collector.RemoveUnused([]); Assert.True(File.Exists(path));
        ended.SetResult(); await playing;
        collector.RemoveUnused([]); Assert.False(File.Exists(path)); Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task PlaybackFailureReleasesTheCopyForCleanup()
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(Path.Combine(temp.Path, "Sounds"));
        var source = Path.Combine(temp.Path, "source.wav"); File.WriteAllBytes(source, ReminderSounds.Default(ReminderSound.Due));
        var id = library.Import(source); var path = library.Resolve(ReminderSound.Due, id);
        using var player = new ReminderSoundPlayer(library, (_, _, _) => throw new IOException("Audio unavailable"));
        await Assert.ThrowsAsync<IOException>(() => player.Preview(ReminderSound.Due, new() { ReminderSoundId = id }, TestContext.Current.CancellationToken));
        library.RemoveUnused([]); Assert.False(File.Exists(path)); Assert.True(File.Exists(source));
    }

    [Fact]
    public void CleanupRetainsDefaultsUnknownFilesAndReferencedOrLeasedCopies()
    {
        using var temp = new TempDirectory(); var directory = Path.Combine(temp.Path, "Sounds");
        var library = new ReminderSounds(directory);
        var source = Path.Combine(temp.Path, "source.wav"); File.WriteAllBytes(source, ReminderSounds.Default(ReminderSound.Due));
        var id = library.Import(source); var path = library.Resolve(ReminderSound.Due, id);
        var builtIn = library.Resolve(ReminderSound.Completed, null);
        var unknown = Path.Combine(directory, "manual.wav"); File.WriteAllText(unknown, "keep");
        var sub = Path.Combine(directory, "nested"); Directory.CreateDirectory(sub);
        var nested = Path.Combine(sub, new string('a', 64) + ".wav"); File.Copy(source, nested);
        library.RemoveUnused([id.ToUpperInvariant(), id]); Assert.True(File.Exists(path));
        using var first = library.AcquirePlayback(ReminderSound.Due, id, true, out _);
        using var second = library.AcquirePlayback(ReminderSound.Due, id, true, out _);
        first.Dispose(); library.RemoveUnused([]); Assert.True(File.Exists(path));
        second.Dispose(); library.RemoveUnused([]); Assert.False(File.Exists(path));
        Assert.True(File.Exists(builtIn)); Assert.True(File.Exists(unknown)); Assert.True(File.Exists(nested)); Assert.True(File.Exists(source));
    }

    public static bool CanCreateLinks => !OperatingSystem.IsWindows();

    [Fact(Skip = "Windows symlink creation requires runner privileges.", SkipUnless = nameof(CanCreateLinks))]
    public void CleanupDoesNotFollowLinkedFilesOrDirectories()
    {
        using var temp = new TempDirectory(); var managed = Path.Combine(temp.Path, "Sounds"); Directory.CreateDirectory(managed);
        var source = Path.Combine(temp.Path, "original.wav"); File.WriteAllBytes(source, ReminderSounds.Default(ReminderSound.Due));
        var link = Path.Combine(managed, new string('a', 64) + ".wav");
        File.CreateSymbolicLink(link, source); new ReminderSounds(managed).RemoveUnused([]);
        Assert.True(File.Exists(link)); Assert.True(File.Exists(source));
        var linkedDirectory = Path.Combine(temp.Path, "SoundsLink"); Directory.CreateSymbolicLink(linkedDirectory, managed);
        var copy = Path.Combine(managed, new string('b', 64) + ".wav"); File.Copy(source, copy);
        new ReminderSounds(linkedDirectory).RemoveUnused([]); Assert.True(File.Exists(copy));
    }
}
