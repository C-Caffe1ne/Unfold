using System.Security.Cryptography;

namespace Unfold.Core;

public sealed partial class GlbModel
{
    private static readonly object cacheGate = new();
    private static readonly Dictionary<string, WeakReference<GlbModel>> models = [];

    // Content identity, not a filename or timestamp: pack replacement, renamed
    // copies and edits with preserved file dates all resolve to the right model.
    // Weak entries never keep an unused model alive, and keys are bounded too.
    internal static GlbModel FromSnapshot(byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        lock (cacheGate)
        {
            if (models.TryGetValue(hash, out var weak) && weak.TryGetTarget(out var shared)) return shared;
            var model = Parse(bytes);
            foreach (var key in models.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) models.Remove(key);
            if (models.Count >= 64) models.Remove(models.Keys.First());
            models[hash] = new(model);
            return model;
        }
    }
}
