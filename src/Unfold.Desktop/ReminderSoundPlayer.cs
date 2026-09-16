using System.Diagnostics;
using System.Runtime.InteropServices;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class ReminderSoundPlayer : IDisposable
{
    private Process? process;
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
        try { if (process is { HasExited: false }) process.Kill(); }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { AppPaths.Log(error); }
        process?.Dispose(); process = null;
        if (OperatingSystem.IsWindows()) PlaySound(null, 0, 0);
    }
    public void Dispose() => Stop();
}
