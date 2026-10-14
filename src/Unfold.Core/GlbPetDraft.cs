using System.Text.Json;

namespace Unfold.Core;

/// <summary>User-owned mappings and a private snapshot; the original GLB is never edited.</summary>
public sealed class GlbPetDraft
{
    public static IReadOnlyList<string> Actions { get; } = System.Array.AsReadOnly(new[]
        { "idle", "click", "pickup", "land", "attention", "stretch", "celebrate", "walk", "sleep", "look", "yawn", "sulk" });
    public static IReadOnlyList<string> PointerClips { get; } = System.Array.AsReadOnly(new[] { "pickup", "land" });
    public static string ActionName(string key) => key switch
    {
        "idle" => L.T("기본 대기"), "click" => L.T("클릭"), "pickup" => L.T("들어 올리기"), "held" => L.T("드래그 중"),
        "land" => L.T("놓기"), "attention" => L.T("휴식 알림"), "stretch" => L.T("휴식 시작"), "celebrate" => L.T("휴식 완료"),
        "walk" => L.T("화면 위 걷기"), "sleep" => L.T("잠자기"), "look" => L.T("두리번거리기"), "yawn" => L.T("하품"), "sulk" => L.T("휴식 미루기"), _ => key
    };
    private readonly byte[] bytes;
    private Version contentVersion = new(1, 0, 0);
    private string? expectedRevision;
    public string? Revision => expectedRevision;
    public GlbModel Model { get; }
    public string FileName { get; }
    public string Id { get; private set; }
    public string Name { get; set; }
    public float Heading { get; set; }
    public GlbRotation? Rotation { get; set; }
    public string? RootNode { get; set; }
    public Dictionary<string, AnimationDefinition> Mappings { get; } = new(StringComparer.Ordinal);
    public GlbPetDraft(string path)
    {
        bytes = ImageCodec.ReadBounded(path); Model = GlbModel.FromSnapshot(bytes); FileName = Path.GetFileName(path); Name = Path.GetFileNameWithoutExtension(path);
        Id = "glb-" + Guid.NewGuid().ToString("N");
        string? Match(params string[] names) => names.FirstOrDefault(n => Model.Animations.Any(a => a.Name == n));
        Set("idle", Match("Cafe_Idle", "Idle", "idle", "Normal_Idle") ?? Model.Animations[0].Name, true);
        var defaults = new Dictionary<string, string?>
        {
            ["click"] = Match("Cafe_Reaction", "Click"),
            ["pickup"] = Match("Formation_Pickup", "Pickup"),
            ["land"] = Match("Formation_Idle", "Land"), ["walk"] = Match("Cafe_Walk", "Walk", "walk"),
            ["celebrate"] = Match("Victory_Start", "Celebrate")
        };
        foreach (var (key, clip) in defaults) if (clip is not null) Set(key, clip, key == "walk");
    }
    public GlbPetDraft(CharacterPackage package) : this(ReadInstalled(package)) { }
    private GlbPetDraft((CharacterPackage Package, string Revision) snapshot) : this(snapshot.Package, snapshot.Revision) { }
    private static (CharacterPackage, string) ReadInstalled(CharacterPackage package)
    {
        if (package.IsBuiltIn) throw new InvalidDataException(L.T("설치한 3D 펫을 선택해 주세요."));
        var revision = CharacterLibrary.PetRevision(package.DirectoryPath);
        return (CharacterLibrary.LoadPackage(package.DirectoryPath), revision);
    }
    private GlbPetDraft(CharacterPackage package, string revision) : this(CharacterLibrary.AssetPath(package.DirectoryPath, package.Manifest.Model?.File ?? throw new InvalidDataException("Not a GLB pet.")))
    {
        Id = package.Manifest.Id; Name = package.Manifest.Name; Heading = package.Manifest.Model!.Heading; RootNode = package.Manifest.Model.RootNode; Rotation = package.Manifest.Model.Rotation;
        var metadata = CharacterPack.ReadMetadata(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(package.DirectoryPath, "pack.json"), 64 * 1024));
        var oldVersion = CharacterPack.ParseVersion(metadata.ContentVersion); contentVersion = new(oldVersion.Major, oldVersion.Minor, checked(oldVersion.Build + 1));
        Mappings.Clear(); foreach (var (key, mapping) in package.Manifest.Animations) if (key != "held") Mappings[key] = mapping;
        if (revision != CharacterLibrary.PetRevision(package.DirectoryPath)) throw new IOException(L.T("펫이 변경됐어요. 다시 선택해 주세요."));
        expectedRevision = revision;
    }
    public void Set(string action, string? clip, bool loop = false, double speed = 1, float? heading = null, GlbRotation? rotation = null)
    {
        if (!Actions.Contains(action)) throw new InvalidDataException("Unknown GLB pet event.");
        if (clip is null) { if (action == "idle") throw new InvalidDataException(L.T("기본 대기 동작을 선택해 주세요.")); Mappings.Remove(action); return; }
        if (!Model.Animations.Any(a => a.Name == clip) || !double.IsFinite(speed) || speed is < .25 or > 3) throw new InvalidDataException("Invalid GLB event mapping.");
        var previous = Mappings.GetValueOrDefault(action);
        rotation ??= heading is null ? previous?.Rotation : null;
        heading ??= previous?.Heading;
        if (heading is { } angle && (!float.IsFinite(angle) || angle is < -180 or > 180)) throw new InvalidDataException("Invalid action heading.");
        if (rotation is { IsValid: false }) throw new InvalidDataException("Invalid action rotation.");
        Mappings[action] = new(ModelClip: clip, Speed: speed, Loop: loop, Heading: rotation?.Y ?? heading, Rotation: rotation);
    }
    private GlbDefinition Definition => new("model.glb", Heading, RootNode, Rotation);
    public GlbRotation RotationFor(string action) => Definition.ForAnimation(Mappings.GetValueOrDefault(action) ?? Mappings["idle"]).EffectiveRotation;
    public void SetRotation(string action, GlbRotation rotation)
    {
        if (!rotation.IsValid || !Mappings.TryGetValue(action, out var mapping)) throw new InvalidDataException("Invalid action rotation.");
        Set(action, mapping.ModelClip, mapping.Loop, mapping.Speed, rotation.Y, rotation);
    }
    public GlbAnimationFrames Preview(string action)
    {
        var clip = Mappings.GetValueOrDefault(action) ?? Mappings["idle"];
        return Model.CreateAnimation(clip.ModelClip!, Definition.ForAnimation(clip), Mappings["idle"].ModelClip!, clip.Speed);
    }
    public CharacterPackage Save(CharacterLibrary library)
    {
        CharacterPackage? installed = null;
        WritePack(archive =>
        {
            using var pack = CharacterPack.Open(archive);
            installed = expectedRevision is null ? library.Install(pack, null) : library.InstallEdited(pack, expectedRevision);
        });
        expectedRevision = CharacterLibrary.PetRevision(installed!.DirectoryPath);
        return installed!;
    }
    public void Export(string path) => WritePack(archive => AtomicFile.Write(path, ImageCodec.ReadBounded(archive, CharacterPack.MaxArchiveBytes)));
    private void WritePack(Action<string> complete)
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new InvalidDataException(L.T("펫 이름을 1~80자로 입력해 주세요."));
        var root = Path.Combine(Path.GetTempPath(), "Unfold-glb-" + Guid.NewGuid().ToString("N")); var directory = Path.Combine(root, Id);
        try
        {
            Directory.CreateDirectory(directory); AtomicFile.Write(Path.Combine(directory, "model.glb"), bytes);
            var mappings = Mappings.Where(pair => pair.Key != "held").ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            // Keep pickup and release complete without restoring the retired held event.
            if (PointerClips.Any(mappings.ContainsKey))
                foreach (var key in PointerClips)
                    if (!mappings.ContainsKey(key)) mappings[key] = mappings["idle"] with { Loop = false };
            var frame = Preview("idle")[0].Image;
            AtomicFile.Write(Path.Combine(directory, "preview.png"), ImageCodec.EncodePng(frame));
            var manifest = new CharacterManifest(Id, Name.Trim(), 1, new("preview.png", 1, 1, frame.Width, frame.Height), mappings,
                RenderStyle: "smooth", Model: Definition);
            AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            var archive = Path.Combine(root, "pet.unfoldpet"); CharacterPack.Create(directory, contentVersion.ToString(3), archive);
            complete(archive);
            contentVersion = new(contentVersion.Major, contentVersion.Minor, checked(contentVersion.Build + 1));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
