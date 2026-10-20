using System.Collections.ObjectModel;
using System.Text.Json;

namespace Unfold.Core;

public sealed partial record AppSettings
{
    private static readonly IReadOnlyDictionary<string, PetPreference> EmptyPetPreferences =
        new PetPreferenceMap(new Dictionary<string, PetPreference>(StringComparer.Ordinal));

    private IReadOnlyDictionary<string, PetPreference> petPreferences = EmptyPetPreferences;
    public IReadOnlyDictionary<string, PetPreference> PetPreferences
    {
        get => petPreferences;
        init => petPreferences = value is null ? null! : value is PetPreferenceMap
            ? value : Freeze(new Dictionary<string, PetPreference>(value, StringComparer.Ordinal));
    }

    /// <summary>Remembers the current size before restoring the next pet's size.</summary>
    public AppSettings WithSelectedCharacter(string id)
    {
        ValidatePetId(id);
        ValidatePetId(SelectedCharacterId);
        ValidatePetScale(PetScalePercent);
        var preferences = CopyPetPreferences();
        preferences[SelectedCharacterId] = PreferenceFor(SelectedCharacterId) with { ScalePercent = PetScalePercent };
        var scale = id == SelectedCharacterId ? PetScalePercent : GetPetScalePercent(id);
        preferences[id] = PreferenceFor(id) with { ScalePercent = scale };
        return this with { SelectedCharacterId = id, PetScalePercent = scale, PetPreferences = Freeze(preferences) };
    }

    public AppSettings WithPetScale(string id, int percent)
    {
        ValidatePetId(id);
        ValidatePetScale(percent);
        var preferences = CopyPetPreferences();
        preferences[id] = PreferenceFor(id) with { ScalePercent = percent };
        return this with
        {
            PetScalePercent = id == SelectedCharacterId ? percent : PetScalePercent,
            PetPreferences = Freeze(preferences)
        };
    }

    public AppSettings WithPetFavorite(string id, bool favorite)
    {
        ValidatePetId(id);
        var preferences = CopyPetPreferences();
        var preference = PreferenceFor(id) with { Favorite = favorite };
        if (preference.ScalePercent is null && !favorite) preferences.Remove(id);
        else preferences[id] = preference;
        return this with { PetPreferences = Freeze(preferences) };
    }

    /// <summary>When deleting the selected pet, select its replacement before removing its preferences.</summary>
    public AppSettings WithoutPetPreference(string id)
    {
        ValidatePetId(id);
        if (!PetPreferences.ContainsKey(id)) return this;
        var preferences = CopyPetPreferences();
        preferences.Remove(id);
        return this with { PetPreferences = Freeze(preferences) };
    }

    public int GetPetScalePercent(string id)
    {
        ValidatePetId(id);
        // The global value remains the active size for existing callers and older settings files.
        return id == SelectedCharacterId ? PetScalePercent : PreferenceFor(id).ScalePercent ?? PetScalePercent;
    }

    public bool IsPetFavorite(string id)
    {
        ValidatePetId(id);
        return PreferenceFor(id).Favorite;
    }

    private PetPreference PreferenceFor(string id) => PetPreferences.TryGetValue(id, out var value) ? value : new();
    private Dictionary<string, PetPreference> CopyPetPreferences() => new(PetPreferences, StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, PetPreference> Freeze(Dictionary<string, PetPreference> preferences) =>
        preferences.Count == 0 ? EmptyPetPreferences : new PetPreferenceMap(preferences);

    // AppSettings is a value record: a save/load cycle must not change equality.
    private sealed class PetPreferenceMap(Dictionary<string, PetPreference> values)
        : ReadOnlyDictionary<string, PetPreference>(values)
    {
        public override bool Equals(object? obj) => ReferenceEquals(this, obj) ||
            obj is PetPreferenceMap other && Count == other.Count &&
            this.All(pair => other.TryGetValue(pair.Key, out var value) && pair.Value == value);
        public override int GetHashCode()
        {
            var hash = 0;
            foreach (var (id, preference) in this)
                hash ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(id), preference);
            return HashCode.Combine(Count, hash);
        }
    }

    private static bool ValidPetScale(int percent) => percent is >= 50 and <= 150 && percent % 10 == 0;
    private static void ValidatePetId(string id)
    {
        if (!CharacterLibrary.SafeId(id)) throw new InvalidDataException("Invalid character ID.");
    }
    private static void ValidatePetScale(int percent)
    {
        if (!ValidPetScale(percent)) throw new InvalidDataException("Invalid pet scale.");
    }
    private static void ValidatePetPreferences(IReadOnlyDictionary<string, PetPreference>? preferences)
    {
        if (preferences is null) throw new InvalidDataException("Missing pet preferences.");
        foreach (var (id, preference) in preferences)
        {
            ValidatePetId(id);
            if (preference is null) throw new InvalidDataException("Missing pet preference.");
            if (preference.ScalePercent is { } scale) ValidatePetScale(scale);
        }
    }

    private static IReadOnlyDictionary<string, PetPreference> ReadPetPreferences(JsonElement element, ref bool damaged)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            damaged = true;
            return EmptyPetPreferences;
        }
        var preferences = new Dictionary<string, PetPreference>(StringComparer.Ordinal);
        foreach (var entry in element.EnumerateObject())
        {
            if (!CharacterLibrary.SafeId(entry.Name) || entry.Value.ValueKind != JsonValueKind.Object)
            {
                damaged = true;
                continue;
            }
            int? scale = null;
            var favorite = false;
            foreach (var field in entry.Value.EnumerateObject())
            {
                if (field.Name.Equals(nameof(PetPreference.ScalePercent), StringComparison.OrdinalIgnoreCase))
                {
                    scale = null;
                    try
                    {
                        var parsed = field.Value.Deserialize<int?>(CharacterLibrary.JsonOptions);
                        if (parsed is null || ValidPetScale(parsed.Value)) scale = parsed;
                        else damaged = true;
                    }
                    catch (JsonException) { damaged = true; }
                }
                else if (field.Name.Equals(nameof(PetPreference.Favorite), StringComparison.OrdinalIgnoreCase))
                {
                    favorite = false;
                    try { favorite = field.Value.Deserialize<bool>(CharacterLibrary.JsonOptions); }
                    catch (JsonException) { damaged = true; }
                }
            }
            preferences[entry.Name] = new() { ScalePercent = scale, Favorite = favorite };
        }
        return Freeze(preferences);
    }
}
