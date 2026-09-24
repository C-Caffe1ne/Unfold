namespace Unfold.Core;

/// <summary>An opt-in art/runtime contract, independent of custom-pet authoring and purchases.</summary>
public static class OriginalCompanion
{
    public const string Profile = "unfold-original-v1";
    public static IReadOnlyList<string> RequiredClips { get; } = Array.AsReadOnly(new[]
        { "idle", "attention", "stretch", "celebrate", "click", "sleep", "look", "yawn", "sulk", "walk" });
    public static IReadOnlyList<string> PointerClips { get; } = Array.AsReadOnly(new[] { "pickup", "held", "land" });
}

/// <summary>Only eligible idle time accrues; interruptions never queue a burst of behaviors.</summary>
public sealed class CompanionIdleSchedule(Random random)
{
    private static readonly string[] Actions = ["sleep", "look", "yawn"];
    private double remaining = random.Next(20, 41);
    private int previous = -1;
    public void Reset() => remaining = random.Next(20, 41);
    public string? Tick(double seconds, bool eligible)
    {
        if (!eligible || !double.IsFinite(seconds) || seconds <= 0 || seconds > .25) return null;
        remaining -= seconds;
        if (remaining > 0) return null;
        var candidates = Enumerable.Range(0, Actions.Length).Where(index => index != previous).ToArray();
        previous = candidates[random.Next(candidates.Length)]; Reset(); return Actions[previous];
    }
}
