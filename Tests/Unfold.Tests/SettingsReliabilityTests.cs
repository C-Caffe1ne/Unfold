using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class SettingsReliabilityTests
{
    [Fact]
    public void LoadWithOutOfRangeIntervalRecoversThatFieldAlone()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"IntervalMinutes\":999,\"IdleMinutes\":9}");
        var value = AppSettings.Load(file);
        Assert.Equal(new AppSettings().IntervalMinutes, value.IntervalMinutes); Assert.Equal(9, value.IdleMinutes);
    }

    [Fact]
    public void LoadWithNullSelectedCharacterIdRecoversThatFieldAlone()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "settings.json");
        File.WriteAllText(file, "{\"SelectedCharacterId\":null,\"IdleMinutes\":9}");
        var value = AppSettings.Load(file);
        Assert.Equal(new AppSettings().SelectedCharacterId, value.SelectedCharacterId); Assert.Equal(9, value.IdleMinutes);
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
    [InlineData("null")]
    public void UnreadableSettingsDoNotPreventStartup(string contents)
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
    public void ARecoverableValueStartsUpHealedWithoutDiscardingTheFile()
    {
        using var temp = new TempDirectory();
        var (lifetime, env) = IsolatedDataRoot(temp.Path);
        using (env) using (lifetime)
        {
            var settingsFile = Path.Combine(temp.Path, "settings.json");
            File.WriteAllText(settingsFile, "{\"IntervalMinutes\":999,\"IdleMinutes\":9,\"ShowPet\":false}");
            using var runtime = new AppRuntime(lifetime);
            // Only the damaged field resets; the rest of the file survives and nothing is quarantined.
            Assert.Equal(new AppSettings().IntervalMinutes, runtime.Settings.IntervalMinutes);
            Assert.Equal(9, runtime.Settings.IdleMinutes); Assert.False(runtime.Settings.ShowPet);
            Assert.Empty(Directory.EnumerateFiles(temp.Path, "settings.json.invalid-*"));
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
