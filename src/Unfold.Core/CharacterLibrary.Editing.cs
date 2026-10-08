namespace Unfold.Core;

public sealed partial class CharacterLibrary
{
    public bool CanManage(string id) => SafeId(id) && !protectedIds.Contains(id);
    public static string PetRevision(string directory) => PackRevision(directory);
    /// <summary>Replaces only the pet opened by the editor; legacy source artwork is retained.</summary>
    public CharacterPackage InstallEdited(CharacterPack pack, string expectedRevision)
    {
        using var lease = Lock(); Recover();
        if (!CanManage(pack.Id)) throw new InvalidDataException("기본 제공 펫은 편집할 수 없어요.");
        var target = PackagePath(pack.Id);
        void CheckRevision()
        {
            if (!Directory.Exists(target) || PetRevision(target) != expectedRevision)
                throw new IOException("펫이 변경되거나 삭제됐어요. 다시 선택한 뒤 편집해 주세요.");
        }
        CheckRevision();
        var stage = Path.Combine(Root, $".stage-{Guid.NewGuid():N}");
        var payload = Path.Combine(stage, pack.Id); var backup = Path.Combine(Root, $".backup-{pack.Id}");
        try
        {
            var validated = pack.CopyTo(payload);
            var source = AssetPath(target, "source.piskel");
            if (File.Exists(source)) AtomicFile.Write(Path.Combine(payload, "source.piskel"), ImageCodec.ReadBounded(source, PiskelCodec.MaxSourceBytes));
            CheckRevision(); Directory.Move(target, backup);
            try { Directory.Move(payload, target); }
            catch { if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target); throw; }
            try { Directory.Delete(backup, true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Warning?.Invoke($"Backup cleanup deferred: {error.Message}"); }
            return new(target, validated.Manifest, false, validated.StillImage);
        }
        finally
        {
            try { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Warning?.Invoke($"Staging cleanup deferred: {error.Message}"); }
        }
    }
}
