using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Unfold.Desktop;

/// <summary>The OS reduce-motion accessibility preference, re-read at most every two seconds.</summary>
internal static class MotionPreference
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static TimeSpan? checkedAt;
    private static bool reduced;

    // Tests and smoke diagnostics assert the motion contract independent of the host setting.
    internal static Func<bool> Source { get; set; } = QueryOs;

    internal static bool IsReduced() => Source();

    internal static bool QueryOs()
    {
        var now = Clock.Elapsed;
        if (checkedAt is { } at && now - at < RefreshInterval) return reduced;
        checkedAt = now;
        try
        {
            reduced = OperatingSystem.IsMacOS() ? MacReduced()
                : OperatingSystem.IsWindows() && SystemParametersInfo(0x1042, 0, out var animate, 0) && !animate; // SPI_GETCLIENTAREAANIMATION
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { reduced = false; }
        return reduced;
    }

    private static bool MacReduced()
    {
        // NSWorkspace is absent until AppKit loads, e.g. on the headless platform.
        var type = GetClass("NSWorkspace");
        var workspace = type == 0 ? 0 : SendPointer(type, Selector("sharedWorkspace"));
        return workspace != 0 && SendBool(workspace, Selector("accessibilityDisplayShouldReduceMotion"));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    private static extern nint GetClass(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint SendPointer(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);
}
