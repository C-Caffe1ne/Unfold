using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class SettingsReliabilityTests
{
    [Fact]
    public void LoadWithOutOfRangeIntervalThrowsInvalidDataException()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"IntervalMinutes\":999}");
        Assert.Throws<InvalidDataException>(() => AppSettings.Load(file));
    }

    [Fact]
    public void LoadWithNullSelectedCharacterIdThrowsInvalidDataException()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"SelectedCharacterId\":null}");
        Assert.Throws<InvalidDataException>(() => AppSettings.Load(file));
    }

    [Fact]
    public void SaveRejectsOutOfRangeIntervalAndDoesNotCreateFile()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        var invalid = new AppSettings { IntervalMinutes = 999 };
        Assert.Throws<InvalidDataException>(() => invalid.Save(file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void SaveRejectsInvalidCharacterIdWithoutOverwritingExistingFile()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        new AppSettings { SelectedCharacterId = "default-cat" }.Save(file);
        var before = File.ReadAllText(file);
        var invalid = AppSettings.Load(file) with { SelectedCharacterId = "../escape" };
        Assert.Throws<InvalidDataException>(() => invalid.Save(file));
        Assert.Equal(before, File.ReadAllText(file));
    }

    [Fact]
    public void SaveRejectsOutOfRangeIdleMinutes()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        Assert.Throws<InvalidDataException>(() => new AppSettings { IdleMinutes = 0 }.Save(file));
    }

    [Fact]
    public void RoundTripPreservesBoundaryAndNullPositionValues()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        var settings = new AppSettings { IntervalMinutes = 5, IdleMinutes = 60, ShowPet = false, PetX = null, PetY = null };
        settings.Save(file);
        Assert.Equal(settings, AppSettings.Load(file));
    }

    private static (ClassicDesktopStyleApplicationLifetime lifetime, IDisposable env) IsolatedDataRoot(string dataDir)
    {
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", dataDir);
        return (new ClassicDesktopStyleApplicationLifetime(), new Restore(previous));
    }
    private sealed class Restore(string? previous) : IDisposable
    { public void Dispose() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }

    [AvaloniaFact]
    public void MalformedJsonSettingsDoNotPreventStartupAndAreBackedUp()
    {
        using var temp = new TempDirectory();
        var (lifetime, env) = IsolatedDataRoot(temp.Path);
        using (env) using (lifetime)
        {
            var settingsFile = Path.Combine(temp.Path, "settings.json");
            File.WriteAllText(settingsFile, "{ not json");
            using var runtime = new AppRuntime(lifetime);
            Assert.Equal(new AppSettings(), runtime.Settings);
            Assert.NotEmpty(Directory.EnumerateFiles(temp.Path, "settings.json.invalid-*"));
        }
    }

    [AvaloniaTheory]
    [InlineData("{\"IntervalMinutes\":999,\"SelectedCharacterId\":\"default-cat\"}")]
    [InlineData("null")]
    public void InvalidSettingsValuesDoNotPreventStartup(string contents)
    {
        using var temp = new TempDirectory();
        var (lifetime, env) = IsolatedDataRoot(temp.Path);
        using (env) using (lifetime)
        {
            var settingsFile = Path.Combine(temp.Path, "settings.json");
            File.WriteAllText(settingsFile, contents);
            using var runtime = new AppRuntime(lifetime);
            Assert.Equal(new AppSettings(), runtime.Settings);
            var backup = Assert.Single(Directory.EnumerateFiles(temp.Path, "settings.json.invalid-*"));
            Assert.Equal(contents, File.ReadAllText(backup));
        }
    }

    [AvaloniaFact]
    public void NullSelectedCharacterIdDoesNotPreventStartup()
    {
        using var temp = new TempDirectory();
        var (lifetime, env) = IsolatedDataRoot(temp.Path);
        using (env) using (lifetime)
        {
            var settingsFile = Path.Combine(temp.Path, "settings.json");
            File.WriteAllText(settingsFile, "{\"SelectedCharacterId\":null}");
            using var runtime = new AppRuntime(lifetime);
            Assert.Equal(new AppSettings(), runtime.Settings);
        }
    }

    [AvaloniaFact]
    public void ValidSettingsLoadUnchangedOnStartup()
    {
        using var temp = new TempDirectory();
        var (lifetime, env) = IsolatedDataRoot(temp.Path);
        using (env) using (lifetime)
        {
            var settingsFile = Path.Combine(temp.Path, "settings.json");
            var settings = new AppSettings { IntervalMinutes = 90, IdleMinutes = 10, ShowPet = false };
            settings.Save(settingsFile);
            using var runtime = new AppRuntime(lifetime);
            Assert.Equal(settings, runtime.Settings);
            Assert.Empty(Directory.EnumerateFiles(temp.Path, "settings.json.invalid-*"));
        }
    }
}
