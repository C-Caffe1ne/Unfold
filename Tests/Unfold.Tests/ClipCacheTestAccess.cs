using System.Collections;
using System.Reflection;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

/// <summary>Injects delayed reads through the cache's test-only reflection seam.</summary>
internal sealed class ClipCacheTestAccess
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly object cache, gate;
    private readonly IDictionary entries, activeFrames;
    private readonly Type entryType;
    private readonly FieldInfo retainedBytes, sequence;

    public ClipCacheTestAccess(AppRuntime runtime)
    {
        cache = typeof(AppRuntime).GetField("clips", Private)!.GetValue(runtime)!;
        var type = cache.GetType();
        gate = type.GetField("gate", Private)!.GetValue(cache)!;
        entries = (IDictionary)type.GetField("entries", Private)!.GetValue(cache)!;
        activeFrames = (IDictionary)type.GetField("activeFrames", Private)!.GetValue(cache)!;
        entryType = type.GetNestedType("Entry", BindingFlags.NonPublic)!;
        retainedBytes = type.GetField("retainedBytes", Private)!;
        sequence = type.GetField("sequence", Private)!;
    }

    public Task<IReadOnlyList<AnimationFrame>> this[string key]
    {
        get => GetValueOrDefault(key) ?? throw new KeyNotFoundException(key);
        set
        {
            lock (gate)
            {
                Remove(key);
                var entry = Activator.CreateInstance(entryType, nonPublic: true)!;
                entryType.GetProperty("Task")!.SetValue(entry, value);
                var use = (long)sequence.GetValue(cache)! + 1;
                sequence.SetValue(cache, use);
                entryType.GetField("LastUse")!.SetValue(entry, use);
                entries[key] = entry;
            }
        }
    }

    public bool TryGetValue(string key, out Task<IReadOnlyList<AnimationFrame>> task)
    {
        lock (gate)
        {
            if (entries[key] is { } entry)
            {
                task = (Task<IReadOnlyList<AnimationFrame>>)entryType.GetProperty("Task")!.GetValue(entry)!;
                return true;
            }
            task = null!;
            return false;
        }
    }

    public Task<IReadOnlyList<AnimationFrame>>? GetValueOrDefault(string key) =>
        TryGetValue(key, out var task) ? task : null;

    public bool Remove(string key)
    {
        lock (gate)
        {
            var existed = entries[key] is { };
            if (entries[key] is { } entry && (bool)entryType.GetField("Ready")!.GetValue(entry)!)
                retainedBytes.SetValue(cache, (long)retainedBytes.GetValue(cache)! - (long)entryType.GetField("Bytes")!.GetValue(entry)!);
            entries.Remove(key);
            activeFrames.Remove(key);
            return existed;
        }
    }
}
