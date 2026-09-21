using System.Buffers.Binary;
using System.Text;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class SoundVolumeTests
{
    [Fact]
    public void MissingVolumeKeepsExistingSoundLevelAndSavedValuesRoundTrip()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, "{\"intervalMinutes\":37}");
        Assert.Equal(100, AppSettings.Load(path).ReminderVolumePercent);
        foreach (var volume in new[] { 0, 1, 42, 100 })
        {
            var settings = new AppSettings { IntervalMinutes = 37, ReminderVolumePercent = volume };
            settings.Save(path); Assert.Equal(settings, AppSettings.Load(path));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void DamagedVolumeRecoversAloneAndInvalidSavePreservesTheFile(int volume)
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, $"{{\"intervalMinutes\":37,\"reminderVolumePercent\":{volume}}}");
        var settings = AppSettings.Load(path);
        Assert.Equal(100, settings.ReminderVolumePercent); Assert.Equal(37, settings.IntervalMinutes);
        settings.Save(path); var before = File.ReadAllText(path);
        Assert.Throws<InvalidDataException>(() => (settings with { ReminderVolumePercent = volume }).Save(path));
        Assert.Equal(before, File.ReadAllText(path));
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    [InlineData(16, 1)]
    [InlineData(16, 2)]
    public void VolumeScalesPcmSamplesPreservingDurationMetadataAndSource(int bits, int channels)
    {
        var original = Wave(bits, channels); var before = original.ToArray();
        var quiet = ReminderSounds.WithVolume(original, 25);
        var silent = ReminderSounds.WithVolume(original, 0);
        Assert.Equal(original, ReminderSounds.WithVolume(original, 100));
        Assert.Equal(before, original);
        Assert.Equal(ReminderSounds.Duration(original), ReminderSounds.Duration(quiet));
        Assert.Equal(ReminderSounds.Duration(original), ReminderSounds.Duration(silent));
        var sampleBytes = new HashSet<int>();
        foreach (var (offset, length) in DataChunks(original))
        {
            for (var sample = offset; sample < offset + length; sample += bits / 8)
            {
                if (bits == 8)
                {
                    Assert.Equal((byte)(128 + (original[sample] - 128) / 4), quiet[sample]);
                    Assert.Equal(128, silent[sample]); sampleBytes.Add(sample);
                }
                else
                {
                    var expected = (short)(BinaryPrimitives.ReadInt16LittleEndian(original.AsSpan(sample, 2)) / 4);
                    Assert.Equal(expected, BinaryPrimitives.ReadInt16LittleEndian(quiet.AsSpan(sample, 2)));
                    Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(silent.AsSpan(sample, 2)));
                    sampleBytes.Add(sample); sampleBytes.Add(sample + 1);
                }
            }
        }
        Assert.NotEmpty(sampleBytes);
        for (var index = 0; index < original.Length; index++)
            if (!sampleBytes.Contains(index)) { Assert.Equal(original[index], quiet[index]); Assert.Equal(original[index], silent[index]); }
    }

    [Theory]
    [InlineData(ReminderSound.Due, false)]
    [InlineData(ReminderSound.Completed, false)]
    [InlineData(ReminderSound.Due, true)]
    [InlineData(ReminderSound.Completed, true)]
    public async Task PreviewAndNotificationsUseTheSameVolumeForBuiltInAndImportedWaves(ReminderSound sound, bool custom)
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(Path.Combine(temp.Path, "sounds"));
        var original = custom ? Wave(8, 2) : ReminderSounds.Default(sound);
        string? id = null;
        if (custom)
        {
            var source = Path.Combine(temp.Path, "custom.wav"); File.WriteAllBytes(source, original); id = library.Import(source);
        }
        var played = new List<(string Path, byte[] Data, TimeSpan Duration)>();
        using var player = new ReminderSoundPlayer(library, (path, duration, _) =>
        {
            played.Add((path, File.ReadAllBytes(path), duration));
            return Task.CompletedTask;
        });
        var settings = new AppSettings { ReminderVolumePercent = 37, ReminderSoundId = id, CompletionSoundId = id };
        await player.Preview(sound, settings, TestContext.Current.CancellationToken);
        player.Play(sound, settings);
        Assert.Equal(2, played.Count);
        Assert.All(played, item =>
        {
            Assert.False(File.Exists(item.Path));
            Assert.Equal(ReminderSounds.WithVolume(original, 37), item.Data);
            Assert.Equal(ReminderSounds.Duration(original), item.Duration);
        });
        Assert.Equal(original, File.ReadAllBytes(library.Resolve(sound, id)));
    }

    [Fact]
    public async Task ZeroVolumeAndMutedNotificationsDoNotStartPlaybackButPreviewIgnoresTheMuteSwitch()
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(temp.Path); var calls = 0;
        using var player = new ReminderSoundPlayer(library, (_, _, _) => { calls++; return Task.CompletedTask; });
        var silent = new AppSettings { ReminderVolumePercent = 0 };
        await player.Preview(ReminderSound.Due, silent, TestContext.Current.CancellationToken);
        player.Play(ReminderSound.Completed, silent);
        player.Play(ReminderSound.Due, new() { ReminderSoundsEnabled = false });
        Assert.Equal(0, calls); Assert.Empty(Directory.EnumerateFiles(temp.Path));
        await player.Preview(ReminderSound.Due, new() { ReminderSoundsEnabled = false }, TestContext.Current.CancellationToken);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ReplacingAndStoppingPlaybackCleansOnlyItsOwnTemporaryFile()
    {
        using var temp = new TempDirectory(); var paths = new List<string>(); var tokens = new List<CancellationToken>();
        using var player = new ReminderSoundPlayer(new(temp.Path), (path, _, token) =>
        {
            paths.Add(path); tokens.Add(token);
            return Task.Delay(Timeout.Infinite, token);
        });
        var settings = new AppSettings { ReminderVolumePercent = 50 };
        var first = player.Preview(ReminderSound.Due, settings, TestContext.Current.CancellationToken);
        Assert.True(File.Exists(paths[0]));
        var second = player.Preview(ReminderSound.Completed, settings, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.NotEqual(paths[0], paths[1]); Assert.False(File.Exists(paths[0])); Assert.True(File.Exists(paths[1]));
        Assert.True(tokens[0].IsCancellationRequested); Assert.False(tokens[1].IsCancellationRequested);
        player.Stop(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        Assert.False(File.Exists(paths[1]));
        Assert.True(File.Exists(Path.Combine(temp.Path, "default-due.wav")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "default-completed.wav")));
    }

    [Fact]
    public async Task CancellationDisposalAndPlaybackFailureReleaseTemporaryWaves()
    {
        using var temp = new TempDirectory(); string? path = null;
        using var player = new ReminderSoundPlayer(new(temp.Path), (file, _, token) =>
        { path = file; return Task.Delay(Timeout.Infinite, token); });
        using var cancellation = new CancellationTokenSource();
        var preview = player.Preview(ReminderSound.Due, new() { ReminderVolumePercent = 50 }, cancellation.Token);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview);
        Assert.False(File.Exists(path));
        preview = player.Preview(ReminderSound.Due, new() { ReminderVolumePercent = 50 }, TestContext.Current.CancellationToken);
        player.Dispose(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => preview);
        Assert.False(File.Exists(path));
        using var failing = new ReminderSoundPlayer(new(temp.Path), (file, _, _) =>
        { path = file; throw new IOException("Unavailable audio device"); });
        await Assert.ThrowsAsync<IOException>(() => failing.Preview(ReminderSound.Due, new() { ReminderVolumePercent = 50 }, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path));
    }

    private static IEnumerable<(int Offset, int Length)> DataChunks(byte[] data)
    {
        for (var offset = 12; offset + 8 <= data.Length;)
        {
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4, 4));
            if (data.AsSpan(offset, 4).SequenceEqual("data"u8)) yield return (offset + 8, length);
            offset += 8 + length + (length & 1);
        }
    }

    private static byte[] Wave(int bits, int channels)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write("RIFF"u8); writer.Write(0); writer.Write("WAVE"u8);
        writer.Write("JUNK"u8); writer.Write(3); writer.Write(new byte[] { 9, 8, 7, 0 });
        // Deliberately put data before fmt and split it into two chunks, as supported by the importer.
        var amplitudes = bits == 8 ? new[] { 0, 64, 128, 192, 255, 128 } : new[] { -32768, -16384, 0, 16384, 32767, 0 };
        foreach (var samples in new[] { amplitudes[..2], amplitudes[2..] })
        {
            writer.Write("data"u8); writer.Write(samples.Length * bits / 8);
            foreach (var value in samples) { if (bits == 8) writer.Write((byte)value); else writer.Write((short)value); }
        }
        const int rate = 8000; var block = channels * bits / 8;
        writer.Write("fmt "u8); writer.Write(16); writer.Write((ushort)1); writer.Write((ushort)channels);
        writer.Write(rate); writer.Write(rate * block); writer.Write((ushort)block); writer.Write((ushort)bits);
        var result = stream.ToArray(); BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4, 4), result.Length - 8);
        return result;
    }
}
