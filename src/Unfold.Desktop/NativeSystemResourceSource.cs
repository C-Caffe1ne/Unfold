using System.Runtime.InteropServices;

namespace Unfold.Desktop;

internal sealed class NativeSystemResourceSource : ISystemResourceSource
{
    private const string LibSystem = "/usr/lib/libSystem.B.dylib";
    private const int HostCpuLoadInfo = 3;
    private const int HostVmInfo64 = 4;
    private const uint MacVmRev1Count = 38;
    private static readonly Lazy<uint> MacTaskPort = new(() =>
    {
        // mach_task_self() is a macro for this process-owned port. Calling the
        // task_self_trap repeatedly would acquire another send-right reference.
        var library = NativeLibrary.Load(LibSystem);
        try { return unchecked((uint)Marshal.ReadInt32(NativeLibrary.GetExport(library, "mach_task_self_"))); }
        finally { NativeLibrary.Free(library); }
    });

    public SystemResourceReading Read(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsMacOS()) return ReadMac(cancellationToken);
        if (OperatingSystem.IsWindows()) return ReadWindows(cancellationToken);
        return default;
    }

    private static SystemResourceReading ReadMac(CancellationToken cancellationToken)
    {
        var task = MacTaskPort.Value;
        var host = mach_host_self();
        if (host == 0) return default;
        try
        {
            CpuTimeCounters? cpu = null;
            uint cpuCount = 4;
            if (host_statistics(host, HostCpuLoadInfo, out var ticks, ref cpuCount) == 0 && cpuCount == 4)
                cpu = new(ticks.Idle, (ulong)ticks.User + ticks.System + ticks.Idle + ticks.Nice);
            cancellationToken.ThrowIfCancellationRequested();

            SystemMemoryUsage? memory = null;
            nuint totalSize = sizeof(ulong);
            uint vmCount = MacVmRev1Count;
            if (sysctlbyname("hw.memsize", out var total, ref totalSize, IntPtr.Zero, 0) == 0 &&
                totalSize == sizeof(ulong) &&
                host_page_size(host, out var pageSize) == 0 &&
                host_statistics64(host, HostVmInfo64, out var vm, ref vmCount) == 0 &&
                vmCount >= MacVmRev1Count)
            {
                // Activity Monitor's "Memory Used": app memory, wired and compressed pages.
                // File cache stays available, matching the Windows definition.
                memory = MacMemory(total, vm.InternalCount, vm.PurgeableCount, vm.WireCount, vm.CompressorCount, (ulong)pageSize);
            }
            return new(cpu, memory);
        }
        finally { _ = mach_port_deallocate(task, host); }
    }

    private static SystemResourceReading ReadWindows(CancellationToken cancellationToken)
    {
        CpuTimeCounters? cpu = null;
        // GetSystemTimes only covers the caller's primary group on >64-processor systems.
        if (GetActiveProcessorGroupCount() == 1 &&
            GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var kernelTicks = kernel.Value;
            var userTicks = user.Value;
            if (kernelTicks <= ulong.MaxValue - userTicks)
                cpu = new(idle.Value, kernelTicks + userTicks);
        }
        cancellationToken.ThrowIfCancellationRequested();

        SystemMemoryUsage? memory = null;
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (GlobalMemoryStatusEx(ref status))
            memory = WindowsMemory(status.TotalPhys, status.AvailPhys);
        return new(cpu, memory);
    }

    internal static SystemMemoryUsage? MacMemory(ulong total, uint internalPages, uint purgeablePages,
        uint wiredPages, uint compressorPages, ulong pageSize)
    {
        if (total == 0 || pageSize == 0) return null;
        // Purgeable pages are app memory the system may discard, so they count as available.
        var pages = (ulong)internalPages - Math.Min(purgeablePages, internalPages) + wiredPages + compressorPages;
        if (pages > ulong.MaxValue / pageSize) return null;
        var used = pages * pageSize;
        return used <= total ? new(used, total) : null;
    }

    internal static SystemMemoryUsage? WindowsMemory(ulong total, ulong available) =>
        total > 0 && available <= total ? new(total - available, total) : null;

    internal static (int Cpu, int Vm, int WindowsMemory) NativeStructureSizes =>
        (Marshal.SizeOf<MacCpuTicks>(), Marshal.SizeOf<MacVmStatistics>(), Marshal.SizeOf<MemoryStatusEx>());

    internal static int ReadMacPageSize(out nuint pageSize)
    {
        var task = MacTaskPort.Value;
        var host = mach_host_self();
        try { return host_page_size(host, out pageSize); }
        finally { _ = mach_port_deallocate(task, host); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MacCpuTicks { public uint User, System, Idle, Nice; }

    // Request the stable revision-1 prefix, not the growing struct in the newest SDK.
    // mach/host_info.h: HOST_VM_INFO64_REV1_COUNT = offsetof(swapped_count) / sizeof(integer_t).
    // Offsets follow struct vm_statistics64 in mach/vm_statistics.h.
    [StructLayout(LayoutKind.Explicit, Size = 152)]
    private struct MacVmStatistics
    {
        [FieldOffset(12)] public uint WireCount;
        [FieldOffset(88)] public uint PurgeableCount;
        [FieldOffset(128)] public uint CompressorCount;
        [FieldOffset(140)] public uint InternalCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport(LibSystem)] private static extern uint mach_host_self();
    [DllImport(LibSystem)] private static extern int mach_port_deallocate(uint task, uint port);
    // vm_size_t is uintptr_t for both LP64 arm64 and x86_64 SDK headers.
    [DllImport(LibSystem)] private static extern int host_page_size(uint host, out nuint pageSize);
    [DllImport(LibSystem)] private static extern int host_statistics(uint host, int flavor, out MacCpuTicks info, ref uint count);
    [DllImport(LibSystem)] private static extern int host_statistics64(uint host, int flavor, out MacVmStatistics info, ref uint count);
    [DllImport(LibSystem)] private static extern int sysctlbyname(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name, out ulong value, ref nuint size, IntPtr newValue, nuint newSize);
    [DllImport("kernel32.dll")] private static extern ushort GetActiveProcessorGroupCount();
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
}
