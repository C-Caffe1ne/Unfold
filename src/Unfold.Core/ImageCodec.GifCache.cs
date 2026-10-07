using System.Security.Cryptography;

namespace Unfold.Core;

public static partial class ImageCodec
{
    private static readonly object gifGate = new();
    private static readonly Dictionary<string, WeakReference<IReadOnlyList<AnimationFrame>>> gifs = [];

    private static IReadOnlyList<AnimationFrame> SharedGifFrames(byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        lock (gifGate)
        {
            // Playback reads immutable source pixels. The pet, home and draft can
            // share identical media without a cache owning another decoded clip.
            if (gifs.TryGetValue(hash, out var weak) && weak.TryGetTarget(out var active)) return active;
            var frames = DecodeGifFrames(bytes);
            foreach (var expired in gifs.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray()) gifs.Remove(expired);
            if (gifs.Count >= 32) gifs.Remove(gifs.Keys.First());
            gifs[hash] = new(frames);
            return frames;
        }
    }
}
