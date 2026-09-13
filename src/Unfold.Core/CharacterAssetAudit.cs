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
                    if (transparent != frames.Count) warnings.Add($"{key}: some frames have no fully transparent pixels; inspect the background.");
                    if (first.Width != package.Manifest.SpriteSheet.FrameWidth || first.Height != package.Manifest.SpriteSheet.FrameHeight)
                        warnings.Add($"{key}: frame size differs from the sprite grid; check alignment and apparent size.");
                    if (OneShots.Contains(key) && duration > 10000) warnings.Add($"{key}: event exceeds the 10-second production target.");
                }
                catch (Exception error) when (IsAssetError(error)) { errors.Add($"{key}: {error.Message}"); }
            }
            foreach (var path in paths.Order(StringComparer.Ordinal))
            {
                var data = ImageCodec.ReadBounded(CharacterLibrary.AssetPath(directory, path));
                files.Add(new(path, data.LongLength, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant()));
            }
            foreach (var key in OneShots.Order(StringComparer.Ordinal))
                if (!package.Manifest.Animations.ContainsKey(key)) warnings.Add($"Optional {key} reaction is not supplied; the pet leaves its current animation unchanged.");
            if (clips.Sum(clip => clip.DecodedBytes) > ImageCodec.MaxDecodedAnimationBytes)
                warnings.Add("Total decoded clips exceed 128 MiB before UI bitmap copies; reduce the pack budget.");
        }
        catch (Exception error) when (IsAssetError(error)) { errors.Add(error.Message); }
        return new(id, clips, files, errors, warnings);
    }
    private static bool IsAssetError(Exception error) => error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException;
}
