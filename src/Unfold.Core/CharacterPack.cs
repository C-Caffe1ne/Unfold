using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Unfold.Core;

public sealed record CharacterPackMetadata(int FormatVersion, string Id, string ContentVersion, Dictionary<string, string> Files);
public sealed record CharacterPackInstallInfo(string Action, string? InstalledVersion, string? Revision);

/// <summary>A validated, private snapshot. Opening an archive does not install it.</summary>
public sealed class CharacterPack : IDisposable
{
    public const int MaxArchiveBytes = 64 * 1024 * 1024;
    public const int MaxFiles = 32;
    private const int CopyBufferBytes = 16 * 1024;
    private readonly string temporaryRoot;
    private readonly CharacterPackMetadata metadata;
    private readonly byte[] metadataBytes;
    private bool disposed;
    public CharacterPackage Character { get; }
    public string Id => metadata.Id;
    public string ContentVersion => metadata.ContentVersion;
    public CharacterAudit Audit { get; }
    private CharacterPack(string root, CharacterPackMetadata metadata, byte[] metadataBytes, CharacterAudit audit)
    {
        temporaryRoot = root; this.metadata = metadata; this.metadataBytes = metadataBytes;
        Character = CharacterLibrary.LoadPackage(Path.Combine(root, metadata.Id)); Audit = audit;
    }
    public static CharacterPack Open(string archivePath)
    {
        using var source = OpenBounded(archivePath, MaxArchiveBytes);
        return OpenSnapshot(source);
    }
    private static CharacterPack OpenSnapshot(FileStream source)
    {
        var archiveLength = source.Length;
        byte[] metadataBytes; CharacterPackMetadata metadata; string directory;
        var root = Path.Combine(Path.GetTempPath(), $"Unfold-pack-{Guid.NewGuid():N}");
        try
        {
            using (var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true))
            {
                if (archive.Entries.Count is < 3 or > MaxFiles) throw new InvalidDataException("A pet pack needs 3–32 files.");
                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase); long total = 0;
                foreach (var entry in archive.Entries)
                {
                    ValidateFileName(entry.FullName);
                    // Reject Unix links/devices and Windows reparse points before extraction.
                    var kind = (entry.ExternalAttributes >> 16) & 0xF000;
                    if ((kind != 0 && kind != 0x8000) || (entry.ExternalAttributes & 0x410) != 0)
                        throw new InvalidDataException("Only regular files are allowed in a pet pack.");
                    if (entry.Length <= 0 || entry.Length > ImageCodec.MaxFileBytes || (total += entry.Length) > MaxArchiveBytes)
                        throw new InvalidDataException("Pet pack exceeds its file or total size limit.");
                    if (!entries.TryAdd(entry.FullName, entry)) throw new InvalidDataException("Duplicate or invalid archive entry.");
                }
                if (!entries.TryGetValue("pack.json", out var metadataEntry)) throw new InvalidDataException("Missing pack.json.");
                if (metadataEntry.Length > 64 * 1024) throw new InvalidDataException("Pack metadata exceeds 64 KiB.");
                metadataBytes = new byte[checked((int)metadataEntry.Length)];
                using (var input = metadataEntry.Open())
                {
                    input.ReadExactly(metadataBytes);
                    if (input.ReadByte() != -1) throw new InvalidDataException("Invalid metadata entry length.");
                }
                metadata = ReadMetadata(metadataBytes);
                if (entries.Count != metadata.Files.Count + 1) throw new InvalidDataException("Pack inventory differs from its contents.");
                directory = Path.Combine(root, metadata.Id); Directory.CreateDirectory(directory);
                foreach (var (name, hash) in metadata.Files)
                {
                    if (!entries.TryGetValue(name, out var entry)) throw new InvalidDataException($"File checksum mismatch: {name}");
                    using var input = entry.Open();
                    using var output = new FileStream(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    if (CopyExactAndHash(input, output, entry.Length, ImageCodec.MaxFileBytes) != hash)
                        throw new InvalidDataException($"File checksum mismatch: {name}");
                    output.Flush(true);
                }
                AtomicFile.Write(Path.Combine(directory, "pack.json"), metadataBytes);
            }
            if (source.Length != archiveLength) throw new InvalidDataException("The archive changed while being opened.");
            var audit = ValidatePayload(directory, metadata);
            return new(root, metadata, metadataBytes, audit);
        }
        catch { if (Directory.Exists(root)) Directory.Delete(root, true); throw; }
    }
    public static void Create(string characterDirectory, string contentVersion, string outputPath)
    {
        _ = ParseVersion(contentVersion);
        var audit = CharacterAssetAudit.InspectPackage(characterDirectory);
        if (audit.Errors.Count > 0) throw new InvalidDataException(string.Join("\n", audit.Errors));
        if (audit.Files.Count >= MaxFiles || audit.Files.Sum(file => file.Bytes) > MaxArchiveBytes - 64 * 1024)
            throw new InvalidDataException("Pet pack exceeds its file or total size limit.");
        var temporary = Path.Combine(Path.GetTempPath(), $"Unfold-pack-{Guid.NewGuid():N}.unfoldpet");
        var outputTemporary = Path.GetFullPath(outputPath) + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var hashes = new Dictionary<string, string>(StringComparer.Ordinal); long total = 0;
                    foreach (var asset in audit.Files)
                    {
                        ValidateFileName(asset.Path);
                        using var input = OpenBounded(CharacterLibrary.AssetPath(characterDirectory, asset.Path), ImageCodec.MaxFileBytes);
                        if ((total += input.Length) > MaxArchiveBytes - 64 * 1024)
                            throw new InvalidDataException("Pet pack exceeds its file or total size limit.");
                        using var output = archive.CreateEntry(asset.Path, CompressionLevel.Optimal).Open();
                        hashes.Add(asset.Path, CopyExactAndHash(input, output, input.Length, ImageCodec.MaxFileBytes));
                    }
                    var metadata = new CharacterPackMetadata(1, audit.Id, contentVersion, hashes);
                    var json = JsonSerializer.SerializeToUtf8Bytes(metadata, CharacterLibrary.JsonOptions); _ = ReadMetadata(json);
                    using var metadataOutput = archive.CreateEntry("pack.json", CompressionLevel.Optimal).Open(); metadataOutput.Write(json);
                }
                file.Flush(true);
            }
            // Validate and publish the same read-locked distribution file, never a second source read.
            using var snapshot = OpenBounded(temporary, MaxArchiveBytes); using var verified = OpenSnapshot(snapshot);
            using (var output = new FileStream(outputTemporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { snapshot.Position = 0; _ = CopyExactAndHash(snapshot, output, snapshot.Length, MaxArchiveBytes); output.Flush(true); }
            File.Move(outputTemporary, outputPath);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (File.Exists(outputTemporary)) File.Delete(outputTemporary);
        }
    }
    internal CharacterPackage CopyTo(string directory)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Directory.CreateDirectory(directory);
        foreach (var (name, hash) in metadata.Files)
        {
            var target = Path.Combine(directory, name); var temporary = target + $".{Guid.NewGuid():N}.tmp";
            try
            {
                using (var input = OpenBounded(CharacterLibrary.AssetPath(Character.DirectoryPath, name), ImageCodec.MaxFileBytes))
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (CopyExactAndHash(input, output, input.Length, ImageCodec.MaxFileBytes) != hash)
                        throw new InvalidDataException("The preview files changed. Reopen the original pack.");
                    output.Flush(true);
                }
                File.Move(temporary, target, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        AtomicFile.Write(Path.Combine(directory, "pack.json"), metadataBytes);
        _ = ValidatePayload(directory, metadata);
        return CharacterLibrary.LoadPackage(directory);
    }
    private static FileStream OpenBounded(string path, int maxBytes)
    {
        var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferBytes, FileOptions.SequentialScan);
        if (input.Length <= maxBytes) return input;
        input.Dispose(); throw new InvalidDataException("File exceeds the size limit.");
    }
    private static string CopyExactAndHash(Stream input, Stream output, long length, int maxBytes)
    {
        if (length < 0 || length > maxBytes) throw new InvalidDataException("File exceeds the size limit.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buffer = stackalloc byte[CopyBufferBytes]; var remaining = length;
        while (remaining > 0)
        {
            var read = input.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
            if (read == 0) throw new EndOfStreamException("The payload is shorter than its declared length.");
            output.Write(buffer[..read]); hash.AppendData(buffer[..read]); remaining -= read;
        }
        if (input.ReadByte() != -1) throw new InvalidDataException("The payload exceeds its declared length.");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    private static CharacterAudit ValidatePayload(string directory, CharacterPackMetadata metadata)
    {
        var package = CharacterLibrary.LoadPackage(directory);
        if (package.Manifest.Animations.Count > 16 || package.Manifest.Animations.Values.Where(animation => animation.Gif is null && animation.ModelClip is null)
            .Sum(animation => (long)package.Manifest.SpriteSheet.FrameWidth * package.Manifest.SpriteSheet.FrameHeight * animation.Frames!.Length * 4) > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException("Pet pack exceeds 16 clips or its decoded sprite budget.");
        var audit = CharacterAssetAudit.InspectPackage(directory);
        if (audit.Id != metadata.Id || audit.Errors.Count > 0) throw new InvalidDataException(string.Join("\n", audit.Errors.Prepend("Invalid pet pack assets.")));
        if (!audit.Files.Select(file => file.Path).ToHashSet(StringComparer.Ordinal).SetEquals(metadata.Files.Keys))
            throw new InvalidDataException("The inventory must contain exactly the runtime manifest and its referenced images.");
        if (audit.Clips.Sum(clip => clip.DecodedBytes) > ImageCodec.MaxDecodedAnimationBytes)
            throw new InvalidDataException("Total decoded pet clips exceed 128 MiB.");
        return audit;
    }
    internal static CharacterPackMetadata ReadMetadata(byte[] bytes)
    {
        if (bytes.Length > 64 * 1024) throw new InvalidDataException("Pack metadata exceeds 64 KiB.");
        var value = JsonSerializer.Deserialize<CharacterPackMetadata>(bytes, CharacterLibrary.JsonOptions)
            ?? throw new InvalidDataException("Missing pack metadata.");
        if (value.FormatVersion != 1 || !CharacterLibrary.SafeId(value.Id) || value.Files is null || value.Files.Count is < 2 or >= MaxFiles)
            throw new InvalidDataException("Invalid pack metadata.");
        ValidateFileName(value.Id); _ = ParseVersion(value.ContentVersion);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pack.json" };
        foreach (var (name, hash) in value.Files)
        {
            ValidateFileName(name);
            if (!names.Add(name) || hash is not { Length: 64 } || hash.Any(c => !char.IsAsciiHexDigit(c)) || hash != hash.ToLowerInvariant())
                throw new InvalidDataException("Invalid file inventory or SHA-256.");
        }
        if (!value.Files.ContainsKey("character.json")) throw new InvalidDataException("Missing character.json inventory entry.");
        return value;
    }
    internal static Version ParseVersion(string value)
    {
        if (value is null || !Version.TryParse(value, out var version) || version.Build < 0 || version.Revision >= 0 || version.ToString(3) != value)
            throw new InvalidDataException("Use a content version such as 1.0.0.");
        return version;
    }
    internal static void ValidateFileName(string name)
    {
        if (name is not { Length: > 0 and <= 128 } || !char.IsAsciiLetterOrDigit(name[0]) || name.EndsWith('.') ||
            name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_'))
            throw new InvalidDataException("Pet packs use flat ASCII file names without folders.");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9'))
            throw new InvalidDataException("Reserved file name.");
    }
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal bool MatchesInventory(CharacterPackMetadata other) => metadata.Files.Count == other.Files.Count &&
        metadata.Files.All(file => other.Files.TryGetValue(file.Key, out var hash) && hash == file.Value);
    public void Dispose()
    {
        if (disposed) return;
        if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        disposed = true;
    }
}

public sealed partial class CharacterLibrary
{
    public CharacterPackInstallInfo InspectInstall(CharacterPack pack)
    { using var lease = Lock(); Recover(); return InspectInstallLocked(pack); }
    private CharacterPackInstallInfo InspectInstallLocked(CharacterPack pack)
    {
        if (protectedIds.Contains(pack.Id)) throw new InvalidDataException("This ID belongs to a built-in companion.");
        var collision = Directory.EnumerateDirectories(Root).FirstOrDefault(path => string.Equals(Path.GetFileName(path), pack.Id, StringComparison.OrdinalIgnoreCase));
        if (collision is null) return new("Install", null, null);
        if (Path.GetFileName(collision) != pack.Id) throw new InvalidDataException("A companion already uses this ID with different casing.");
        var metaPath = AssetPath(collision, "pack.json");
        if (!File.Exists(metaPath) || File.Exists(AssetPath(collision, "source.piskel")))
            throw new InvalidDataException("This ID belongs to existing artwork. It cannot be replaced by a pet pack.");
        var metadata = CharacterPack.ReadMetadata(ImageCodec.ReadBounded(metaPath, 64 * 1024));
        if (metadata.Id != pack.Id) throw new InvalidDataException("Installed pack ID differs from its folder.");
        var difference = CharacterPack.ParseVersion(pack.ContentVersion).CompareTo(CharacterPack.ParseVersion(metadata.ContentVersion));
        // Content versions describe the selected archive; they are not an app
        // compatibility gate. The preview revision still protects replacements.
        var action = difference == 0 && pack.MatchesInventory(metadata) ? "Reinstall" : difference > 0 ? "Update" : "Replace";
        return new(action, metadata.ContentVersion, PackRevision(collision));
    }
    private static string PackRevision(string directory)
    {
        RejectLink(directory);
        if (Directory.EnumerateDirectories(directory).Any()) throw new InvalidDataException("Unexpected folders in installed pack.");
        var files = Directory.GetFiles(directory).Order(StringComparer.Ordinal).ToArray();
        if (files.Length > CharacterPack.MaxFiles) throw new InvalidDataException("Unexpected files in installed pack.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); long total = 0;
        foreach (var path in files)
        {
            var name = Path.GetFileName(path); CharacterPack.ValidateFileName(name);
            var bytes = ImageCodec.ReadBounded(AssetPath(directory, name)); total += bytes.Length;
            if (total > CharacterPack.MaxArchiveBytes) throw new InvalidDataException("Installed pack exceeds size limit.");
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(name + "\0")); hash.AppendData(SHA256.HashData(bytes));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public CharacterPackage Install(CharacterPack pack, string? expectedRevision)
    {
        using var lease = Lock(); Recover();
        if (InspectInstallLocked(pack).Revision != expectedRevision) throw new IOException("The installed companion changed. Reopen the pack preview.");
        var stage = Path.Combine(Root, $".stage-{Guid.NewGuid():N}");
        var payload = Path.Combine(stage, pack.Id); var target = PackagePath(pack.Id); var backup = Path.Combine(Root, $".backup-{pack.Id}");
        try
        {
            var validated = pack.CopyTo(payload);
            if (InspectInstallLocked(pack).Revision != expectedRevision) throw new IOException("The installed companion changed during validation.");
            if (Directory.Exists(target)) Directory.Move(target, backup);
            try { Directory.Move(payload, target); }
            catch { if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target); throw; }
            try { if (Directory.Exists(backup)) Directory.Delete(backup, true); }
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
