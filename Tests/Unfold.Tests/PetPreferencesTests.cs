using System.Text.Json;
using System.Text.Json.Nodes;
using Unfold.Core;

namespace Unfold.Tests;

public class PetPreferencesTests
{
    [Fact]
    public void LegacySettingsKeepTheirGlobalSizeUntilAPetIsSelected()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        const string original = """{"selectedCharacterId":"cat","petScalePercent":130,"intervalMinutes":37}""";
        File.WriteAllText(path, original);

        var legacy = AppSettings.Load(path, out var needsBackup, out var needsMigration);

        Assert.False(needsBackup); Assert.False(needsMigration);
        Assert.Empty(legacy.PetPreferences);
        Assert.Equal(130, legacy.GetPetScalePercent("cat"));
        Assert.Equal(130, legacy.GetPetScalePercent("dog"));
        Assert.Equal(original, File.ReadAllText(path));
        var selected = legacy.WithSelectedCharacter("dog");
        Assert.Equal(130, selected.PetScalePercent);
        Assert.Equal(130, selected.PetPreferences["cat"].ScalePercent);
        Assert.Equal(130, selected.PetPreferences["dog"].ScalePercent);
        Assert.Equal(37, selected.IntervalMinutes);
        selected.Save(path);
        var loaded = AppSettings.Load(path);
        Assert.Equal(130, loaded.WithSelectedCharacter("cat").PetScalePercent);
    }

    [Fact]
    public void SwitchingAndSavingTwoPetsKeepsTheirSizesAndFavoritesSeparate()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { SelectedCharacterId = "cat" }
            .WithPetScale("cat", 70).WithPetFavorite("cat", true)
            .WithSelectedCharacter("dog").WithPetScale("dog", 140);
        settings.Save(path);

        var loaded = AppSettings.Load(path, out var needsBackup);

        Assert.False(needsBackup);
        Assert.Equal(settings, loaded);
        Assert.Equal(settings.GetHashCode(), loaded.GetHashCode());
        Assert.Equal("dog", loaded.SelectedCharacterId);
        Assert.Equal(140, loaded.PetScalePercent);
        Assert.Equal(70, loaded.GetPetScalePercent("cat"));
        Assert.True(loaded.IsPetFavorite("cat"));
        Assert.False(loaded.IsPetFavorite("dog"));
        var cat = loaded.WithSelectedCharacter("cat") with { ShowPetTimer = false };
        cat.Save(path);
        var afterUnrelatedSave = AppSettings.Load(path);
        Assert.Equal(70, afterUnrelatedSave.PetScalePercent);
        Assert.Equal(140, afterUnrelatedSave.WithSelectedCharacter("dog").PetScalePercent);
        Assert.False(afterUnrelatedSave.ShowPetTimer);
        Assert.Equal(JsonSerializer.Serialize(cat, CharacterLibrary.JsonOptions),
            JsonSerializer.Serialize(afterUnrelatedSave, CharacterLibrary.JsonOptions));
    }

    [Fact]
    public void PreferenceSnapshotsCopyMutableInputsAndCompareRegardlessOfInsertionOrder()
    {
        var mutable = new Dictionary<string, PetPreference>
        {
            ["cat"] = new() { ScalePercent = 80 },
            ["dog"] = new() { Favorite = true }
        };
        var first = new AppSettings { PetPreferences = mutable };
        var second = new AppSettings().WithPetFavorite("dog", true).WithPetScale("cat", 80);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        mutable["cat"] = new() { ScalePercent = 140 };
        Assert.Equal(80, first.GetPetScalePercent("cat"));
        Assert.Equal(first, second);
    }

    [Fact]
    public void ANewFavoriteDoesNotLockItsSizeBeforeFirstSelection()
    {
        var initial = new AppSettings { SelectedCharacterId = "cat", PetScalePercent = 80 };
        var favorite = initial.WithPetFavorite("dog", true);
        Assert.Null(favorite.PetPreferences["dog"].ScalePercent);
        Assert.Empty(initial.PetPreferences);

        var selected = favorite.WithPetScale("cat", 120).WithSelectedCharacter("dog");

        Assert.Equal(120, selected.PetScalePercent);
        Assert.True(selected.IsPetFavorite("dog"));
        Assert.Equal(120, selected.PetPreferences["dog"].ScalePercent);
        Assert.Null(favorite.PetPreferences["dog"].ScalePercent);
        Assert.Empty(favorite.WithPetFavorite("dog", false).PetPreferences);
    }

    [Fact]
    public void SettingAnInactivePetsSizeDoesNotResizeTheSelectedPet()
    {
        var settings = new AppSettings { SelectedCharacterId = "cat", PetScalePercent = 90 }
            .WithPetFavorite("dog", true).WithPetScale("dog", 150);

        Assert.Equal(90, settings.PetScalePercent);
        Assert.Equal(150, settings.GetPetScalePercent("dog"));
        Assert.True(settings.IsPetFavorite("dog"));
        Assert.Equal(150, settings.WithSelectedCharacter("dog").PetScalePercent);
        Assert.Equal(90, settings.WithSelectedCharacter("dog").WithSelectedCharacter("cat").PetScalePercent);
    }

    [Fact]
    public void LegacyGlobalSizeChangesRemainAuthoritativeForTheSelectedPet()
    {
        var settings = new AppSettings { SelectedCharacterId = "cat" }.WithPetScale("cat", 70);
        settings = settings with { PetScalePercent = 110 };

        Assert.Equal(110, settings.GetPetScalePercent("cat"));
        var switched = settings.WithSelectedCharacter("dog");
        Assert.Equal(110, switched.PetPreferences["cat"].ScalePercent);
        Assert.Equal(110, switched.WithSelectedCharacter("cat").PetScalePercent);
    }

    [Fact]
    public void DeletingSelectedAndInactivePreferencesDoesNotRecreateThemOnSave()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { SelectedCharacterId = "cat" }
            .WithPetScale("cat", 60).WithPetFavorite("cat", true)
            .WithPetScale("dog", 140).WithPetFavorite("rabbit", true);

        var removed = settings.WithSelectedCharacter("dog")
            .WithoutPetPreference("cat").WithoutPetPreference("rabbit");
        removed.Save(path);
        var loaded = AppSettings.Load(path);

        Assert.Equal("dog", loaded.SelectedCharacterId);
        Assert.Equal(140, loaded.PetScalePercent);
        Assert.Single(loaded.PetPreferences);
        Assert.False(loaded.IsPetFavorite("cat"));
        Assert.False(loaded.PetPreferences.ContainsKey("rabbit"));
        Assert.Equal(140, loaded.WithSelectedCharacter("cat").PetScalePercent);
        Assert.Same(loaded, loaded.WithoutPetPreference("missing"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("123")]
    public void ACorruptPreferencesContainerKeepsUnrelatedSettings(string invalid)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var original = $$"""{"petPreferences":{{invalid}},"petScalePercent":120,"intervalMinutes":37}""";
        File.WriteAllText(path, original);

        var loaded = AppSettings.Load(path, out var needsBackup);

        Assert.True(needsBackup);
        Assert.Empty(loaded.PetPreferences);
        Assert.Equal(120, loaded.PetScalePercent);
        Assert.Equal(37, loaded.IntervalMinutes);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Theory]
    [InlineData("scalePercent", "\"broken\"", null, true)]
    [InlineData("scalePercent", "55", null, true)]
    [InlineData("scalePercent", "160", null, true)]
    [InlineData("scalePercent", "1e999", null, true)]
    [InlineData("scalePercent", "{}", null, true)]
    [InlineData("favorite", "\"true\"", 80, false)]
    [InlineData("favorite", "null", 80, false)]
    [InlineData("favorite", "[]", 80, false)]
    public void ACorruptPreferenceFieldKeepsTheOtherFieldAndOtherPets(
        string field, string invalid, int? expectedScale, bool expectedFavorite)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var cat = new JsonObject { ["scalePercent"] = 80, ["favorite"] = true, [field] = JsonNode.Parse(invalid) };
        var original = new JsonObject
        {
            ["petScalePercent"] = 120,
            ["petPreferences"] = new JsonObject
            {
                ["cat"] = cat,
                ["dog"] = new JsonObject { ["scalePercent"] = 150, ["favorite"] = true },
                ["../escape"] = new JsonObject { ["favorite"] = true },
                ["broken-entry"] = "broken",
                ["null-entry"] = null
            }
        }.ToJsonString();
        File.WriteAllText(path, original);

        var loaded = AppSettings.Load(path, out var needsBackup);

        Assert.True(needsBackup);
        Assert.Equal(2, loaded.PetPreferences.Count);
        Assert.Equal(expectedScale, loaded.PetPreferences["cat"].ScalePercent);
        Assert.Equal(expectedFavorite, loaded.IsPetFavorite("cat"));
        Assert.Equal(150, loaded.GetPetScalePercent("dog"));
        Assert.True(loaded.IsPetFavorite("dog"));
        Assert.Equal(original, File.ReadAllText(path));
        loaded.Save(path);
        _ = AppSettings.Load(path, out needsBackup);
        Assert.False(needsBackup);
    }

    [Fact]
    public void PreferenceFieldNamesAreCaseInsensitiveButPetIdsStayDistinct()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(path, """
            {"PETPREFERENCES":{"Cat":{"SCALEPERCENT":"50","FAVORITE":true},"cat":{"scalePercent":150}}}
            """);

        var loaded = AppSettings.Load(path, out var needsBackup);

        Assert.False(needsBackup);
        Assert.Equal(50, loaded.GetPetScalePercent("Cat"));
        Assert.Equal(150, loaded.GetPetScalePercent("cat"));
        Assert.True(loaded.IsPetFavorite("Cat"));
        Assert.False(loaded.IsPetFavorite("cat"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData("pet/id")]
    public void HelpersRejectUnsafeIdsWithoutChangingExistingPreferences(string id)
    {
        var settings = new AppSettings().WithPetFavorite("cat", true);
        Assert.Throws<InvalidDataException>(() => settings.WithSelectedCharacter(id));
        Assert.Throws<InvalidDataException>(() => settings.WithPetScale(id, 100));
        Assert.Throws<InvalidDataException>(() => settings.WithPetFavorite(id, true));
        Assert.Throws<InvalidDataException>(() => settings.WithoutPetPreference(id));
        Assert.Single(settings.PetPreferences);
        Assert.True(settings.IsPetFavorite("cat"));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(55)]
    [InlineData(160)]
    public void InvalidPreferencesCannotOverwriteTheLastSavedSettings(int invalidScale)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings().WithPetFavorite("cat", true);
        settings.Save(path);
        var original = File.ReadAllText(path);
        var invalid = settings with
        {
            PetPreferences = new Dictionary<string, PetPreference>
            {
                ["cat"] = new() { ScalePercent = invalidScale, Favorite = true }
            }
        };

        Assert.Throws<InvalidDataException>(() => invalid.Save(path));
        Assert.Throws<InvalidDataException>(() => settings.WithPetScale("cat", invalidScale));
        Assert.Equal(original, File.ReadAllText(path));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public void InvalidPreferenceIdsAndNullValuesCannotOverwriteSavedSettings()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings().WithPetFavorite("cat", true);
        settings.Save(path);
        var original = File.ReadAllText(path);
        IReadOnlyDictionary<string, PetPreference>?[] invalidPreferences =
        [
            null,
            new Dictionary<string, PetPreference> { ["../escape"] = new() },
            new Dictionary<string, PetPreference> { ["cat"] = null! }
        ];
        foreach (var preferences in invalidPreferences)
        {
            Assert.Throws<InvalidDataException>(() => (settings with { PetPreferences = preferences! }).Save(path));
            Assert.Equal(original, File.ReadAllText(path));
        }
    }

    [Fact]
    public void FailedAtomicReplacementLeavesNoTemporaryFileOrChangedPreferences()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "settings.json");
        Directory.CreateDirectory(path);
        var marker = Path.Combine(path, "existing.txt");
        File.WriteAllText(marker, "preserved");
        var original = new AppSettings().WithPetScale("default-cat", 70);
        var candidate = original.WithPetScale("default-cat", 140);

        var error = Record.Exception(() => candidate.Save(path));

        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.Equal("preserved", File.ReadAllText(marker));
        Assert.Empty(Directory.EnumerateFiles(temp.Path, "*.tmp"));
        Assert.Equal(70, original.PetScalePercent);
        Assert.Equal(70, original.PetPreferences["default-cat"].ScalePercent);
    }
}
