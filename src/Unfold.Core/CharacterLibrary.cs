using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Unfold.Core;

public sealed record SheetDefinition(string File, int Columns, int Rows, int FrameWidth, int FrameHeight);
public sealed record AnimationDefinition(int[]? Frames = null, double? Fps = null, string? Gif = null, bool Loop = true);
public sealed record CharacterManifest(string Id, string Name, int Version, SheetDefinition SpriteSheet,
    Dictionary<string, AnimationDefinition> Animations, string? ThumbnailSymbol = null, string? RenderStyle = null);

public sealed class CharacterPackage
{
    public string DirectoryPath { get; }
    public CharacterManifest Manifest { get; }
    public bool IsBuiltIn { get; }
    private readonly Lazy<PixelImage> sheet;
    public PixelImage Sheet => sheet.Value;
    public override string ToString() => Manifest.Name;
    internal CharacterPackage(string directory, CharacterManifest manifest, bool builtIn)
    {
        DirectoryPath = directory; Manifest = manifest; IsBuiltIn = builtIn;
        sheet = new(() => ImageCodec.DecodePng(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(directory, manifest.SpriteSheet.File))));
    }
    public IReadOnlyList<AnimationFrame> LoadAnimation(string key)
    {
        if (!Manifest.Animations.TryGetValue(key, out var definition)) definition = Manifest.Animations["idle"];
        if (definition.Gif is not null)
            return ImageCodec.DecodeGif(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(DirectoryPath, definition.Gif)));
        var sprite = Manifest.SpriteSheet; var image = Sheet;
        var frames = new List<AnimationFrame>();
        foreach (var index in definition.Frames!)
        {
            var pixels = new uint[sprite.FrameWidth * sprite.FrameHeight];
            var x = index % sprite.Columns * sprite.FrameWidth; var y = index / sprite.Columns * sprite.FrameHeight;
            for (var row = 0; row < sprite.FrameHeight; row++)
                Array.Copy(image.Pixels, (y + row) * image.Width + x, pixels, row * sprite.FrameWidth, sprite.FrameWidth);
            frames.Add(new(new(sprite.FrameWidth, sprite.FrameHeight, pixels), TimeSpan.FromSeconds(1 / definition.Fps!.Value)));
        }
        return frames;
    }
}

public sealed class CharacterLibrary
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 32 };
    public string Root { get; }
    public event Action<string>? Warning;
    public CharacterLibrary(string root) { Root = Path.GetFullPath(root); Directory.CreateDirectory(Root); }
    public static bool SafeId(string? id) => id is { Length: > 0 and <= 128 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public string PackagePath(string id) => SafeId(id) ? Path.Combine(Root, id) : throw new InvalidDataException("Invalid character ID.");
    public static string AssetPath(string directory, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\0'))
            throw new InvalidDataException("Invalid asset path.");
        var segments = relative.Replace('\\', '/').Split('/');
        if (segments.Any(s => s is "" or "." or "..")) throw new InvalidDataException("Asset path traversal is not allowed.");
        var current = directory;
        RejectLink(current);
        foreach (var segment in segments) { current = Path.Combine(current, segment); RejectLink(current); }
        return current;
    }
    private static void RejectLink(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Linked character files are not supported.");
    }
    public static CharacterPackage LoadPackage(string directory, bool builtIn = false)
    {
        var data = ImageCodec.ReadBounded(AssetPath(directory, "character.json"), 1024 * 1024);
        var manifest = JsonSerializer.Deserialize<CharacterManifest>(data, JsonOptions) ?? throw new InvalidDataException("Missing character manifest.");
        if (manifest.Id is null || !SafeId(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Name) || manifest.Version != 1 || manifest.SpriteSheet is null || manifest.Animations is null)
            throw new InvalidDataException("Invalid character manifest.");
        var s = manifest.SpriteSheet;
        if (s.Columns is < 1 or > 256 || s.Rows is < 1 or > 256 || s.FrameWidth is < 1 or > 2048 || s.FrameHeight is < 1 or > 2048 ||
            (long)s.Columns * s.FrameWidth > 4096 || (long)s.Rows * s.FrameHeight > 4096)
            throw new InvalidDataException("Invalid sprite sheet grid.");
        var package = new CharacterPackage(directory, manifest, builtIn);
        var image = package.Sheet;
        if (image.Width != s.Columns * s.FrameWidth || image.Height != s.Rows * s.FrameHeight) throw new InvalidDataException("Sprite sheet dimensions do not match the manifest.");
        if (!manifest.Animations.ContainsKey("idle")) throw new InvalidDataException("A character needs an idle animation.");
        foreach (var animation in manifest.Animations.Values)
        {
            if (animation is null) throw new InvalidDataException("Missing animation.");
            if (animation.Gif is not null) { _ = AssetPath(directory, animation.Gif); if (!File.Exists(AssetPath(directory, animation.Gif))) throw new FileNotFoundException("Missing GIF."); }
            else if (animation.Frames is not { Length: > 0 and <= 4096 } || animation.Frames.Any(f => f < 0 || f >= s.Columns * s.Rows) ||
                animation.Fps is not double fps || !double.IsFinite(fps) || fps is < 1 or > 120) throw new InvalidDataException("Invalid sprite animation.");
        }
        return package;
    }
    public IReadOnlyList<CharacterPackage> List()
    {
        using var lease = Lock(); Recover();
        var results = new List<CharacterPackage>();
        foreach (var directory in Directory.EnumerateDirectories(Root).Order())
        {
            var id = Path.GetFileName(directory); if (!SafeId(id)) continue;
            try { var item = LoadPackage(directory); if (item.Manifest.Id != id) throw new InvalidDataException("Package ID differs from directory."); results.Add(item); }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException) { Warning?.Invoke($"Skipped {id}: {ex.Message}"); }
        }
        return results;
    }
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
            _ = LoadPackage(staging); _ = PiskelCodec.Load(Path.Combine(staging, "source.piskel"));
            if (id is not null && (!Directory.Exists(target) || Revision(target) != expectedRevision))
                throw new IOException("The character changed while preparing this save. The library was not overwritten.");
            if (Directory.Exists(target)) Directory.Move(target, backup);
            try { Directory.Move(staging, target); }
            catch { if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target); throw; }
            // Installation has committed. Cleanup failures must not report a failed save.
            try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warning?.Invoke($"Backup cleanup deferred: {ex.Message}"); }
            return new CharacterPackage(target, manifest, false);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }
    public void Delete(string id)
    {
        using var lease = Lock(); Recover(); var directory = PackagePath(id); RejectLink(directory);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    private FileStream Lock()
    {
        Directory.CreateDirectory(Root);
        // Only one writer/recovery operation may manipulate staging directories.
        return new FileStream(Path.Combine(Root, ".library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    private void Recover()
    {
        foreach (var backup in Directory.EnumerateDirectories(Root, ".backup-*"))
        {
            RejectLink(backup); var id = Path.GetFileName(backup)[8..]; if (!SafeId(id)) continue;
            var target = PackagePath(id);
            if (!Directory.Exists(target)) Directory.Move(backup, target);
            else Directory.Delete(backup, true);
        }
        foreach (var stage in Directory.EnumerateDirectories(Root, ".stage-*"))
            if (Guid.TryParseExact(Path.GetFileName(stage)[7..], "N", out _)) { RejectLink(stage); Directory.Delete(stage, true); }
    }
}

public static class AtomicFile
{
    public static void Write(string path, byte[] data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(data); stream.Flush(true); }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
