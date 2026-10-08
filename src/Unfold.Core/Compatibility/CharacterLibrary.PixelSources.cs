using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Unfold.Core;

// Legacy pixel-pet source operations retained for compatibility diagnostics.
public sealed partial class CharacterLibrary
{
    public static string Revision(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, limit) in new[] { ("source.piskel", PiskelCodec.MaxSourceBytes), ("spritesheet.png", ImageCodec.MaxFileBytes), ("character.json", 1024 * 1024) })
        {
            var bytes = ImageCodec.ReadBounded(AssetPath(directory, name), limit);
            hash.AppendData(Encoding.UTF8.GetBytes(name)); hash.AppendData(SHA256.HashData(bytes));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public (PixelDocument Document, string Revision) OpenForEditing(string id)
    {
        using var lease = Lock(); Recover(); var directory = PackagePath(id);
        var revision = Revision(directory); var document = PiskelCodec.Load(AssetPath(directory, "source.piskel"));
        if (revision != Revision(directory)) throw new IOException("Character changed while opening.");
        return (document, revision);
    }
    public CharacterPackage Save(PixelDocument document, string? id = null, string? expectedRevision = null)
    {
        document.Validate(); if (string.IsNullOrWhiteSpace(document.Name)) throw new InvalidDataException("Enter a character name.");
        var source = PiskelCodec.Encode(document); var png = ImageCodec.EncodePng(PiskelCodec.CompositeSheet(document));
        using var lease = Lock(); Recover();
        var targetId = id ?? $"user-{Guid.NewGuid():N}"; var target = PackagePath(targetId);
        if (id is not null && (expectedRevision is null || !Directory.Exists(target) || Revision(target) != expectedRevision))
            throw new IOException("This character changed or was deleted outside the editor. Export your work before reopening it.");
        var staging = Path.Combine(Root, $".stage-{Guid.NewGuid():N}"); var backup = Path.Combine(Root, $".backup-{targetId}");
        Directory.CreateDirectory(staging);
        try
        {
            AtomicFile.Write(Path.Combine(staging, "source.piskel"), source);
            AtomicFile.Write(Path.Combine(staging, "spritesheet.png"), png);
            var manifest = new CharacterManifest(targetId, document.Name.Trim(), 1,
                new("spritesheet.png", document.FrameCount, 1, document.Width, document.Height),
                new() { ["idle"] = new(Enumerable.Range(0, document.FrameCount).ToArray(), document.Fps) }, "pawprint.fill", "pixel");
            AtomicFile.Write(Path.Combine(staging, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions));
            var validated = LoadPackage(staging); _ = PiskelCodec.Load(Path.Combine(staging, "source.piskel"));
            if (id is not null && (!Directory.Exists(target) || Revision(target) != expectedRevision))
                throw new IOException("The character changed while preparing this save. The library was not overwritten.");
            if (Directory.Exists(target)) Directory.Move(target, backup);
            try { Directory.Move(staging, target); }
            catch { if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target); throw; }
            // Installation has committed. Cleanup failures must not report a failed save.
            try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warning?.Invoke($"Backup cleanup deferred: {ex.Message}"); }
            return new CharacterPackage(target, manifest, false, validated.StillImage);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
}
