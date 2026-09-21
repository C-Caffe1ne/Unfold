using System.Diagnostics;
using System.Runtime.InteropServices;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class ReminderSoundPlayer : IDisposable
{
    private readonly object gate = new();
    private readonly ReminderSounds sounds;
    private readonly Func<string, TimeSpan, CancellationToken, Task> playback;
    private CancellationTokenSource? current;
    private bool disposed;
    // PlaySound has one process-wide channel. An older preview must never stop a newer notification.
    private static readonly object windowsGate = new();
    private static object? windowsOwner;
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PlaySound(string? path, nint module, uint flags);

    public ReminderSoundPlayer() : this(new(Path.Combine(AppPaths.DataRoot, "Sounds")), PlayPlatform) { }
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
            var volume = Math.Clamp(settings.ReminderVolumePercent, 0, 100);
            if (volume == 0) return;
            var id = sound == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId;
            var path = sounds.Resolve(sound, id, strict);
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

    private static async Task PlayPlatform(string path, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            var owner = new object();
            lock (windowsGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!PlaySound(path, 0, 0x20000 | 0x1 | 0x2)) throw new IOException("효과음을 재생하지 못했어요.");
                windowsOwner = owner;
            }
            try { await Task.Delay(duration, cancellationToken).ConfigureAwait(false); }
            finally
            {
                lock (windowsGate)
                {
                    if (windowsOwner == owner) { PlaySound(null, 0, 0); windowsOwner = null; }
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var info = new ProcessStartInfo("/usr/bin/afplay") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            info.ArgumentList.Add(path);
            cancellationToken.ThrowIfCancellationRequested();
            using var process = Process.Start(info) ?? throw new IOException("오디오 재생을 시작하지 못했어요.");
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (process.ExitCode != 0) throw new IOException("오디오 장치에서 효과음을 재생하지 못했어요.");
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
        }
        else throw new PlatformNotSupportedException();
    }

    public void Dispose()
    {
        lock (gate) { disposed = true; current?.Cancel(); current = null; }
    }
}
