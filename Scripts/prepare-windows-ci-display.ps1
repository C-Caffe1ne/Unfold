$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'This display setup is only for disposable GitHub Actions runners.' }

# The hosted runner starts at 1024x768, which makes Windows clamp the
# 1120x800 native diagnostic window. Keep the diagnostic assertions intact.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class UnfoldCiDisplay {
    [StructLayout(LayoutKind.Explicit, Size = 220, CharSet = CharSet.Unicode)]
    public struct Mode {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(72)] public uint Fields;
        [FieldOffset(172)] public uint Width;
        [FieldOffset(176)] public uint Height;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool EnumDisplaySettings(string device, int mode, ref Mode value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int ChangeDisplaySettings(ref Mode mode, uint flags);
    public static string Prepare() {
        var mode = new Mode { Size = 220 };
        if (!EnumDisplaySettings(null, -1, ref mode)) throw new Exception("Cannot read the CI display mode.");
        var before = mode.Width + "x" + mode.Height;
        mode.Fields = 0x80000 | 0x100000; // DM_PELSWIDTH | DM_PELSHEIGHT
        mode.Width = 1920; mode.Height = 1080;
        var status = ChangeDisplaySettings(ref mode, 0); // session only, not persisted
        if (status != 0) throw new Exception("Cannot enlarge the CI display: " + status);
        if (!EnumDisplaySettings(null, -1, ref mode) || mode.Width < 1280 || mode.Height < 900)
            throw new Exception("The CI display is still too small for native layout checks.");
        return "CI display: " + before + " -> " + mode.Width + "x" + mode.Height;
    }
}
'@
[UnfoldCiDisplay]::Prepare()
