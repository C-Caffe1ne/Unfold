using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace Unfold.Desktop;

/// <summary>Lets only the desktop pet accompany other apps across macOS Spaces.</summary>
internal static class MacPetWindow
{
    // AppKit's NSWindowCollectionBehavior values (NSWindow.h).
    private const nuint CanJoinAllSpaces = 1u << 0;
    private const nuint MoveToActiveSpace = 1u << 1;
    private const nuint FullScreenPrimary = 1u << 7;
    private const nuint FullScreenAuxiliary = 1u << 8;
    private const nuint FullScreenNone = 1u << 9;
    private const nuint Primary = 1u << 16;
    private const nuint Auxiliary = 1u << 17;
    private const nuint CanJoinAllApplications = 1u << 18;

    internal static nuint CollectionBehavior(nuint existing, bool supportsAllApplications)
    {
        // Avalonia initially marks NSWindows FullScreenPrimary. It conflicts with
        // FullScreenAuxiliary; the Stage Manager roles are also mutually exclusive.
        var result = (existing & ~(MoveToActiveSpace | FullScreenPrimary | FullScreenNone |
            Primary | Auxiliary | CanJoinAllApplications)) | CanJoinAllSpaces | FullScreenAuxiliary;
        return supportsAllApplications ? result | CanJoinAllApplications : result;
    }

    internal static bool Apply(Window window)
    {
        if (!OperatingSystem.IsMacOS()) return false;
        var handle = window.TryGetPlatformHandle();
        // Headless tests also run on Macs, but do not own an AppKit object.
        if (handle is not { HandleDescriptor: "NSWindow" } || handle.Handle == 0) return false;

        var current = GetUnsigned(handle.Handle, Selector("collectionBehavior"));
        SetUnsigned(handle.Handle, Selector("setCollectionBehavior:"),
            CollectionBehavior(current, OperatingSystem.IsMacOSVersionAtLeast(13)));
        SetBool(handle.Handle, Selector("setHidesOnDeactivate:"), false);
        // Topmost already supplies NSFloatingWindowLevel. Do not activate/order the
        // window here: switching apps must not steal focus or reveal a hidden pet.
        return true;
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    private static extern nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nuint GetUnsigned(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SetUnsigned(nint receiver, nint selector, nuint value);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern void SetBool(nint receiver, nint selector, [MarshalAs(UnmanagedType.I1)] bool value);
}
