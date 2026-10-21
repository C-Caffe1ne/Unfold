namespace Unfold.Core;

/// <summary>Chooses which file of a multi-file action plays next.</summary>
public sealed class ClipVariantPicker(Random random)
{
    private readonly Dictionary<string, int> last = new(StringComparer.Ordinal);

    /// <summary><c>sequence</c> cycles in order; anything else picks at random without repeating the previous file.</summary>
    public int Next(string key, int count, string? order)
    {
        if (count <= 1) return 0;
        var previous = last.GetValueOrDefault(key, -1);
        int next;
        if (order == "sequence") next = (previous + 1) % count;
        else
        {
            next = random.Next(previous >= 0 ? count - 1 : count);
            if (previous >= 0 && next >= previous) next++;
        }
        last[key] = next;
        return next;
    }

    public void Clear() => last.Clear();
}
