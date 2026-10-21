namespace Unfold.Core;

public sealed partial class CustomPetDraft
{
    // The imported clip each cleaned action started from, so settings can change without re-importing.
    private readonly Dictionary<string, (ImportedPetClip Source, ClipProcessing Settings)> processed = new(StringComparer.Ordinal);

    public ClipProcessing Processing(string action) => processed.TryGetValue(action, out var entry) ? entry.Settings : ClipProcessing.None;

    /// <summary>The unprocessed clip for <paramref name="action"/>: the cleanup source, else the current clip.</summary>
    public ImportedPetClip? ProcessingSource(string action) =>
        processed.TryGetValue(action, out var entry) ? entry.Source : clips.GetValueOrDefault(action);

    public void SetProcessedClip(string action, ImportedPetClip source, ClipProcessing settings, ImportedPetClip result)
    {
        PutClip(action, result);
        if (settings.IsActive) processed[action] = (source, settings);
        else processed.Remove(action);
    }
}
