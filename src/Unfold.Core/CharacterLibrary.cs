using System.Text.Json;

namespace Unfold.Core;

public sealed record SheetDefinition(string File, int Columns, int Rows, int FrameWidth, int FrameHeight);
public sealed record AnimationDefinition(int[]? Frames = null, double? Fps = null, string? Gif = null, bool Loop = true, bool PingPong = false, string? ModelClip = null, double Speed = 1, float? Heading = null);
public sealed record CharacterManifest(string Id, string Name, int Version, SheetDefinition SpriteSheet,
    Dictionary<string, AnimationDefinition> Animations, string? ThumbnailSymbol = null, string? RenderStyle = null,
    string? BehaviorProfile = null, GlbDefinition? Model = null);

public sealed class CharacterPackage
{
    public string DirectoryPath { get; }
    public CharacterManifest Manifest { get; }
    public bool IsBuiltIn { get; }
    public bool IsGlb => Manifest.Model is not null;
    private readonly object modelGate = new();
    private WeakReference<GlbModel>? model;
    public GlbModel Model
    {
        get
        {
            lock (modelGate)
            {
                if (model is not null && model.TryGetTarget(out var cached)) return cached;
                var loaded = GlbModel.Load(CharacterLibrary.AssetPath(DirectoryPath, Manifest.Model?.File ?? throw new InvalidDataException("Not a GLB pet.")));
                var names = loaded.Animations.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
                if (Manifest.Animations.Values.Any(definition => definition.ModelClip is null || !names.Contains(definition.ModelClip)))
                    throw new InvalidDataException("Invalid GLB animation mapping.");
                model = new(loaded);
                return loaded;
            }
        }
    }
    public bool HasOriginalBehavior => IsGlb || Manifest.BehaviorProfile == OriginalCompanion.Profile &&
        OriginalCompanion.RequiredClips.All(Manifest.Animations.ContainsKey);
    public bool HasPointerArt => HasOriginalBehavior && OriginalCompanion.PointerClips.All(Manifest.Animations.ContainsKey);
    private readonly object sheetGate = new();
    private WeakReference<PixelImage>? sheet;
    /// <summary>The validated first cell, retained for read-only fallback rendering without file IO.</summary>
    public PixelImage StillImage { get; private set; } = null!;
    internal void CaptureStill(PixelImage image)
    {
        var sprite = Manifest.SpriteSheet;
        var pixels = new uint[sprite.FrameWidth * sprite.FrameHeight];
        for (var row = 0; row < sprite.FrameHeight; row++)
            Array.Copy(image.Pixels, row * image.Width, pixels, row * sprite.FrameWidth, sprite.FrameWidth);
        StillImage = new(sprite.FrameWidth, sprite.FrameHeight, pixels);
    }
    public PixelImage Sheet
    {
        get
        {
            lock (sheetGate)
            {
                if (sheet is not null && sheet.TryGetTarget(out var cached)) return cached;
                var image = ImageCodec.DecodePng(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(DirectoryPath, Manifest.SpriteSheet.File)));
                var sprite = Manifest.SpriteSheet;
                if (image.Width != sprite.Columns * sprite.FrameWidth || image.Height != sprite.Rows * sprite.FrameHeight)
                    throw new InvalidDataException("Sprite sheet dimensions do not match the manifest.");
                sheet = new(image);
                return image;
            }
        }
    }
    public override string ToString() => Manifest.Name;
    internal CharacterPackage(string directory, CharacterManifest manifest, bool builtIn, PixelImage? validatedStill = null)
    {
        DirectoryPath = directory; Manifest = manifest; IsBuiltIn = builtIn;
        if (validatedStill is not null) StillImage = validatedStill;
    }
    public IReadOnlyList<AnimationFrame> LoadAnimation(string key)
    {
        if (!Manifest.Animations.TryGetValue(key, out var definition)) definition = Manifest.Animations["idle"];
        if (IsGlb) return Model.CreateAnimation(definition.ModelClip!, Manifest.Model! with { Heading = definition.Heading ?? Manifest.Model!.Heading }, Manifest.Animations["idle"].ModelClip!, definition.Speed);
        if (definition.Gif is not null)
            return ImageCodec.DecodeGif(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(DirectoryPath, definition.Gif)));
        var sprite = Manifest.SpriteSheet;
        if ((long)sprite.FrameWidth * sprite.FrameHeight * definition.Frames!.Length * 4 > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException("Decoded sprite animation exceeds the animation budget.");
        var image = Sheet;
        var frames = new List<AnimationFrame>();
        var cells = new Dictionary<int, PixelImage>();
        foreach (var index in definition.Frames!)
        {
            if (!cells.TryGetValue(index, out var cell))
            {
                var pixels = new uint[sprite.FrameWidth * sprite.FrameHeight];
                var x = index % sprite.Columns * sprite.FrameWidth; var y = index / sprite.Columns * sprite.FrameHeight;
                for (var row = 0; row < sprite.FrameHeight; row++)
                    Array.Copy(image.Pixels, (y + row) * image.Width + x, pixels, row * sprite.FrameWidth, sprite.FrameWidth);
                cells[index] = cell = new(sprite.FrameWidth, sprite.FrameHeight, pixels);
            }
            frames.Add(new(cell, TimeSpan.FromSeconds(1 / definition.Fps!.Value)));
        }
        return frames;
    }
}

public sealed partial class CharacterLibrary
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true, MaxDepth = 32 };
    public string Root { get; }
    public event Action<string>? Warning;
    private readonly HashSet<string> protectedIds;
    public CharacterLibrary(string root, IEnumerable<string>? protectedIds = null)
    {
        Root = Path.GetFullPath(root); Directory.CreateDirectory(Root);
        this.protectedIds = new(protectedIds ?? [], StringComparer.OrdinalIgnoreCase);
    }
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
    public static CharacterPackage LoadPackage(string directory, bool builtIn = false) => LoadPackage(directory, builtIn, loadModel: true);
    private static CharacterPackage LoadPackage(string directory, bool builtIn, bool loadModel)
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
        package.CaptureStill(image);
        if (!manifest.Animations.ContainsKey("idle")) throw new InvalidDataException("A character needs an idle animation.");
        if (manifest.Model is not null && manifest.Animations.Count > 16) throw new InvalidDataException("Pets support at most 16 event mappings.");
        if (manifest.Model is { } model)
        {
            if (!float.IsFinite(model.Heading) || model.Heading is < -180 or > 180) throw new InvalidDataException("Invalid model heading.");
            var path = AssetPath(directory, model.File);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing GLB model.", path);
            if (new FileInfo(path).Length > ImageCodec.MaxFileBytes) throw new InvalidDataException("GLB file exceeds 32 MiB.");
            foreach (var definition in manifest.Animations.Values)
                if (definition is null || definition.ModelClip is null || !double.IsFinite(definition.Speed) || definition.Speed is < .25 or > 3 || definition.PingPong ||
                    definition.Heading is { } heading && (!float.IsFinite(heading) || heading is < -180 or > 180))
                    throw new InvalidDataException("Invalid GLB animation mapping.");
            if (!manifest.Animations["idle"].Loop || manifest.Animations.Keys.Any(key => !GlbPetDraft.Actions.Contains(key))) throw new InvalidDataException("Invalid GLB pet events.");
            if (loadModel) _ = package.Model;
            return package;
        }
        foreach (var animation in manifest.Animations.Values)
        {
            if (animation is null) throw new InvalidDataException("Missing animation.");
            if (animation.Gif is not null) { _ = AssetPath(directory, animation.Gif); if (!File.Exists(AssetPath(directory, animation.Gif))) throw new FileNotFoundException("Missing GIF."); }
            else if (animation.Frames is not { Length: > 0 and <= 4096 } || animation.Frames.Any(f => f < 0 || f >= s.Columns * s.Rows) ||
                animation.Fps is not double fps || !double.IsFinite(fps) || fps is < 1 or > 120) throw new InvalidDataException("Invalid sprite animation.");
        }
        return package;
    }
    // UI lists need manifests and thumbnails only. Installation/audit callers
    // retain eager validation; deferred models validate before their first use.
    public IReadOnlyList<CharacterPackage> List(bool loadModels = true)
    {
        using var lease = Lock(); Recover();
        var results = new List<CharacterPackage>();
        foreach (var directory in Directory.EnumerateDirectories(Root).Order())
        {
            var id = Path.GetFileName(directory); if (!SafeId(id)) continue;
            try { var item = LoadPackage(directory, builtIn: false, loadModel: loadModels); if (item.Manifest.Id != id) throw new InvalidDataException("Package ID differs from directory."); results.Add(item); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
            { Warning?.Invoke($"Skipped {id}: {ex.Message}"); }
        }
        return results;
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
