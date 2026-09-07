using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Unfold.Core;

namespace Unfold.Desktop;

public static class PlatformServices
{
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size, Time; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern double CGEventSourceSecondsSinceLastEventType(int state, uint type);

    public static TimeSpan IdleTime()
    {
        if (OperatingSystem.IsWindows())
        {
            var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
            if (!GetLastInputInfo(ref input)) throw new IOException("Windows idle-time query failed.");
            return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - input.Time));
        }
        if (OperatingSystem.IsMacOS())
        {
            var seconds = CGEventSourceSecondsSinceLastEventType(0, uint.MaxValue);
            if (!double.IsFinite(seconds) || seconds < 0) throw new IOException("macOS idle-time query failed.");
            return TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.MaxValue.TotalSeconds / 2));
        }
        throw new PlatformNotSupportedException("Automatic idle pause is supported on Windows and macOS.");
    }

    private static string LaunchAgent => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", "app.unfold.desktop.plist");
    public static bool StartsAtLogin()
    {
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Unfold") is string;
        }
        return OperatingSystem.IsMacOS() && File.Exists(LaunchAgent);
    }
    public static void SetStartAtLogin(bool enabled)
    {
        var exe = Environment.ProcessPath ?? throw new IOException("Cannot locate the app executable.");
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Publish Unfold before enabling launch at login.");
        if (OperatingSystem.IsWindows())
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled) key.SetValue("Unfold", $"\"{exe}\" --background"); else key.DeleteValue("Unfold", false);
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (enabled)
            {
                var xml = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict><key>Label</key><string>app.unfold.desktop</string>
                <key>ProgramArguments</key><array><string>{SecurityElement.Escape(exe)}</string><string>--background</string></array>
                <key>RunAtLoad</key><true/></dict></plist>
                """;
                AtomicFile.Write(LaunchAgent, System.Text.Encoding.UTF8.GetBytes(xml));
            }
            else if (File.Exists(LaunchAgent)) File.Delete(LaunchAgent);
        }
        else throw new PlatformNotSupportedException("Launch at login is supported on Windows and macOS.");
    }
}
