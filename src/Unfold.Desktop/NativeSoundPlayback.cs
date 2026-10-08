using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Retains audio until the device reports completion, rather than stopping at an estimated duration.</summary>
internal static class NativeSoundPlayback
{
    private static readonly object windowsGate = new();
    private static WaveOutput? windowsCurrent;
    private static readonly Lazy<nint> appKit = new(() => NativeLibrary.Load("/System/Library/Frameworks/AppKit.framework/AppKit"));

    internal static Task Play(string path, CancellationToken token) => OperatingSystem.IsWindows() ? PlayWindows(path, token)
        : OperatingSystem.IsMacOS() ? PlayMac(path, token) : throw new PlatformNotSupportedException();

    private static async Task PlayMac(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); _ = appKit.Value;
        var file = StringMessage(Message(Class("NSString"), Selector("alloc")), Selector("initWithUTF8String:"), path);
        nint sound = 0;
        try
        {
            sound = FileMessage(Message(Class("NSSound"), Selector("alloc")), Selector("initWithContentsOfFile:byReference:"), file, false);
            if (sound == 0 || !BoolMessage(sound, Selector("play"))) throw new IOException("오디오 장치에서 효과음을 재생하지 못했어요.");
            // NSSound stays in the app, so a short-lived command-line player's shutdown cannot cut its tail.
            while (BoolMessage(sound, Selector("isPlaying"))) await Task.Delay(10, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            if (sound != 0) { BoolMessage(sound, Selector("stop")); VoidMessage(sound, Selector("release")); }
            if (file != 0) VoidMessage(file, Selector("release"));
        }
    }

    private static async Task PlayWindows(string path, CancellationToken token)
    {
        var (format, samples) = PcmBuffer(ImageCodec.ReadBounded(path, ReminderSoundImporter.MaxFileBytes));
        WaveOutput output;
        lock (windowsGate)
        {
            token.ThrowIfCancellationRequested(); windowsCurrent?.Stop();
            output = new(format, samples); windowsCurrent = output;
        }
        try
        {
            while (!output.Done) await Task.Delay(10, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }
        finally
        {
            lock (windowsGate)
            {
                if (windowsCurrent == output) windowsCurrent = null;
                output.Dispose(); // Reset/unprepare/close only this request's device and buffer.
            }
        }
    }

    internal static (byte[] Format, byte[] Samples) PcmBuffer(ReadOnlySpan<byte> data)
    {
        ReminderSounds.Validate(data);
        var format = new byte[18]; using var samples = new MemoryStream();
        for (var at = 12; at + 8 <= data.Length;)
        {
            var count = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at + 4, 4));
            if (data.Slice(at, 4).SequenceEqual("fmt "u8)) data.Slice(at + 8, 16).CopyTo(format);
            if (data.Slice(at, 4).SequenceEqual("data"u8)) samples.Write(data.Slice(at + 8, count));
            at += 8 + count + (count & 1);
        }
        return (format, samples.ToArray());
    }

    private sealed class WaveOutput : IDisposable
    {
        private nint device, buffer, header;
        private bool prepared;
        private static readonly uint headerSize = (uint)Marshal.SizeOf<WaveHeader>();
        private static readonly int flagsOffset = Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags)).ToInt32();
        public bool Done => (Marshal.ReadInt32(header, flagsOffset) & 1) != 0; // WHDR_DONE, set by the device after all samples.
        public WaveOutput(byte[] format, byte[] samples)
        {
            var formatPointer = Marshal.AllocHGlobal(format.Length);
            try
            {
                Marshal.Copy(format, 0, formatPointer, format.Length);
                Check(waveOutOpen(out device, uint.MaxValue, formatPointer, 0, 0, 0));
                buffer = Marshal.AllocHGlobal(samples.Length); Marshal.Copy(samples, 0, buffer, samples.Length);
                header = Marshal.AllocHGlobal((int)headerSize);
                Marshal.StructureToPtr(new WaveHeader { Data = buffer, BufferLength = (uint)samples.Length }, header, false);
                Check(waveOutPrepareHeader(device, header, headerSize)); prepared = true;
                Check(waveOutWrite(device, header, headerSize));
            }
            catch { Dispose(); throw; }
            finally { Marshal.FreeHGlobal(formatPointer); }
        }
        public void Stop() { if (device != 0) Check(waveOutReset(device)); }
        public void Dispose()
        {
            if (device != 0)
            {
                if (prepared) { Stop(); Check(waveOutUnprepareHeader(device, header, headerSize)); prepared = false; }
                Check(waveOutClose(device)); device = 0;
            }
            if (header != 0) { Marshal.FreeHGlobal(header); header = 0; }
            if (buffer != 0) { Marshal.FreeHGlobal(buffer); buffer = 0; }
        }
        private static void Check(uint result) { if (result != 0) throw new IOException($"오디오 장치에서 효과음을 재생하지 못했어요. (오류 {result})"); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public nint Data; public uint BufferLength, BytesRecorded; public nuint User;
        public uint Flags, Loops; public nint Next; public nuint Reserved;
    }
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out nint handle, uint device, nint format, nuint callback, nuint instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(nint handle);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(nint handle, nint header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(nint handle);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")] private static extern nint Class(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")] private static extern nint Selector(string name);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern nint Message(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] private static extern void VoidMessage(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool BoolMessage(nint receiver, nint selector);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint StringMessage(nint receiver, nint selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint FileMessage(nint receiver, nint selector, nint path, [MarshalAs(UnmanagedType.I1)] bool byReference);
}
