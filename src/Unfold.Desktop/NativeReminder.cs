using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Unfold.Desktop;

internal static class NativeReminder
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, CallbackMessage; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Timeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Title;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Shell_NotifyIconW(uint message, ref NotifyData data);
    [DllImport("user32.dll", EntryPoint = "LoadIconW")] private static extern nint LoadIcon(nint instance, nint name);
    public static void Show(Window owner)
    {
        try
        {
            if (OperatingSystem.IsWindows() && owner.TryGetPlatformHandle()?.Handle is nint hwnd)
            {
                var data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = hwnd, Id = 17, Flags = 2 | 4 | 16,
                    Icon = LoadIcon(0, 32516), Tip = "Unfold", Title = "Time to stretch", Info = "Stand up, stretch, and rest your eyes.", InfoFlags = 1, Timeout = 10000 };
                if (Shell_NotifyIconW(0, ref data))
                {
                    var cleanup = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                    void Remove() { cleanup.Stop(); Shell_NotifyIconW(2, ref data); }
                    cleanup.Tick += (_, _) => Remove(); owner.Closed += (_, _) => Remove(); cleanup.Start();
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var start = new ProcessStartInfo("/usr/bin/osascript") { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("-e"); start.ArgumentList.Add("display notification \"Stand up, stretch, and rest your eyes.\" with title \"Unfold\"");
                using var process = Process.Start(start);
            }
        }
        catch (Exception error) { AppPaths.Log(error); } // The in-app reminder stays available.
    }
}
