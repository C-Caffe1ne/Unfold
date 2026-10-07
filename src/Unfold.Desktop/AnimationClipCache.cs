using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Shares pending decodes and bounds the pixels retained after playback.</summary>
internal sealed class AnimationClipCache(long maxBytes = 32 * 1024 * 1024)
{
    private sealed class Entry
    {
        public Task<IReadOnlyList<AnimationFrame>> Task { get; set; } = null!;
        public long Bytes, LastUse;
        public bool Ready, Failed;
    }
    private readonly object gate = new();
    private readonly SemaphoreSlim decodeGate = new(1);
    private readonly Dictionary<string, Entry> entries = [];
    private readonly Dictionary<string, WeakReference<IReadOnlyList<AnimationFrame>>> activeFrames = [];
    private long retainedBytes, sequence;
    internal long RetainedBytes { get { lock (gate) return retainedBytes; } }

    public Task<IReadOnlyList<AnimationFrame>> Get(string key, Func<IReadOnlyList<AnimationFrame>> decode)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var cached))
            {
                if (!cached.Task.IsFaulted && !cached.Task.IsCanceled)
                {
                    cached.LastUse = ++sequence;
                    return cached.Task;
                }
                entries.Remove(key);
            }
            if (activeFrames.TryGetValue(key, out var weak) && weak.TryGetTarget(out var active))
                return Task.FromResult(active);
            var entry = new Entry { LastUse = ++sequence };
            // Decode one clip at a time so independent reactions cannot multiply peak memory.
            entry.Task = Task.Run(async () =>
            {
                await decodeGate.WaitAsync();
                try
                {
                    var frames = decode();
                    lock (gate)
                    {
                        if (entries.TryGetValue(key, out var current) && current == entry)
                        {
                            entry.Ready = true;
                            entry.Bytes = DecodedBytes(frames);
                            if (entry.Bytes > maxBytes)
                            {
                                // The pet and preview can share a large active clip without
                                // the cache keeping it alive after both views release it.
                                RememberActive(key, frames);
                                entries.Remove(key);
                            }
                            else
                            {
                                retainedBytes += entry.Bytes;
                                while (retainedBytes > maxBytes || entries.Values.Count(value => value.Ready) > 32)
                                {
                                    var oldest = entries.Where(pair => pair.Value.Ready).MinBy(pair => pair.Value.LastUse);
                                    if (oldest.Value == entry) RememberActive(oldest.Key, frames);
                                    else if (oldest.Value.Task.IsCompletedSuccessfully) RememberActive(oldest.Key, oldest.Value.Task.Result);
                                    retainedBytes -= oldest.Value.Bytes;
                                    entries.Remove(oldest.Key);
                                }
                            }
                        }
                    }
                    return frames;
                }
                catch
                {
                    lock (gate)
                        if (entries.TryGetValue(key, out var current) && current == entry)
                        {
                            // Keep the last failure until the next request, without retaining
                            // decoded pixels. Bound failure metadata as well as successful clips.
                            entry.Failed = true;
                            while (entries.Values.Count(value => value.Failed) > 32)
                                entries.Remove(entries.Where(pair => pair.Value.Failed).MinBy(pair => pair.Value.LastUse).Key);
                        }
                    throw;
                }
                finally { decodeGate.Release(); }
            });
            entries[key] = entry;
            return entry.Task;
        }
    }

    public void Clear()
    {
        lock (gate) { entries.Clear(); activeFrames.Clear(); retainedBytes = 0; }
    }

    private void RememberActive(string key, IReadOnlyList<AnimationFrame> frames)
    {
        foreach (var expired in activeFrames.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray()) activeFrames.Remove(expired);
        if (activeFrames.Count >= 32) activeFrames.Remove(activeFrames.Keys.First());
        activeFrames[key] = new(frames);
    }

    private static long DecodedBytes(IReadOnlyList<AnimationFrame> frames)
    {
        // GLB frames are rendered on demand; enumerating them would render the entire clip.
        if (frames is GlbAnimationFrames) return 0;
        var pixels = new HashSet<uint[]>(ReferenceEqualityComparer.Instance);
        return frames.Where(frame => pixels.Add(frame.Image.Pixels)).Sum(frame => (long)frame.Image.Pixels.Length * sizeof(uint));
    }
}
