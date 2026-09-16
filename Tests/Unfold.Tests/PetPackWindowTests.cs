using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Media;
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
    private static ComboBox Choice(Window window, string name) => window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == name);
    private static AnimationView Preview(Window window) => window.GetVisualDescendants().OfType<AnimationView>().Single();
    private static bool Repeats(Window window) => (bool)typeof(AnimationView).GetProperty("Repeats", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Preview(window))!;
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
            Assert.Equal(5, Choice(window, "PackClip").ItemCount);
            Press(window, "InstallPetPack"); await Until(() => Equals(Button(window, "InstallPetPack").Content, "설치 완료"));
            Assert.Equal("test-pet", selected?.Manifest.Id);
            Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal("재설치", Button(window, "InstallPetPack").Content);
            file = CharacterPackTests.CreatePack(temp.Path, "2.0.0"); Press(window, "OpenPetPack");
            await Until(() => Button(window, "InstallPetPack").IsEnabled); Assert.Equal("업데이트", Button(window, "InstallPetPack").Content);
            file = Path.Combine(temp.Path, "broken.unfoldpet"); File.WriteAllText(file, "broken archive"); Press(window, "OpenPetPack");
            await Until(() => Button(window, "OpenPetPack").IsEnabled);
            Assert.False(Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal(0, Choice(window, "PackClip").ItemCount);
            Assert.False(Button(window, "PausePackPreview").IsEnabled);
            Assert.False(Button(window, "ReplayPackPreview").IsEnabled);
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
    [AvaloniaFact]
    public async Task PausedSelectionResumesThenReplayKeepsTheChosenReaction()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var file = CharacterPackTests.CreatePack(temp.Path);
        var window = new PetPackWindow(library, _ => Task.CompletedTask, () => Task.FromResult<string?>(file));
        try
        {
            window.Show(); Assert.False(Button(window, "PausePackPreview").IsEnabled);
            Press(window, "OpenPetPack"); await Until(() => Button(window, "PausePackPreview").IsEnabled);
            Press(window, "PausePackPreview");
            Assert.Equal("미리보기 계속", AutomationProperties.GetName(Button(window, "PausePackPreview")));
            Choice(window, "PackClip").SelectedItem = "stretch";
            await Until(() => Button(window, "PausePackPreview").IsEnabled && !Repeats(window));
            var completed = 0; Preview(window).Completed += () => completed++;
            await Task.Delay(400, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, completed); Assert.False(Repeats(window));
            Press(window, "PausePackPreview");
            await Until(() => Repeats(window) && Button(window, "ReplayPackPreview").IsEnabled);
            Assert.Equal(1, completed); Assert.Equal("stretch", Choice(window, "PackClip").SelectedItem);
            Press(window, "PausePackPreview"); Press(window, "ReplayPackPreview");
            await Until(() => !Repeats(window) && Button(window, "PausePackPreview").IsEnabled);
            Assert.Equal("미리보기 일시정지", AutomationProperties.GetName(Button(window, "PausePackPreview")));
            await Until(() => Repeats(window) && Button(window, "ReplayPackPreview").IsEnabled);
            Assert.Equal(2, completed); Assert.Equal("stretch", Choice(window, "PackClip").SelectedItem);
            Assert.Empty(library.List());
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task EnlargedLightPreviewFitsTheMinimumWidthAndDoesNotInstall()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var file = CharacterPackTests.CreatePack(temp.Path);
        var window = new PetPackWindow(library, _ => Task.CompletedTask, () => Task.FromResult<string?>(file)) { Width = 480, Height = 560 };
        try
        {
            window.Show(); Press(window, "OpenPetPack"); await Until(() => Button(window, "PausePackPreview").IsEnabled);
            Choice(window, "PackBackground").SelectedIndex = 1; Choice(window, "PackSize").SelectedIndex = 2;
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var surface = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PackPreviewSurface");
            Assert.Equal(Brushes.WhiteSmoke, surface.Background);
            Assert.Equal(384, Preview(window).Bounds.Width); Assert.Equal(384, Preview(window).Bounds.Height);
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            Assert.True(Preview(window).Bounds.Right <= surface.Bounds.Width);
            var status = window.GetVisualDescendants().OfType<TextBlock>().Single(control => control.Name == "PackStatus");
            var statusOrigin = status.TranslatePoint(default, window)!.Value;
            Assert.True(statusOrigin.Y >= 0 && statusOrigin.Y + status.Bounds.Height <= window.ClientSize.Height);
            Assert.Equal("미리보기 크기", AutomationProperties.GetName(Choice(window, "PackSize")));
            Assert.Equal("미리보기 배경", AutomationProperties.GetName(Choice(window, "PackBackground")));
            Choice(window, "PackSize").SelectedIndex = 1; window.UpdateLayout(); Assert.Equal(288, Preview(window).Bounds.Width);
            Choice(window, "PackSize").SelectedIndex = 0; window.UpdateLayout(); Assert.Equal(192, Preview(window).Bounds.Width);
            window.Width = 520; window.Height = 850; Choice(window, "PackSize").SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var install = Button(window, "InstallPetPack");
            var installOrigin = install.TranslatePoint(new Avalonia.Point(), window);
            Assert.NotNull(installOrigin);
            Assert.True(installOrigin.Value.Y + install.Bounds.Height <= window.ClientSize.Height,
                $"Install bottom {installOrigin.Value.Y + install.Bounds.Height}, client height {window.ClientSize.Height}, requested height {window.Height}");
            Assert.Empty(library.List());
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task HidingPreviewSuspendsTheReactionAndPreservesManualPause()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var file = CharacterPackTests.CreatePack(temp.Path);
        var window = new PetPackWindow(library, _ => Task.CompletedTask, () => Task.FromResult<string?>(file));
        try
        {
            window.Show(); Press(window, "OpenPetPack"); await Until(() => Button(window, "PausePackPreview").IsEnabled);
            Press(window, "PausePackPreview"); Choice(window, "PackClip").SelectedItem = "click";
            await Until(() => !Repeats(window) && Button(window, "PausePackPreview").IsEnabled);
            var completed = 0; Preview(window).Completed += () => completed++;
            window.Hide(); window.Show();
            await Task.Delay(350, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, completed); Assert.Equal("계속", Button(window, "PausePackPreview").Content);
            Press(window, "PausePackPreview"); window.Hide();
            await Task.Delay(350, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs(); Assert.Equal(0, completed);
            window.Show(); await Until(() => completed == 1 && Repeats(window));
            Press(window, "ReplayPackPreview"); await Until(() => !Repeats(window) && Button(window, "PausePackPreview").IsEnabled);
            var tabs = window.GetVisualDescendants().OfType<TabControl>().Single(control => control.Name == "PetManagementTabs");
            tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
            await Task.Delay(350, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, completed);
            tabs.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
            await Until(() => completed == 2 && Repeats(window));
        }
        finally { window.Close(); }
    }
}
