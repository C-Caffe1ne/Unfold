namespace Unfold.Core;

public sealed partial class ReminderSounds
{
    // Players and settings use separate library instances but share the same managed files.
    private static readonly object fileGate = new();
    private static readonly Dictionary<string, int> playingFiles = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable AcquirePlayback(ReminderSound sound, string? id, bool strict, out string path)
    {
        lock (fileGate)
        {
            path = Path.GetFullPath(Resolve(sound, id, strict));
            playingFiles[path] = playingFiles.GetValueOrDefault(path) + 1;
            return new PlaybackLease(path);
        }
    }

    private sealed class PlaybackLease(string path) : IDisposable
    {
        private bool released;
        public void Dispose()
        {
            lock (fileGate)
            {
                if (released) return;
                released = true;
                if (--playingFiles[path] == 0) playingFiles.Remove(path);
            }
        }
    }

    /// <summary>Deletes only unreferenced, managed hash-named WAV copies, never source files or active playback.</summary>
    public void RemoveUnused(IEnumerable<string?> retainedIds, Action<Exception>? reportError = null)
    {
        var retained = retainedIds.Where(id => id is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (fileGate)
        {
            try
            {
                if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                foreach (var file in Directory.EnumerateFiles(directory, "*.wav", SearchOption.TopDirectoryOnly))
                {
                    var id = Path.GetFileNameWithoutExtension(file);
                    if (id.Length != 64 || !id.All(char.IsAsciiHexDigit) || retained.Contains(id) ||
                        playingFiles.ContainsKey(Path.GetFullPath(file))) continue;
                    try
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.Delete(file);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { reportError?.Invoke(error); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { reportError?.Invoke(error); }
        }
    }
}
