using System.Text.Json;

namespace Unfold.Core;

public sealed partial class CustomPetDraft
{
    private readonly CharacterManifest? sourceManifest;
    private readonly Dictionary<string, byte[]> sourceFiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportedPetClip> sourceClips = new(StringComparer.Ordinal);
    private readonly string? sourceDirectory;
    private string? expectedRevision;
    private Version contentVersion = new(1, 0, 0);
    public bool IsEditing => sourceManifest is not null;
    public string? Revision => expectedRevision;
    public string? OriginalName => sourceManifest?.Name;
    public bool PixelPreview => sourceManifest is not null && sourceManifest.RenderStyle != "smooth";
    public CustomPetDraft() { }
    public CustomPetDraft(CharacterPackage package)
    {
        if (package.IsBuiltIn || package.IsGlb) throw new InvalidDataException("설치한 이미지·영상 펫을 선택해 주세요.");
        sourceDirectory = package.DirectoryPath; expectedRevision = CharacterLibrary.PetRevision(sourceDirectory);
        package = CharacterLibrary.LoadPackage(sourceDirectory);
        sourceManifest = package.Manifest with { Animations = package.Manifest.Animations.ToDictionary(pair => pair.Key,
            pair => pair.Value with { Frames = pair.Value.Frames?.ToArray() }, StringComparer.Ordinal) };
        Id = sourceManifest.Id;
        var sheetName = sourceManifest.SpriteSheet.File;
        foreach (var file in sourceManifest.Animations.Values.Select(value => value.Gif).OfType<string>().Prepend(sheetName).Distinct(StringComparer.Ordinal))
            sourceFiles[file] = ImageCodec.ReadBounded(CharacterLibrary.AssetPath(sourceDirectory, file));
        if (sourceFiles.Sum(pair => (long)pair.Value.Length) > CharacterPack.MaxArchiveBytes - 64 * 1024)
            throw new InvalidDataException("펫 팩의 파일 용량이 너무 커요.");
        var sheet = ImageCodec.DecodePng(sourceFiles[sheetName]);
        foreach (var (key, definition) in sourceManifest.Animations)
        {
            var clip = definition.Gif is { } gif ? ImportedPetClip.FromOwnedGif(gif, sourceFiles[gif])
                : new ImportedPetClip(sheetName, sourceFiles[sheetName], sourceManifest.SpriteSheet, definition, sheet);
            clips[key] = sourceClips[key] = clip; playback[key] = definition.PingPong ? 2 : definition.Loop ? 1 : 0;
        }
        var metadataPath = CharacterLibrary.AssetPath(sourceDirectory, "pack.json");
        if (File.Exists(metadataPath))
        {
            var metadata = CharacterPack.ReadMetadata(ImageCodec.ReadBounded(metadataPath, 64 * 1024));
            var previous = CharacterPack.ParseVersion(metadata.ContentVersion);
            contentVersion = new(previous.Major, previous.Minor, checked(previous.Build + 1));
        }
        if (expectedRevision != CharacterLibrary.PetRevision(sourceDirectory)) throw new IOException("펫이 변경됐어요. 다시 선택해 주세요.");
    }
    public CustomPetDraft Snapshot()
    {
        var snapshot = new CustomPetDraft(this);
        foreach (var (key, clip) in clips) snapshot.clips[key] = clip;
        foreach (var (key, mode) in playback) snapshot.playback[key] = mode;
        return snapshot;
    }
    private CustomPetDraft(CustomPetDraft original)
    {
        Id = original.Id; sourceManifest = original.sourceManifest; sourceDirectory = original.sourceDirectory;
        expectedRevision = original.expectedRevision; contentVersion = original.contentVersion;
        foreach (var (key, bytes) in original.sourceFiles) sourceFiles[key] = bytes;
        foreach (var (key, clip) in original.sourceClips) sourceClips[key] = clip;
    }
    public CharacterPackage Save(CharacterLibrary library, string name)
    {
        if (!IsEditing || sourceDirectory != library.PackagePath(Id)) throw new InvalidDataException("편집할 펫을 다시 선택해 주세요.");
        var path = Path.Combine(Path.GetTempPath(), $"Unfold-edited-{Guid.NewGuid():N}.unfoldpet");
        try
        {
            Export(name, path); using var pack = CharacterPack.Open(path);
            var installed = library.InstallEdited(pack, expectedRevision!);
            expectedRevision = CharacterLibrary.PetRevision(installed.DirectoryPath);
            contentVersion = new(contentVersion.Major, contentVersion.Minor, checked(contentVersion.Build + 1));
            return installed;
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    private void ExportEdited(string name, string outputPath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"Unfold-edit-{Guid.NewGuid():N}");
        var directory = Path.Combine(root, Id); Directory.CreateDirectory(directory);
        try
        {
            var manifest = sourceManifest!;
            foreach (var (file, bytes) in sourceFiles) AtomicFile.Write(CharacterLibrary.AssetPath(directory, file), bytes);
            var definitions = new Dictionary<string, AnimationDefinition>(StringComparer.Ordinal);
            var staticClips = new List<(string Key, ImportedPetClip Clip)>();
            foreach (var (key, clip) in clips)
            {
                AnimationDefinition definition;
                if (sourceClips.GetValueOrDefault(key) == clip) definition = manifest.Animations[key];
                else if (clip.IsStillImage) { staticClips.Add((key, clip)); continue; }
                else
                {
                    var file = $"edited-{Guid.NewGuid():N}.gif"; clip.Write(Path.Combine(directory, file));
                    definition = new(Gif: file);
                }
                definitions[key] = definition with { Loop = Playback(key) != 0, PingPong = Playback(key) == 2 };
            }
            var sprite = manifest.SpriteSheet;
            if (staticClips.Count > 0 || clips["idle"] != sourceClips.GetValueOrDefault("idle"))
            {
                var source = ImageCodec.DecodePng(sourceFiles[sprite.File]);
                var idle = clips["idle"].Thumbnail;
                var width = Math.Max(sprite.FrameWidth, Math.Max(idle.Width, staticClips.Select(pair => pair.Clip.Width).DefaultIfEmpty(0).Max()));
                var height = Math.Max(sprite.FrameHeight, Math.Max(idle.Height, staticClips.Select(pair => pair.Clip.Height).DefaultIfEmpty(0).Max()));
                var count = 1 + sprite.Columns * sprite.Rows + staticClips.Count;
                var columns = Math.Min(count, Math.Min(256, 4096 / width)); var rows = (count + columns - 1) / columns;
                if (rows > 256 || rows * height > 4096) throw new InvalidDataException("새 이미지가 펫 시트에 들어가지 않아요. 더 작은 이미지를 선택해 주세요.");
                var sheet = new PixelImage(columns * width, rows * height, new uint[columns * width * rows * height]);
                void CopyCell(PixelImage image, int index, int x, int y, int cellWidth, int cellHeight)
                {
                    var targetX = index % columns * width + (width - cellWidth) / 2;
                    var targetY = index / columns * height + (height - cellHeight) / 2;
                    for (var row = 0; row < cellHeight; row++) Array.Copy(image.Pixels, (y + row) * image.Width + x,
                        sheet.Pixels, (targetY + row) * sheet.Width + targetX, cellWidth);
                }
                CopyCell(idle, 0, 0, 0, idle.Width, idle.Height);
                for (var index = 0; index < sprite.Columns * sprite.Rows; index++)
                    CopyCell(source, index + 1, index % sprite.Columns * sprite.FrameWidth, index / sprite.Columns * sprite.FrameHeight, sprite.FrameWidth, sprite.FrameHeight);
                foreach (var key in definitions.Keys.ToArray())
                    if (definitions[key].Gif is null) definitions[key] = definitions[key] with { Frames = definitions[key].Frames!.Select(index => index + 1).ToArray() };
                for (var index = 0; index < staticClips.Count; index++)
                {
                    var (key, clip) = staticClips[index]; var image = clip.Thumbnail; var cell = 1 + sprite.Columns * sprite.Rows + index;
                    CopyCell(image, cell, 0, 0, image.Width, image.Height);
                    definitions[key] = new([cell], 1, Loop: Playback(key) != 0, PingPong: Playback(key) == 2);
                }
                var file = $"edited-{Guid.NewGuid():N}.png";
                AtomicFile.Write(Path.Combine(directory, file), ImageCodec.EncodePng(sheet)); sprite = new(file, columns, rows, width, height);
            }
            var updated = manifest with { Name = name, SpriteSheet = sprite, Animations = definitions };
            AtomicFile.Write(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(updated, CharacterLibrary.JsonOptions));
            var archive = Path.Combine(root, "edited.unfoldpet"); CharacterPack.Create(directory, contentVersion.ToString(3), archive);
            AtomicFile.Write(outputPath, ImageCodec.ReadBounded(archive, CharacterPack.MaxArchiveBytes));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
