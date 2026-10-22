using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Applies <see cref="ClipProcessing"/> to an imported clip and stores the result as a new clip.</summary>
public static class PetMediaProcessor
{
    public static async Task<ImportedPetClip> Apply(ImportedPetClip source, ClipProcessing settings, CancellationToken cancellationToken = default)
    {
        if (!settings.IsActive) return source;
        var frames = await Task.Run(() => PetMediaProcessing.Apply(source.LoadFrames(), settings), cancellationToken);
        if (source.IsStillImage)
            return await Task.Run(() => ImportedPetClip.FromImage(source.FileName, ImageCodec.EncodePng(frames[0].Image)), cancellationToken);
        return await EncodeGif(source.FileName, frames, cancellationToken);
    }

    /// <summary>GIF has one-bit transparency; FFmpeg writes per-frame palettes, then each delay is set from the source.
    /// Frames go in as raw BGRA because the bundled LGPL FFmpeg has no PNG decoder.</summary>
    internal static async Task<ImportedPetClip> EncodeGif(string fileName, IReadOnlyList<AnimationFrame> frames,
        CancellationToken cancellationToken, string? executable = null, string? failure = null, string? timeoutMessage = null)
    {
        failure ??= L.T("배경을 지운 파일을 만들지 못했어요.");
        timeoutMessage ??= L.T("배경 지우기 시간이 초과됐어요. 더 짧거나 작은 파일을 선택해 주세요.");
        executable ??= PetMediaImporter.FindFFmpeg() ?? throw new InvalidOperationException(L.T("배경 지우기에 필요한 변환 도구가 없어요."));
        var root = Path.Combine(Path.GetTempPath(), "Unfold-clean-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var width = frames.Max(frame => frame.Image.Width); var height = frames.Max(frame => frame.Image.Height);
            var input = Path.Combine(root, "frames.bgra");
            await Task.Run(() => WriteBgra(input, frames, width, height), cancellationToken);
            var output = Path.Combine(root, "clean.gif");
            await MediaConversion.Run(executable, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-threads", "2",
                "-protocol_whitelist", "file", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{width}x{height}",
                "-framerate", "100", "-i", input,
                "-filter_complex", "split[a][b];[a]palettegen=reserve_transparent=1:stats_mode=single[p];[b][p]paletteuse=new=1:alpha_threshold=128",
                "-fps_mode", "passthrough", "-gifflags", "-offsetting-transdiff", "-loop", "0", "-f", "gif", output },
                failure, timeoutMessage, cancellationToken);
            var delays = frames.Select(frame => (int)Math.Round(frame.Duration.TotalMilliseconds / 10)).ToArray();
            return await Task.Run(() =>
            {
                byte[] timed;
                try { timed = GifTiming.RewriteDelays(ImageCodec.ReadBounded(output), delays, clearBetweenFrames: true); }
                catch (InvalidDataException error) { throw new InvalidDataException(failure, error); }
                return ImportedPetClip.FromGif(fileName, timed);
            }, cancellationToken);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    // Little-endian 0xAARRGGBB pixels are B, G, R, A bytes. Smaller frames pad transparently from the top left.
    private static void WriteBgra(string path, IReadOnlyList<AnimationFrame> frames, int width, int height)
    {
        using var stream = File.Create(path);
        var row = new uint[width];
        foreach (var frame in frames)
        {
            var image = frame.Image;
            for (var y = 0; y < height; y++)
            {
                Array.Clear(row);
                if (y < image.Height) image.Pixels.AsSpan(y * image.Width, image.Width).CopyTo(row);
                stream.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(row.AsSpan()));
            }
        }
    }
}
