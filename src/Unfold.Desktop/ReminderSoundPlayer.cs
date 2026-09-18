using System.Diagnostics;
using System.Runtime.InteropServices;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class ReminderSoundPlayer : IDisposable
{
    private Process? process;
    private CancellationTokenSource? previewCancellation;
    private int generation;
    private readonly ReminderSounds sounds = new(Path.Combine(AppPaths.DataRoot, "Sounds"));
    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PlaySound(string? path, nint module, uint flags);

    public void Play(ReminderSound sound, AppSettings settings)
    {
        if (!settings.ReminderSoundsEnabled) return;
        try
        {
            var path = sounds.Resolve(sound, sound == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId);
            Stop();
            if (OperatingSystem.IsWindows())
            {
                if (!PlaySound(path, 0, 0x20000 | 0x1 | 0x2)) throw new IOException("효과음을 재생하지 못했어요.");
            }
            else if (OperatingSystem.IsMacOS())
            {
                var info = new ProcessStartInfo("/usr/bin/afplay") { UseShellExecute = false, CreateNoWindow = true };
                info.ArgumentList.Add(path); process = Process.Start(info);
            }
        }
        catch (Exception error) { AppPaths.Log(error); }
    }
    public void Stop()
    {
        generation++;
        previewCancellation?.Cancel(); previewCancellation = null;
        try { if (process is { HasExited: false }) process.Kill(); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { AppPaths.Log(error); }
        process?.Dispose(); process = null;
        if (OperatingSystem.IsWindows()) PlaySound(null, 0, 0);
    }
    /// <summary>Explicit preview ignores the notification mute switch and reports playback failures.</summary>
    public async Task Preview(ReminderSound sound, AppSettings settings, CancellationToken cancellationToken)
    {
        Stop();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        previewCancellation = cancellation;
        var request = generation;
        try
        {
            var id = sound == ReminderSound.Due ? settings.ReminderSoundId : settings.CompletionSoundId;
            var path = sounds.Resolve(sound, id, strict: true);
            var duration = ReminderSounds.Duration(ImageCodec.ReadBounded(path, 5 * 1024 * 1024));
            cancellation.Token.ThrowIfCancellationRequested();
            if (OperatingSystem.IsWindows())
            {
                if (!PlaySound(path, 0, 0x20000 | 0x1 | 0x2)) throw new IOException("효과음을 재생하지 못했어요.");
                await Task.Delay(duration, cancellation.Token);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var info = new ProcessStartInfo("/usr/bin/afplay") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
                info.ArgumentList.Add(path);
                var playback = Process.Start(info) ?? throw new IOException("오디오 재생을 시작하지 못했어요.");
                process = playback;
                await playback.WaitForExitAsync(cancellation.Token);
                if (playback.ExitCode != 0) throw new IOException("오디오 장치에서 효과음을 재생하지 못했어요.");
            }
            else throw new PlatformNotSupportedException();
        }
        finally { if (generation == request) Stop(); }
    }
    public void Dispose() => Stop();
}
