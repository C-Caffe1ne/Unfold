using System.Diagnostics;
using Unfold.Core;

namespace Unfold.Desktop;

public static class PetMediaImporter
{
    public const int MaxVideoBytes = 128 * 1024 * 1024;
    public static string? FindFFmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("UNFOLD_FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && Path.IsPathFullyQualified(configured) && File.Exists(configured)) return configured;
        var name = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var bundled = Path.Combine(AppContext.BaseDirectory, "Tools", name);
        if (File.Exists(bundled)) return bundled;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            if (Path.IsPathFullyQualified(directory) && File.Exists(Path.Combine(directory, name))) return Path.Combine(directory, name);
        return null;
    }
    public static async Task<ImportedPetClip> Import(string path, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".gif" or ".mp4" && new FileInfo(path).Length > (extension == ".gif" ? ImageCodec.MaxFileBytes : MaxVideoBytes))
            throw new InvalidDataException(extension == ".gif" ? "GIF는 32 MiB 이하의 파일을 선택해 주세요." : "MP4는 128 MiB 이하의 파일을 선택해 주세요.");
        if (extension == ".gif")
            return await Task.Run(() => ImportedPetClip.FromGif(path, ImageCodec.ReadBounded(path)), cancellationToken);
        if (extension != ".mp4") throw new InvalidDataException("GIF 또는 MP4 파일을 선택해 주세요.");
        var executable = FindFFmpeg() ?? throw new InvalidOperationException("영상 변환 도구가 없는 앱이에요. MP4 지원 배포본을 사용하거나 GIF 파일을 가져와 주세요.");
        var root = Path.Combine(Path.GetTempPath(), "Unfold-video-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.mp4"); var output = Path.Combine(root, "animation.gif");
            await Task.Run(() => AtomicFile.Write(source, ImageCodec.ReadBounded(path, MaxVideoBytes)), cancellationToken);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            // Bound decoding and output, ignore audio, and permit only local input protocols.
            foreach (var argument in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-max_alloc", "67108864", "-threads", "2",
                "-protocol_whitelist", "file,pipe", "-f", "mov", "-t", "10.1", "-i", source,
                "-map", "0:v:0", "-an", "-sn", "-dn", "-filter_threads", "1",
                "-vf", "fps=12,scale=192:192:force_original_aspect_ratio=decrease,setsar=1,split[a][b];[a]palettegen=stats_mode=single[p];[b][p]paletteuse=new=1",
                "-frames:v", "121", "-loop", "0", "-f", "gif", output }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new IOException("영상 변환을 시작하지 못했어요.");
            var errors = ReadErrors(process.StandardError);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync(CancellationToken.None); await errors;
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidDataException("영상 변환 시간이 초과됐어요. 더 짧거나 작은 MP4를 선택해 주세요.");
            }
            var details = await errors;
            if (process.ExitCode != 0) throw new InvalidDataException("MP4를 읽지 못했어요. 재생 가능한 영상 파일인지 확인해 주세요.", new IOException(details));
            var clip = await Task.Run(() => ImportedPetClip.FromGif(path, ImageCodec.ReadBounded(output)), cancellationToken);
            if (clip.FrameCount > 120) throw new InvalidDataException("MP4는 10초 이하의 영상으로 선택해 주세요. 파일을 자동으로 자르지는 않아요.");
            return clip;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static async Task<string> ReadErrors(StreamReader reader)
    {
        var result = new System.Text.StringBuilder(); var buffer = new char[1024]; int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            if (result.Length < 2048) result.Append(buffer, 0, Math.Min(count, 2048 - result.Length));
        return result.ToString().Trim();
    }
}
