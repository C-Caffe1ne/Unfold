using Unfold.Core;

namespace Unfold.Desktop;

public static class PetMediaImporter
{
    public const int MaxVideoBytes = 128 * 1024 * 1024;
    public const string SupportedFileTypes = "PNG · JPG · WEBP · APNG · GIF · BMP · MP4 · MOV · M4V · WEBM · MKV · AVI";
    public static string[] FilePatterns => ["*.png", "*.jpg", "*.jpeg", "*.jfif", "*.webp", "*.apng", "*.gif", "*.bmp", "*.mp4", "*.mov", "*.m4v", "*.webm", "*.mkv", "*.avi"];
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
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".gif" && new FileInfo(path).Length > ImageCodec.MaxFileBytes)
            throw new InvalidDataException(L.T("GIF는 32 MiB 이하의 파일을 선택해 주세요."));
        if (extension == ".gif")
            return await Task.Run(() => ImportedPetClip.FromGif(path, ImageCodec.ReadBounded(path)), cancellationToken);
        if (extension is ".png" or ".jpg" or ".jpeg" or ".jfif" or ".webp" or ".bmp" or ".apng")
        {
            if (new FileInfo(path).Length > ImageCodec.MaxFileBytes)
                throw new InvalidDataException(L.T("이미지는 32 MiB 이하로 선택해 주세요."));
            var bytes = await Task.Run(() => ImageCodec.ReadBounded(path), cancellationToken);
            var frames = await Task.Run(() => ImageCodec.DecodePetAnimation(bytes, cancellationToken), cancellationToken);
            if (frames is null)
                return await Task.Run(() => ImportedPetClip.FromImage(path, bytes), cancellationToken);
            return await PetMediaProcessor.EncodeGif(path, frames, cancellationToken, FindFFmpeg() ?? throw MissingConverter(),
                L.T("움직이는 이미지를 변환하지 못했어요."), L.T("이미지 변환 시간이 초과됐어요. 더 짧거나 작은 파일을 선택해 주세요."));
        }
        var format = extension switch
        {
            ".mp4" or ".mov" or ".m4v" => "mov",
            ".webm" or ".mkv" => "matroska",
            ".avi" => "avi",
            _ => throw new InvalidDataException(L.F("지원하는 파일을 선택해 주세요: {0}", SupportedFileTypes))
        };
        if (new FileInfo(path).Length > MaxVideoBytes)
            throw new InvalidDataException(L.T("영상은 128 MiB 이하의 파일을 선택해 주세요."));
        var executable = FindFFmpeg() ?? throw MissingConverter();
        var root = Path.Combine(Path.GetTempPath(), "Unfold-video-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source" + extension); var output = Path.Combine(root, "animation.gif");
            await Task.Run(() => AtomicFile.Write(source, ImageCodec.ReadBounded(path, MaxVideoBytes)), cancellationToken);
            // Bound decoding and output, ignore audio, and permit only local input protocols.
            await MediaConversion.Run(executable, new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-max_alloc", "67108864", "-threads", "2",
                "-protocol_whitelist", "file,pipe", "-f", format, "-t", "10.1", "-i", source,
                "-map", "0:v:0", "-an", "-sn", "-dn", "-filter_threads", "1",
                "-vf", "fps=12,scale=192:192:force_original_aspect_ratio=decrease,setsar=1,split[a][b];[a]palettegen=stats_mode=single[p];[b][p]paletteuse=new=1",
                "-frames:v", "121", "-loop", "0", "-f", "gif", output },
                L.T("영상을 읽지 못했어요. 지원하는 코덱의 영상인지 확인해 주세요."),
                L.T("영상 변환 시간이 초과됐어요. 더 짧거나 작은 영상을 선택해 주세요."), cancellationToken);
            var clip = await Task.Run(() => ImportedPetClip.FromGif(path, ImageCodec.ReadBounded(output)), cancellationToken);
            if (clip.FrameCount > 120) throw new InvalidDataException(L.T("영상은 10초 이하로 선택해 주세요. 파일을 자동으로 자르지는 않아요."));
            return clip;
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static InvalidOperationException MissingConverter() => new(L.T("미디어 변환 도구가 없어요. 변환 도구가 포함된 배포본을 사용해 주세요."));
}
