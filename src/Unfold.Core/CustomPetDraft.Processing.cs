namespace Unfold.Core;

public sealed partial class CustomPetDraft
{
    // Cleanup settings are shared by every file of an action; each cleaned file remembers its imported original.
    private readonly Dictionary<string, ClipProcessing> processed = new(StringComparer.Ordinal);
    private readonly Dictionary<ImportedPetClip, ImportedPetClip> originals = new(ReferenceEqualityComparer.Instance);

    public ClipProcessing Processing(string action) => processed.GetValueOrDefault(action, ClipProcessing.None);

    /// <summary>The unprocessed clip of file <paramref name="index"/>: its cleanup source, else the file itself.</summary>
    public ImportedPetClip? ProcessingSource(string action, int index = 0) =>
        Files(action).ElementAtOrDefault(index) is { } file ? originals.GetValueOrDefault(file, file) : null;

    public void SetProcessedClip(string action, ImportedPetClip source, ClipProcessing settings, ImportedPetClip result, int index = 0)
    {
        var files = Files(action);
        if (index < 0 || index >= files.Count) throw new ArgumentOutOfRangeException(nameof(index));
        ReplaceFile(action, index, result);
        originals.Remove(files[index]);
        if (settings.IsActive)
        {
            if (!ReferenceEquals(result, source)) originals[result] = source;
            processed[action] = settings;
        }
        else processed.Remove(action);
    }

    /// <summary>A new first file starts without cleanup, so the action's other files return to their originals.</summary>
    private void ResetCleanup(string action)
    {
        processed.Remove(action);
        if (!extras.TryGetValue(action, out var more)) return;
        for (var index = 0; index < more.Count; index++)
            if (originals.Remove(more[index], out var original)) more[index] = original;
    }
}
