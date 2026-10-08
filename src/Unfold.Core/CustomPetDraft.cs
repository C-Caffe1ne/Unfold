using System.Text.Json;

namespace Unfold.Core;

/// <summary>Validated media snapshots. Source files can move after import without changing the draft.</summary>
public sealed class ImportedPetClip
{
    private readonly byte[] media;
    private readonly PixelImage firstFrame;
    private readonly object frameGate = new();
    private WeakReference<IReadOnlyList<AnimationFrame>> decodedFrames;
    private readonly Func<IReadOnlyList<AnimationFrame>>? loadSpriteFrames;
    public PixelImage Thumbnail => new(firstFrame.Width, firstFrame.Height, firstFrame.Pixels.ToArray());
    public bool IsStillImage { get; }
    public string FileName { get; }
    public int Width { get; }
    public int Height { get; }
    public int FrameCount { get; }
    public TimeSpan Duration { get; }
    public long DecodedBytes { get; }
    internal int FileBytes => media.Length;
    private ImportedPetClip(string fileName, byte[] bytes, IReadOnlyList<AnimationFrame> frames, bool isStillImage = false)
    {
        FileName = fileName; media = bytes; IsStillImage = isStillImage; Width = frames[0].Image.Width; Height = frames[0].Image.Height;
        FrameCount = frames.Count; Duration = TimeSpan.FromTicks(frames.Sum(frame => frame.Duration.Ticks));
        DecodedBytes = frames.Sum(frame => (long)frame.Image.Width * frame.Image.Height * 4);
        var first = frames[0].Image;
        firstFrame = new(first.Width, first.Height, first.Pixels.ToArray());
        decodedFrames = new(frames);
    }
    public static ImportedPetClip FromGif(string fileName, byte[] bytes)
        => FromOwnedGif(fileName, bytes.ToArray());
    internal static ImportedPetClip FromOwnedGif(string fileName, byte[] snapshot)
    {
        IReadOnlyList<AnimationFrame> frames;
        try { frames = ImageCodec.DecodeGif(snapshot); }
        catch (InvalidDataException error)
        { throw new InvalidDataException("GIF를 읽지 못했어요. 파일 손상 여부와 이미지 크기·프레임 수를 확인해 주세요.", error); }
        if (frames.Any(frame => frame.Image.Pixels.All(pixel => pixel >> 24 == 0)))
            throw new InvalidDataException("완전히 투명한 프레임이 있어요. 모든 프레임에 펫이 보이는 파일을 선택해 주세요.");
        return new(Path.GetFileName(fileName), snapshot, frames);
    }
    public static ImportedPetClip FromImage(string fileName, byte[] bytes)
    {
        var image = ImageCodec.DecodePetImage(bytes);
        if (image.Pixels.All(pixel => pixel >> 24 == 0)) throw new InvalidDataException("완전히 투명한 이미지예요. 펫이 보이는 파일을 선택해 주세요.");
        return new(Path.GetFileName(fileName), ImageCodec.EncodePng(image), [new(image, TimeSpan.FromSeconds(1))], true);
    }
    internal ImportedPetClip(string fileName, byte[] sheet, SheetDefinition grid, AnimationDefinition definition, PixelImage image)
    {
        FileName = fileName; media = sheet; Width = grid.FrameWidth; Height = grid.FrameHeight;
        FrameCount = definition.Frames!.Length; Duration = TimeSpan.FromSeconds(FrameCount / definition.Fps!.Value);
        DecodedBytes = (long)Width * Height * FrameCount * 4;
        PixelImage Cell(PixelImage source, int index)
        {
            var pixels = new uint[Width * Height]; var x = index % grid.Columns * Width; var y = index / grid.Columns * Height;
            for (var row = 0; row < Height; row++) Array.Copy(source.Pixels, (y + row) * source.Width + x, pixels, row * Width, Width);
            return new(Width, Height, pixels);
        }
        firstFrame = Cell(image, definition.Frames[0]); decodedFrames = new(null!);
        loadSpriteFrames = () =>
        {
            var source = ImageCodec.DecodePng(sheet); var cells = new Dictionary<int, PixelImage>();
            return definition.Frames.Select(index =>
            {
                if (!cells.TryGetValue(index, out var cell)) cells[index] = cell = Cell(source, index);
                return new AnimationFrame(cell, TimeSpan.FromSeconds(1 / definition.Fps.Value));
            }).ToArray();
        };
    }
    public IReadOnlyList<AnimationFrame> LoadFrames()
    {
        lock (frameGate)
        {
            if (decodedFrames.TryGetTarget(out var cached)) return cached;
            IReadOnlyList<AnimationFrame> frames = loadSpriteFrames is not null ? loadSpriteFrames() : IsStillImage
                ? [new(ImageCodec.DecodePng(media), TimeSpan.FromSeconds(1))] : ImageCodec.DecodeGif(media);
            decodedFrames = new(frames);
            return frames;
        }
    }
    internal void Write(string path) => AtomicFile.Write(path, media);
}

public sealed partial class CustomPetDraft
{
    public static IReadOnlyList<string> Actions { get; } = Array.AsReadOnly(new[] { "idle", "attention", "stretch", "celebrate", "click", "hover", "pointerDown", "pointerUp" });
    private readonly Dictionary<string, ImportedPetClip> clips = [];
    public string Id { get; private set; } = "custom-" + Guid.NewGuid().ToString("N");
    public IReadOnlyList<string> AvailableActions => sourceManifest is null ? Actions : Actions.Concat(sourceManifest.Animations.Keys).Distinct(StringComparer.Ordinal).ToArray();
    public IReadOnlyDictionary<string, ImportedPetClip> Clips => new System.Collections.ObjectModel.ReadOnlyDictionary<string, ImportedPetClip>(clips);
    public static string ActionName(string key) => key switch
    {
        "idle" => "기본", "attention" => "알림", "stretch" => "휴식",
        "celebrate" => "휴식 완료", "click" => "클릭 반응", "hover" => "마우스 호버", "pointerDown" => "마우스 눌림", "pointerUp" => "마우스 뗌", _ => GlbPetDraft.ActionName(key)
    };
    public void SetClip(string action, ImportedPetClip clip)
    {
        if (!AvailableActions.Contains(action)) throw new ArgumentException("지원하지 않는 동작이에요.");
        var others = clips.Where(pair => pair.Key != action).Select(pair => pair.Value).ToArray();
        if (others.Sum(item => item.DecodedBytes) + clip.DecodedBytes > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException("펫의 전체 애니메이션이 너무 커요. 이미지 크기나 프레임 수를 줄여 주세요. (최대 128 MiB)");
        if (sourceManifest is null && others.Sum(item => (long)item.FileBytes) + clip.FileBytes > CharacterPack.MaxArchiveBytes - ImageCodec.MaxFileBytes - 64 * 1024)
            throw new InvalidDataException("펫 팩의 파일 용량이 너무 커요. 더 작은 파일을 선택해 주세요.");
        clips[action] = clip;
    }
    private readonly Dictionary<string, int> playback = [];
    public int Playback(string action) => playback.GetValueOrDefault(action, action == "idle" ? 1 : 0);
    public void SetPlayback(string action, int mode)
    {
        if (!AvailableActions.Contains(action) || mode is < 0 or > 2) throw new ArgumentException("잘못된 재생 설정이에요.");
        playback[action] = mode;
    }
    public void RemoveClip(string action) { clips.Remove(action); playback.Remove(action); }
    public void Export(string name, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 80)
            throw new ArgumentException("펫 이름을 1~80자로 입력해 주세요.");
        if (!clips.TryGetValue("idle", out var idle)) throw new InvalidDataException("필수 동작인 ‘기본’에 파일을 넣어 주세요.");
        if (sourceManifest is not null) { ExportEdited(name.Trim(), outputPath); return; }
        var root = Path.Combine(Path.GetTempPath(), "Unfold-custom-" + Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, Id); Directory.CreateDirectory(directory);
        try
        {
            foreach (var (key, clip) in clips.Where(pair => !pair.Value.IsStillImage)) clip.Write(Path.Combine(directory, key + ".gif"));
            var cells = new List<(string Key, PixelImage Image)> { ("idle", idle.Thumbnail) };
            if (clips.Values.Any(clip => clip.IsStillImage))
            {
                // Keep the existing version-1 sheet/GIF contract; static actions use one sheet frame.
                if (!idle.IsStillImage) cells[0] = ("idle", ImageCodec.DecodePetImage(ImageCodec.EncodePng(cells[0].Image)));
                cells.AddRange(clips.Where(pair => pair.Key != "idle" && pair.Value.IsStillImage)
                    .Select(pair => (pair.Key, pair.Value.Thumbnail)));
            }
            var width = cells.Max(cell => cell.Image.Width); var height = cells.Max(cell => cell.Image.Height);
            var sheet = new PixelImage(width * cells.Count, height, new uint[width * cells.Count * height]);
            for (var index = 0; index < cells.Count; index++)
            {
                var image = cells[index].Image; var x = index * width + (width - image.Width) / 2; var y = (height - image.Height) / 2;
                for (var row = 0; row < image.Height; row++)
                    Array.Copy(image.Pixels, row * image.Width, sheet.Pixels, (y + row) * sheet.Width + x, image.Width);
            }
            AtomicFile.Write(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(sheet));
            var manifest = new CharacterManifest(Id, name.Trim(), 1,
                new("spritesheet.png", cells.Count, 1, width, height),
                clips.ToDictionary(pair => pair.Key, pair => pair.Value.IsStillImage
                    ? new AnimationDefinition(Frames: [cells.FindIndex(cell => cell.Key == pair.Key)], Fps: 1, Loop: Playback(pair.Key) != 0, PingPong: Playback(pair.Key) == 2)
                    : new AnimationDefinition(Gif: pair.Key + ".gif", Loop: Playback(pair.Key) != 0, PingPong: Playback(pair.Key) == 2)),
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
