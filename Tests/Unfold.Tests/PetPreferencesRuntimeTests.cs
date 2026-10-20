using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetPreferencesRuntimeTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupFallbackRestoresItsOwnSizeWhenTheSavedPetIsMissingOrDamaged(bool damaged)
    {
        using var data = new DataScope();
        var fallbackId = Path.GetFileName(Directory.EnumerateDirectories(AppPaths.BuiltInRoot).First());
        var settings = new AppSettings().WithPetScale(fallbackId, 70)
            .WithSelectedCharacter("lost-pet").WithPetScale("lost-pet", 140);
        settings.Save(Path.Combine(data.Root, "settings.json"));
        if (damaged)
        {
            var directory = Path.Combine(data.Root, "Characters", "lost-pet");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "manifest.json"), "{broken");
        }
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        await runtime.Start(true, true); runtime.Stop();
        Assert.Equal(fallbackId, runtime.Selected!.Manifest.Id);
        Assert.Equal(fallbackId, runtime.Settings.SelectedCharacterId);
        Assert.Equal(70, runtime.Settings.PetScalePercent);
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 90 });
        var saved = AppSettings.Load(Path.Combine(data.Root, "settings.json"));
        Assert.Equal(90, saved.GetPetScalePercent(fallbackId));
        Assert.Equal(140, saved.GetPetScalePercent("lost-pet"));
    }

    [AvaloniaFact]
    public async Task HomeSelectionRestoresEachScaleWithoutChangingTheStoppedTimer()
    {
        using var data = new DataScope();
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        await runtime.Start(true, true); runtime.Stop(); runtime.ShowSettings();
        var window = (SettingsWindow)lifetime.MainWindow!;
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        var picker = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(control => ReferenceEquals(control.ItemsSource, runtime.Characters));
        var first = runtime.Selected!;
        var second = runtime.Characters.First(pet => pet.Manifest.Id != first.Manifest.Id);
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 80 });
        var remaining = runtime.Clock.Remaining;
        picker.SelectedItem = second;
        await Until(() => runtime.Settings.SelectedCharacterId == second.Manifest.Id);
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 130 });
        picker.SelectedItem = first;
        await Until(() => runtime.Settings.SelectedCharacterId == first.Manifest.Id);
        Assert.Equal(80, runtime.Settings.PetScalePercent);
        picker.SelectedItem = second;
        await Until(() => runtime.Settings.SelectedCharacterId == second.Manifest.Id);
        Assert.Equal(130, runtime.Settings.PetScalePercent);
        Assert.True(runtime.Clock.Stopped);
        Assert.Equal(remaining, runtime.Clock.Remaining);
        Assert.False(runtime.Reminder.HasNotice);
        var saved = AppSettings.Load(Path.Combine(data.Root, "settings.json"));
        Assert.Equal(80, saved.GetPetScalePercent(first.Manifest.Id));
        Assert.Equal(130, saved.GetPetScalePercent(second.Manifest.Id));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovingAPetDropsItsPreferencesAndRestoresTheFallbackScale(bool selected)
    {
        using var data = new DataScope();
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        var custom = PetEditingCoreTests.InstallMedia(runtime.Library, data.Root);
        await runtime.Start(true, true); runtime.Stop();
        var fallback = runtime.Characters.First(pet => pet.Manifest.Id != custom.Manifest.Id);
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = fallback.Manifest.Id });
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 70 });
        await runtime.SetPetFavorite(custom.Manifest.Id, true);
        if (selected)
        {
            await runtime.SelectInstalledCharacter(custom);
            await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 140 });
        }
        runtime.Library.Delete(custom.Manifest.Id);
        await runtime.RefreshAfterCharacterRemoval(custom.Manifest.Id);
        Assert.Equal(fallback.Manifest.Id, runtime.Settings.SelectedCharacterId);
        Assert.Equal(70, runtime.Settings.PetScalePercent);
        Assert.False(runtime.Settings.PetPreferences.ContainsKey(custom.Manifest.Id));
        var saved = AppSettings.Load(Path.Combine(data.Root, "settings.json"));
        Assert.False(saved.PetPreferences.ContainsKey(custom.Manifest.Id));
        Assert.Equal(70, saved.PetScalePercent);
    }

    [AvaloniaFact]
    public async Task FailedSaveCannotPublishScaleOrFavoriteChanges()
    {
        using var data = new DataScope();
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        await runtime.Start(true, true); runtime.Stop();
        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 90 });
        var before = runtime.Settings;
        var path = Path.Combine(data.Root, "settings.json");
        File.Move(path, path + ".saved");
        Directory.CreateDirectory(path);
        var scaleError = await Record.ExceptionAsync(() =>
            runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 120 }));
        Assert.True(scaleError is IOException or UnauthorizedAccessException);
        Assert.Same(before, runtime.Settings);
        var favoriteError = await Record.ExceptionAsync(() => runtime.SetPetFavorite(before.SelectedCharacterId, true));
        Assert.True(favoriteError is IOException or UnauthorizedAccessException);
        Assert.Same(before, runtime.Settings);
        Assert.False(runtime.Settings.IsPetFavorite(before.SelectedCharacterId));
    }

    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 300 && !predicate(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.True(predicate());
    }

    private sealed class DataScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        public string Root => temp.Path;
        public DataScope() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
        public void Dispose() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
