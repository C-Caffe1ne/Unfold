using Unfold.Core;

namespace Unfold.Desktop;

public sealed class ReminderSoundPlayer : IDisposable
{
    private readonly object gate = new();
    private readonly ReminderSounds sounds;
    private readonly Func<string, TimeSpan, CancellationToken, Task> playback;
    private CancellationTokenSource? current;
    private bool disposed;
    public ReminderSoundPlayer() : this(new(Path.Combine(AppPaths.DataRoot, "Sounds")), PlayPlatform) { }
    internal ReminderSoundPlayer(ReminderSounds sounds) : this(sounds, PlayPlatform) { }
    internal ReminderSoundPlayer(ReminderSounds sounds, Func<string, TimeSpan, CancellationToken, Task> playback)
    { this.sounds = sounds; this.playback = playback; }

    public void Play(ReminderSound sound, AppSettings settings)
    {
        if (!settings.ReminderSoundsEnabled) return;
        _ = PlayNotification(sound, settings);
    }

    private async Task PlayNotification(ReminderSound sound, AppSettings settings)
    {
        try { await Start(sound, settings, strict: false, CancellationToken.None).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        catch (Exception error) { AppPaths.Log(error); }
    }

    public void Stop()
    {
        lock (gate) { current?.Cancel(); current = null; }
    }

    /// <summary>Explicit preview ignores the notification mute switch and reports playback failures.</summary>
    public Task Preview(ReminderSound sound, AppSettings settings, CancellationToken cancellationToken) =>
        Start(sound, settings, strict: true, cancellationToken);

    private Task Start(ReminderSound sound, AppSettings settings, bool strict, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            current?.Cancel();
            var request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            current = request;
            return Run(request, sound, settings, strict);
        }
    }

    private async Task Run(CancellationTokenSource request, ReminderSound sound, AppSettings settings, bool strict)
    {
        string? temporaryPath = null;
        try
        {
            request.Token.ThrowIfCancellationRequested();
            var individual = sound == ReminderSound.Due ? settings.ReminderSoundVolumePercent : settings.CompletionSoundVolumePercent;
            var volume = Math.Clamp(settings.ReminderVolumePercent, 0, 100) * Math.Clamp(individual, 0, 100) / 100d;
            if (volume == 0) return;
            var id = sound == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId;
            using var lease = sounds.AcquirePlayback(sound, id, strict, out var path);
            var data = ImageCodec.ReadBounded(path, 5 * 1024 * 1024);
            var duration = ReminderSounds.Duration(data);
            if (volume < 100)
            {
                temporaryPath = Path.Combine(Path.GetTempPath(), "Unfold-sound-" + Guid.NewGuid().ToString("N") + ".wav");
                File.WriteAllBytes(temporaryPath, ReminderSounds.WithVolume(data, volume));
                path = temporaryPath;
            }
            request.Token.ThrowIfCancellationRequested();
            await playback(path, duration, request.Token).ConfigureAwait(false);
        }
        finally
        {
            // Platform playback releases the file before this task completes, including cancellation.
            if (temporaryPath is not null)
            {
                try { File.Delete(temporaryPath); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { AppPaths.Log(error); }
            }
            lock (gate)
            {
                if (current == request) current = null;
                request.Dispose();
            }
        }
    }

    private static Task PlayPlatform(string path, TimeSpan duration, CancellationToken cancellationToken) => NativeSoundPlayback.Play(path, cancellationToken);

    public void Dispose()
    {
        lock (gate) { disposed = true; current?.Cancel(); current = null; }
    }
}
