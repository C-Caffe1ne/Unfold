using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Unfold.Core;

public enum ReminderSound { Due, Completed }

public sealed class ReminderSounds(string directory)
{
    public string Import(string path)
    {
        byte[] data;
        try { data = ImageCodec.ReadBounded(path, 5 * 1024 * 1024); }
        catch (InvalidDataException error) { throw new InvalidDataException("효과음 파일은 5 MiB 이하로 선택해 주세요.", error); }
        Validate(data);
        var id = Convert.ToHexStringLower(SHA256.HashData(data));
        AtomicFile.Write(Path.Combine(directory, id + ".wav"), data);
        return id;
    }
    public string Resolve(ReminderSound sound, string? id, bool strict = false)
    {
        if (id is { Length: 64 } && id.All(char.IsAsciiHexDigit))
        {
            var imported = Path.Combine(directory, id + ".wav");
            if (File.Exists(imported))
            {
                try { Validate(ImageCodec.ReadBounded(imported, 5 * 1024 * 1024)); return imported; }
                catch (Exception error) when (!strict && error is (IOException or InvalidDataException or UnauthorizedAccessException)) { }
            }
        }
        if (strict && id is not null) throw new FileNotFoundException("가져온 효과음을 찾지 못했어요. 파일을 다시 선택해 주세요.");
        var path = Path.Combine(directory, sound == ReminderSound.Due ? "default-due.wav" : "default-completed.wav");
        if (!File.Exists(path)) AtomicFile.Write(path, Default(sound));
        return path;
    }
    public static TimeSpan Duration(ReadOnlySpan<byte> data)
    {
        Validate(data);
        uint rate = 0; long length = 0;
        for (var offset = 12; offset + 8 <= data.Length;)
        {
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            if (data.Slice(offset, 4).SequenceEqual("fmt "u8)) rate = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 16, 4));
            if (data.Slice(offset, 4).SequenceEqual("data"u8)) length += size;
            offset += 8 + size + (size & 1);
        }
        return TimeSpan.FromSeconds(length / (double)rate);
    }
    /// <summary>Returns a PCM copy at the requested gain, preserving every chunk and the original duration.</summary>
    public static byte[] WithVolume(ReadOnlySpan<byte> data, int percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100);
        Validate(data);
        var result = data.ToArray();
        if (percent == 100) return result;
        ushort bits = 0;
        // RIFF permits ancillary chunks, multiple data chunks, and a format chunk after the data.
        for (var offset = 12; offset + 8 <= data.Length;)
        {
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            if (data.Slice(offset, 4).SequenceEqual("fmt "u8)) bits = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset + 22, 2));
            offset += 8 + length + (length & 1);
        }
        for (var offset = 12; offset + 8 <= result.Length;)
        {
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(result.AsSpan(offset + 4, 4));
            if (result.AsSpan(offset, 4).SequenceEqual("data"u8))
            {
                var samples = result.AsSpan(offset + 8, length);
                if (bits == 8)
                {
                    for (var sample = 0; sample < samples.Length; sample++)
                        samples[sample] = (byte)(128 + (samples[sample] - 128) * percent / 100);
                }
                else
                {
                    if (samples.Length % 2 != 0) throw new InvalidDataException("PCM WAV sample is incomplete.");
                    for (var sample = 0; sample < samples.Length; sample += 2)
                    {
                        var amplitude = BinaryPrimitives.ReadInt16LittleEndian(samples.Slice(sample, 2));
                        BinaryPrimitives.WriteInt16LittleEndian(samples.Slice(sample, 2), (short)(amplitude * percent / 100));
                    }
                }
            }
            offset += 8 + length + (length & 1);
        }
        return result;
    }
    public static void Validate(ReadOnlySpan<byte> data)
    {
        static InvalidDataException Invalid() => new("효과음은 30초 이하 · 5 MiB 이하의 PCM WAV(8/16비트, 모노/스테레오) 파일을 선택해 주세요.");
        if (data.Length < 44 || data.Length > 5 * 1024 * 1024 || !data[..4].SequenceEqual("RIFF"u8) || !data[8..12].SequenceEqual("WAVE"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(data[4..8]) != data.Length - 8) throw Invalid();
        uint rate = 0, bytesPerSecond = 0; ushort block = 0; var bytes = 0; var format = false;
        for (var offset = 12; offset + 8 <= data.Length;)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            if (length > data.Length - offset - 8) throw Invalid();
            var chunk = data.Slice(offset + 8, (int)length);
            if (data.Slice(offset, 4).SequenceEqual("fmt "u8))
            {
                if (format || length < 16) throw Invalid();
                var encoding = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                var channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                rate = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
                bytesPerSecond = BinaryPrimitives.ReadUInt32LittleEndian(chunk[8..]);
                block = BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]);
                var bits = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
                if (encoding != 1 || channels is < 1 or > 2 || bits is not (8 or 16) || rate is < 8000 or > 96000 ||
                    block != channels * bits / 8 || bytesPerSecond != rate * block) throw Invalid();
                format = true;
            }
            else if (data.Slice(offset, 4).SequenceEqual("data"u8)) bytes += (int)length;
            offset += 8 + (int)length + ((int)length & 1);
        }
        if (!format || bytes == 0 || bytes % block != 0 || bytes / (double)bytesPerSecond > 30) throw Invalid();
    }
    public static byte[] Default(ReminderSound sound)
    {
        const int rate = 44100; const double noteLength = .18;
        var notes = sound == ReminderSound.Due ? new[] { 660d, 880d } : new[] { 660d, 880d, 1100d };
        var samples = (int)(rate * noteLength * notes.Length);
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((ushort)2); writer.Write((ushort)16); writer.Write("data"u8); writer.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var t = i / (double)rate; var note = Math.Min(notes.Length - 1, (int)(t / noteLength));
            var phase = t - note * noteLength;
            var envelope = Math.Min(1, phase / .015) * Math.Max(0, 1 - phase / noteLength);
            writer.Write((short)(Math.Sin(2 * Math.PI * notes[note] * phase) * envelope * 6500));
        }
        return stream.ToArray();
    }
}
