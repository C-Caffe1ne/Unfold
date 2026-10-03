using Unfold.Core;

namespace Unfold.Desktop;

public static class ReminderSoundImporter
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const string SupportedFileTypes = "WAV · MP3 효과음";
    public static string[] FilePatterns => ["*.wav", "*.mp3"];
    public static async Task<string> Import(string path, ReminderSounds sounds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension == ".wav") return await Task.Run(() => sounds.Import(path), cancellationToken);
        if (extension != ".mp3") throw new InvalidDataException("WAV 또는 MP3 효과음 파일을 선택해 주세요.");
        if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("효과음 파일은 5 MiB 이하로 선택해 주세요.");
        var executable = PetMediaImporter.FindFFmpeg() ?? throw new InvalidDataException("소리 변환 도구가 없는 앱이에요. MP3 지원 배포본을 사용하거나 WAV 파일을 가져와 주세요.");
        var root = Path.Combine(Path.GetTempPath(), "Unfold-sound-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.mp3"); var output = Path.Combine(root, "effect.wav");
            await Task.Run(() => AtomicFile.Write(source, ImageCodec.ReadBounded(path, MaxFileBytes)), cancellationToken);
            // Decode a bounded extra second so long effects are rejected, never silently shortened.
            await MediaConversion.Run(executable, ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-max_alloc", "67108864", "-threads", "2",
                "-protocol_whitelist", "file,pipe", "-f", "mp3", "-i", source, "-map", "0:a:0", "-vn", "-sn", "-dn",
                "-t", "31", "-map_metadata", "-1", "-ac", "1", "-ar", "44100", "-c:a", "pcm_s16le", "-f", "wav", output],
                "MP3를 읽지 못했어요. 재생 가능한 소리 파일인지 확인해 주세요.",
                "소리 변환 시간이 초과됐어요. 더 짧거나 작은 MP3를 선택해 주세요.", cancellationToken);
            var data = await Task.Run(() => ImageCodec.ReadBounded(output, MaxFileBytes), cancellationToken);
            ReminderSounds.Validate(data);
            cancellationToken.ThrowIfCancellationRequested();
            return sounds.ImportPcmWav(data);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
