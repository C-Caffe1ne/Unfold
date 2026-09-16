using System.Text.Json;

namespace Unfold.Core;

/// <summary>Validated media snapshots. Source files can move after import without changing the draft.</summary>
public sealed class ImportedPetClip
{
    private readonly byte[] gif;
    public string FileName { get; }
    public int Width { get; }
    public int Height { get; }
    public int FrameCount { get; }
    public TimeSpan Duration { get; }
    public long DecodedBytes { get; }
    internal int FileBytes => gif.Length;
    private ImportedPetClip(string fileName, byte[] bytes, IReadOnlyList<AnimationFrame> frames)
    {
        FileName = fileName; gif = bytes; Width = frames[0].Image.Width; Height = frames[0].Image.Height;
        FrameCount = frames.Count; Duration = TimeSpan.FromTicks(frames.Sum(frame => frame.Duration.Ticks));
        DecodedBytes = frames.Sum(frame => (long)frame.Image.Width * frame.Image.Height * 4);
    }
    public static ImportedPetClip FromGif(string fileName, byte[] bytes)
    {
        var snapshot = bytes.ToArray();
        IReadOnlyList<AnimationFrame> frames;
        try { frames = ImageCodec.DecodeGif(snapshot); }
        catch (InvalidDataException error)
        { throw new InvalidDataException("GIF를 읽지 못했어요. 파일 손상 여부와 이미지 크기·프레임 수를 확인해 주세요.", error); }
        if (frames.Any(frame => frame.Image.Pixels.All(pixel => pixel >> 24 == 0)))
            throw new InvalidDataException("완전히 투명한 프레임이 있어요. 모든 프레임에 펫이 보이는 파일을 선택해 주세요.");
        return new(Path.GetFileName(fileName), snapshot, frames);
    }
    public IReadOnlyList<AnimationFrame> LoadFrames() => ImageCodec.DecodeGif(gif);
    internal void Write(string path) => AtomicFile.Write(path, gif);
}

public sealed class CustomPetDraft
{
    public static IReadOnlyList<string> Actions { get; } = Array.AsReadOnly(new[] { "idle", "attention", "stretch", "celebrate", "click" });
    private readonly Dictionary<string, ImportedPetClip> clips = [];
    public string Id { get; } = "custom-" + Guid.NewGuid().ToString("N");
    public IReadOnlyDictionary<string, ImportedPetClip> Clips => new System.Collections.ObjectModel.ReadOnlyDictionary<string, ImportedPetClip>(clips);
    public static string ActionName(string key) => key switch
    {
        "idle" => "쉬는 모습", "attention" => "휴식 안내", "stretch" => "스트레칭",
        "celebrate" => "휴식 완료", "click" => "클릭 반응", _ => key
    };
    public void SetClip(string action, ImportedPetClip clip)
    {
        if (!Actions.Contains(action)) throw new ArgumentException("지원하지 않는 동작이에요.");
        var others = clips.Where(pair => pair.Key != action).Select(pair => pair.Value).ToArray();
        if (others.Sum(item => item.DecodedBytes) + clip.DecodedBytes > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException("펫의 전체 애니메이션이 너무 커요. 이미지 크기나 프레임 수를 줄여 주세요. (최대 128 MiB)");
        if (others.Sum(item => (long)item.FileBytes) + clip.FileBytes > CharacterPack.MaxArchiveBytes - ImageCodec.MaxFileBytes - 64 * 1024)
            throw new InvalidDataException("펫 팩의 파일 용량이 너무 커요. 더 작은 파일을 선택해 주세요.");
        clips[action] = clip;
    }
    public void RemoveClip(string action) => clips.Remove(action);
    public void Export(string name, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new ArgumentException("펫 이름을 1~80자로 입력해 주세요.");
        if (!clips.TryGetValue("idle", out var idle)) throw new InvalidDataException("필수 동작인 ‘쉬는 모습’에 파일을 넣어 주세요.");
        var root = Path.Combine(Path.GetTempPath(), "Unfold-custom-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, Id); Directory.CreateDirectory(directory);
        try
        {
            foreach (var (key, clip) in clips) clip.Write(Path.Combine(directory, key + ".gif"));
            AtomicFile.Write(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(idle.LoadFrames()[0].Image));
            var manifest = new CharacterManifest(Id, name.Trim(), 1,
                new("spritesheet.png", 1, 1, idle.Width, idle.Height),
                clips.ToDictionary(pair => pair.Key, pair => new AnimationDefinition(Gif: pair.Key + ".gif", Loop: pair.Key == "idle")),
                RenderStyle: "smooth");
            AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            var archive = Path.Combine(root, "custom.unfoldpet");
            CharacterPack.Create(directory, "1.0.0", archive);
            // Finish validation before replacing a destination selected in the save dialog.
            AtomicFile.Write(outputPath, ImageCodec.ReadBounded(archive, CharacterPack.MaxArchiveBytes));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
