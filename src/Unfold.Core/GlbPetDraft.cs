using System.Text.Json;

namespace Unfold.Core;

/// <summary>User-owned mappings and a private snapshot; the original GLB is never edited.</summary>
public sealed class GlbPetDraft
{
    public static IReadOnlyList<string> Actions { get; } = System.Array.AsReadOnly(new[]
        { "idle", "hover", "click", "pickup", "held", "land", "attention", "stretch", "celebrate", "walk", "sleep", "look", "yawn", "sulk" });
    public static string ActionName(string key) => key switch
    {
        "idle" => "기본 대기", "hover" => "마우스 올리기", "click" => "클릭", "pickup" => "들어 올리기", "held" => "드래그 중",
        "land" => "놓기", "attention" => "휴식 알림", "stretch" => "휴식 시작", "celebrate" => "휴식 완료",
        "walk" => "화면 위 걷기", "sleep" => "잠자기", "look" => "두리번거리기", "yawn" => "하품", "sulk" => "휴식 미루기", _ => key
    };
    public static bool? RequiredLoop(string action) => action == "idle" ? true : null;
    private readonly byte[] bytes;
    private Version contentVersion = new(1, 0, 0);
    private string? expectedRevision;
    public string? Revision => expectedRevision;
    public GlbModel Model { get; }
    public string FileName { get; }
    public string Id { get; private set; }
    public string Name { get; set; }
    public float Heading { get; set; }
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
            ["click"] = Match("Cafe_Reaction", "Click"), ["hover"] = Match("Cafe_Reaction", "Hover"),
            ["pickup"] = Match("Formation_Pickup", "Pickup"), ["held"] = Match("Formation_Idle", "Held"),
            ["land"] = Match("Formation_Idle", "Land"), ["walk"] = Match("Cafe_Walk", "Walk", "walk"),
            ["celebrate"] = Match("Victory_Start", "Celebrate")
        };
        foreach (var (key, clip) in defaults) if (clip is not null) Set(key, clip, key is "held" or "walk");
    }
    public GlbPetDraft(CharacterPackage package) : this(ReadInstalled(package)) { }
    private GlbPetDraft((CharacterPackage Package, string Revision) snapshot) : this(snapshot.Package, snapshot.Revision) { }
    private static (CharacterPackage, string) ReadInstalled(CharacterPackage package)
    {
        if (package.IsBuiltIn) throw new InvalidDataException("설치한 3D 펫을 선택해 주세요.");
        var revision = CharacterLibrary.PetRevision(package.DirectoryPath);
        return (CharacterLibrary.LoadPackage(package.DirectoryPath), revision);
    }
    private GlbPetDraft(CharacterPackage package, string revision) : this(CharacterLibrary.AssetPath(package.DirectoryPath, package.Manifest.Model?.File ?? throw new InvalidDataException("Not a GLB pet.")))
    {
        Id = package.Manifest.Id; Name = package.Manifest.Name; Heading = package.Manifest.Model!.Heading; RootNode = package.Manifest.Model.RootNode;
        var metadata = CharacterPack.ReadMetadata(ImageCodec.ReadBounded(CharacterLibrary.AssetPath(package.DirectoryPath, "pack.json"), 64 * 1024));
        var oldVersion = CharacterPack.ParseVersion(metadata.ContentVersion); contentVersion = new(oldVersion.Major, oldVersion.Minor, checked(oldVersion.Build + 1));
        Mappings.Clear(); foreach (var (key, mapping) in package.Manifest.Animations) Mappings[key] = mapping;
        if (revision != CharacterLibrary.PetRevision(package.DirectoryPath)) throw new IOException("펫이 변경됐어요. 다시 선택해 주세요.");
        expectedRevision = revision;
    }
    public void Set(string action, string? clip, bool loop = false, double speed = 1, float? heading = null)
    {
        if (!Actions.Contains(action)) throw new InvalidDataException("Unknown GLB pet event.");
        if (clip is null) { if (action == "idle") throw new InvalidDataException("기본 대기 동작을 선택해 주세요."); Mappings.Remove(action); return; }
        if (!Model.Animations.Any(a => a.Name == clip) || !double.IsFinite(speed) || speed is < .25 or > 3) throw new InvalidDataException("Invalid GLB event mapping.");
        heading ??= Mappings.GetValueOrDefault(action)?.Heading;
        if (heading is { } angle && (!float.IsFinite(angle) || angle is < -180 or > 180)) throw new InvalidDataException("Invalid action heading.");
        loop = RequiredLoop(action) ?? loop;
        Mappings[action] = new(ModelClip: clip, Speed: speed, Loop: loop, Heading: heading);
    }
    public GlbAnimationFrames Preview(string action)
    {
        var clip = Mappings.GetValueOrDefault(action) ?? Mappings["idle"];
        return Model.CreateAnimation(clip.ModelClip!, new("model.glb", clip.Heading ?? Heading, RootNode), Mappings["idle"].ModelClip!, clip.Speed);
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
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80) throw new InvalidDataException("펫 이름을 1~80자로 입력해 주세요.");
        var root = Path.Combine(Path.GetTempPath(), "Unfold-glb-" + Guid.NewGuid().ToString("N")); var directory = Path.Combine(root, Id);
        try
        {
            Directory.CreateDirectory(directory); AtomicFile.Write(Path.Combine(directory, "model.glb"), bytes);
            var mappings = new Dictionary<string, AnimationDefinition>(Mappings, StringComparer.Ordinal);
            // Keep the existing pointer lifecycle complete even when one phase is unassigned.
            if (OriginalCompanion.PointerClips.Any(mappings.ContainsKey))
                foreach (var key in OriginalCompanion.PointerClips)
                    if (!mappings.ContainsKey(key)) mappings[key] = mappings["idle"] with { Loop = key == "held" };
            var frame = Preview("idle")[0].Image;
            AtomicFile.Write(Path.Combine(directory, "preview.png"), ImageCodec.EncodePng(frame));
            var manifest = new CharacterManifest(Id, Name.Trim(), 1, new("preview.png", 1, 1, frame.Width, frame.Height), mappings,
                RenderStyle: "smooth", Model: new("model.glb", Heading, RootNode));
            AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            var archive = Path.Combine(root, "pet.unfoldpet"); CharacterPack.Create(directory, contentVersion.ToString(3), archive);
            complete(archive);
            contentVersion = new(contentVersion.Major, contentVersion.Minor, checked(contentVersion.Build + 1));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
