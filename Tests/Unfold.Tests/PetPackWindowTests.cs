using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetPackWindowTests
{
    private sealed class ProfileScope(string root) : IDisposable
    {
        private readonly string? previous = Set(root);
        private static string? Set(string root)
        {
            var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(root, "profile")); return previous;
        }
        public void Dispose() => Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous);
    }
    private static Button Button(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    private static void Press(Window window, string name) => Button(window, name).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 200 && !ready(); i++) { await Task.Delay(10, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); }
        Assert.True(ready());
    }
    [AvaloniaFact]
    public async Task PreviewRequiresInstallThenOffersUpdateAndReinstall()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var file = CharacterPackTests.CreatePack(temp.Path); CharacterPackage? selected = null;
        var window = new PetPackWindow(library, pack => { selected = pack; return Task.CompletedTask; }, () => Task.FromResult<string?>(file));
        try
        {
            window.Show(); Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Assert.Empty(library.List()); Assert.Null(selected);
            Assert.Equal(5, window.GetVisualDescendants().OfType<ComboBox>().Single().ItemCount);
            Press(window, "InstallPetPack"); await Until(() => Equals(Button(window, "InstallPetPack").Content, "Installed"));
            Assert.Equal("test-pet", selected?.Manifest.Id);
            Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal("Reinstall", Button(window, "InstallPetPack").Content);
            file = CharacterPackTests.CreatePack(temp.Path, "2.0.0"); Press(window, "OpenPetPack");
            await Until(() => Button(window, "InstallPetPack").IsEnabled); Assert.Equal("Update", Button(window, "InstallPetPack").Content);
            file = Path.Combine(temp.Path, "broken.unfoldpet"); File.WriteAllText(file, "broken archive"); Press(window, "OpenPetPack");
            await Until(() => Button(window, "OpenPetPack").IsEnabled);
            Assert.False(Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal(0, window.GetVisualDescendants().OfType<ComboBox>().Single().ItemCount);
            Assert.Equal("test-pet", Assert.Single(library.List()).Manifest.Id);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task ClosingPreviewLeavesLibraryUntouched()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var file = CharacterPackTests.CreatePack(temp.Path); var selected = false;
        var window = new PetPackWindow(library, _ => { selected = true; return Task.CompletedTask; }, () => Task.FromResult<string?>(file));
        try
        {
            window.Show(); Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
        }
        finally { window.Close(); }
        Assert.Empty(library.List()); Assert.False(selected);
    }
}
