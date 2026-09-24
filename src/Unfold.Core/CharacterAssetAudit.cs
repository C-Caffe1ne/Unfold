using System.Security.Cryptography;
using System.Text.Json;

namespace Unfold.Core;

public sealed record CharacterClipAudit(string Key, int Frames, double DurationMs, int Width, int Height,
    long DecodedBytes, int TransparentFrames, int EmptyFrames);
public sealed record CharacterFileAudit(string Path, long Bytes, string Sha256);
public sealed record CharacterAudit(string Id, IReadOnlyList<CharacterClipAudit> Clips, IReadOnlyList<CharacterFileAudit> Files,
    IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
public sealed record CharacterAuditReport(IReadOnlyList<CharacterAudit> Characters, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0 && Characters.Count > 0 && Characters.All(character => character.Errors.Count == 0);
}

/// <summary>Read-only asset inspection with the same path validation and decoders as the app.</summary>
public static class CharacterAssetAudit
{
    private static readonly HashSet<string> OneShots = ["attention", "stretch", "celebrate", "click"];
    public static CharacterAuditReport Inspect(string root)
    {
        var characters = new List<CharacterAudit>(); var errors = new List<string>();
        if (!Directory.Exists(root)) return new(characters, ["Character directory does not exist."]);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            _ = CharacterLibrary.AssetPath(root, "character.json");
            foreach (var directory in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
            {
                var report = InspectPackage(directory);
                if (!ids.Add(report.Id)) errors.Add($"Duplicate character ID: {report.Id}");
                characters.Add(report);
            }
        }
        catch (Exception error) when (IsAssetError(error)) { errors.Add(error.Message); }
        if (characters.Count == 0) errors.Add("No character packages found.");
        return new(characters, errors);
    }
    public static CharacterAudit InspectPackage(string directory)
    {
        var clips = new List<CharacterClipAudit>(); var files = new List<CharacterFileAudit>();
        var errors = new List<string>(); var warnings = new List<string>();
        var id = Path.GetFileName(directory);
        try
        {
            var package = CharacterLibrary.LoadPackage(directory, true); id = package.Manifest.Id;
            if (id != Path.GetFileName(directory)) errors.Add("Manifest ID differs from the directory name.");
            var paths = new HashSet<string>(StringComparer.Ordinal) { "character.json", package.Manifest.SpriteSheet.File };
            foreach (var (key, definition) in package.Manifest.Animations.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!CharacterLibrary.SafeId(key)) errors.Add($"Invalid animation key: {key}");
                if (key == "idle" && !definition.Loop) errors.Add("The idle animation must loop.");
                if (OneShots.Contains(key) && definition.Loop) errors.Add($"The {key} event must not loop.");
                if (package.HasPointerArt && (key is "pickup" or "land") && definition.Loop) errors.Add($"The {key} event must not loop.");
                if (package.HasPointerArt && key == "held" && !definition.Loop) errors.Add("The held animation must loop.");
                if (definition.Gif is not null) paths.Add(definition.Gif);
                try
                {
                    var frames = package.LoadAnimation(key);
                    var first = frames[0].Image;
                    var transparent = frames.Count(frame => frame.Image.Pixels.Any(pixel => pixel >> 24 == 0));
                    var empty = frames.Count(frame => frame.Image.Pixels.All(pixel => pixel >> 24 == 0));
                    var bytes = frames.Sum(frame => (long)frame.Image.Width * frame.Image.Height * 4);
                    var duration = frames.Sum(frame => frame.Duration.TotalMilliseconds);
                    clips.Add(new(key, frames.Count, duration, first.Width, first.Height, bytes, transparent, empty));
                    if (empty > 0) errors.Add($"{key}: {empty} completely invisible frames.");
                    if (transparent != frames.Count) warnings.Add($"{key}: 일부 프레임에 투명한 영역이 없어요. 배경을 확인해 주세요.");
                    if (first.Width != package.Manifest.SpriteSheet.FrameWidth || first.Height != package.Manifest.SpriteSheet.FrameHeight)
                        warnings.Add($"{key}: 동작별 이미지 크기가 달라요. 위치와 크기가 자연스러운지 확인해 주세요.");
                    if (OneShots.Contains(key) && duration > 10000) warnings.Add($"{key}: 반응이 10초보다 길어요.");
                }
                catch (Exception error) when (IsAssetError(error)) { errors.Add($"{key}: {error.Message}"); }
            }
            foreach (var path in paths.Order(StringComparer.Ordinal))
            {
                var data = ImageCodec.ReadBounded(CharacterLibrary.AssetPath(directory, path));
                files.Add(new(path, data.LongLength, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()));
            }
            foreach (var key in OneShots.Order(StringComparer.Ordinal))
                if (!package.Manifest.Animations.ContainsKey(key)) warnings.Add($"{key} 반응이 없는 팩이에요. 해당 상황에서는 현재 동작을 유지해요.");
            if (clips.Sum(clip => clip.DecodedBytes) > ImageCodec.MaxDecodedAnimationBytes)
                warnings.Add("펫의 메모리 사용량이 커요. 이미지 크기를 줄인 팩을 권장해요.");
        }
        catch (Exception error) when (IsAssetError(error)) { errors.Add(error.Message); }
        return new(id, clips, files, errors, warnings);
    }
    private static bool IsAssetError(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException;
}
