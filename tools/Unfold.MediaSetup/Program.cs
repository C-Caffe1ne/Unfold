using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

if (args.Length != 2) throw new ArgumentException("Usage: Unfold.MediaSetup <osx-arm64|osx-x64|win-x64|win-arm64> <repository-root>");
var rid = args[0];
// Windows on Arm uses the x64 command-line tool through OS emulation.
var (platform, hash) = rid switch
{
    "osx-arm64" => ("aarch64-apple-darwin", "ac0babf65798bf681da1dc81c4fe4ae049d0d26e161410174b0e120e3e783332"),
    "osx-x64" => ("x86_64-apple-darwin", "51ac761dde58a600bb685a5d2510ae38d6ade1f62ef2ef556263c67cb69f2b00"),
    "win-x64" or "win-arm64" => ("x86_64-pc-windows-msvc", "fb2de01912edb449a5eba1cc487696c7f71ebb7b325e5c5c69434642ec2ba6b0"),
    _ => throw new ArgumentException("Unsupported runtime.")
};
var repository = Path.GetFullPath(args[1]);
if (!File.Exists(Path.Combine(repository, "Unfold.slnx"))) throw new ArgumentException("Expected the Unfold repository root.");
var cache = Path.Combine(repository, ".tools", "media-downloads");
var output = Path.Combine(repository, ".tools", "media-lgpl", rid);
Directory.CreateDirectory(cache); Directory.CreateDirectory(output);
const string origin = "https://github.com/serversideup/ffmpeg-lgpl-builds/releases/download/v8.1.2-27/";
var asset = "ffmpeg-8.1.2-" + platform + ".tar.gz";
var path = Path.Combine(cache, asset);
byte[] bytes;
if (File.Exists(path)) bytes = await File.ReadAllBytesAsync(path);
else
{
    Console.WriteLine("Downloading " + asset);
    using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    bytes = await client.GetByteArrayAsync(origin + asset);
}
if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != hash)
    throw new InvalidDataException("SHA-256 mismatch: " + asset);
await File.WriteAllBytesAsync(path, bytes);
using var compressed = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
using var archive = new TarReader(compressed);
var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
while (archive.GetNextEntry() is { } entry)
{
    if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
        throw new InvalidDataException("Unexpected media tool archive entry.");
    var name = entry.Name;
    if (name != Path.GetFileName(name) || name.Contains('\\') || name is "." or ".." || !written.Add(name) || entry.Length > 128 * 1024 * 1024)
        throw new InvalidDataException("Invalid media tool file.");
    if (name is "ffprobe" or "ffprobe.exe") continue;
    var target = Path.Combine(output, name); var temporary = target + ".tmp";
    try
    {
        using (var file = File.Create(temporary)) await entry.DataStream.CopyToAsync(file);
        File.Move(temporary, target, true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    if (name == "ffmpeg" && !OperatingSystem.IsWindows()) File.SetUnixFileMode(target,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
}
if (!written.Contains(rid.StartsWith("win-") ? "ffmpeg.exe" : "ffmpeg") || !written.Contains("COPYING.LGPLv2.1") || !written.Contains("SOURCE.txt"))
    throw new InvalidDataException("Media tool or its license/source notice is missing.");
Console.WriteLine("Media tools ready: " + output);
