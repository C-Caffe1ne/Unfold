using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Layout;
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
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    /// <summary>Builds a valid pack whose missing one-shot reactions make the asset audit report warnings.</summary>
    private static string CreateWarningPack(string root, string id = "warn-pet")
    {
        var directory = Path.Combine(root, "warning-source", id); Directory.CreateDirectory(directory);
        var pixels = new uint[32 * 16];
        for (var y = 3; y < 13; y++) for (var x = 3; x < 13; x++) { pixels[y * 32 + x] = 0xFFF4B860; pixels[y * 32 + x + 16] = 0xFFF4B860; }
        File.WriteAllBytes(Path.Combine(directory, "spritesheet.png"), ImageCodec.EncodePng(new(32, 16, pixels)));
        var manifest = new CharacterManifest(id, "참고 사항이 있는 팩", 1, new("spritesheet.png", 2, 1, 16, 16),
            new() { ["idle"] = new([0, 1], 8), ["attention"] = new([1, 0], 8, Loop: false) });
        File.WriteAllBytes(Path.Combine(directory, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
        var path = Path.Combine(root, $"{Guid.NewGuid():N}.unfoldpet"); CharacterPack.Create(directory, "1.0.0", path); return path;
    }
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
    public async Task SaveResetsThePreviewAndPreservesUpdateAndReinstallBehavior()
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
            Assert.Equal("저장", Button(window, "InstallPetPack").Content);
            Choice(window, "PackSize").SelectedIndex = 2;
            Press(window, "InstallPetPack"); await Until(() => Find<TextBlock>(window, "PackStatus").Text == "저장했어요.");
            Assert.Equal("test-pet", selected?.Manifest.Id);
            Assert.False(Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal(0, Choice(window, "PackClip").ItemCount); Assert.Null(Choice(window, "PackClip").SelectedItem);
            Assert.False(Choice(window, "PackClip").IsEnabled); Assert.False(Find<WrapPanel>(window, "PackInfo").IsVisible);
            Assert.False(Button(window, "PausePackPreview").IsEnabled); Assert.False(Button(window, "ReplayPackPreview").IsEnabled);
            Assert.Same(DesignSystem.Surface, Find<Border>(window, "PackPreviewSurface").Background);
            Assert.Equal(0, Choice(window, "PackSize").SelectedIndex);
            Assert.Equal("", Find<TextBlock>(window, "PackPlaybackStatus").Text);
            Assert.False(Preview(window).OpaqueAt(new Point(96, 96)));
            Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Assert.Equal("저장", Button(window, "InstallPetPack").Content);
            File.WriteAllText(Path.Combine(selected!.DirectoryPath, "spritesheet.png"), "damaged");
            Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Press(window, "InstallPetPack"); await Until(() => Find<TextBlock>(window, "PackStatus").Text == "저장했어요.");
            Assert.Equal(2, Assert.Single(library.List()).LoadAnimation("idle").Count);
            file = CharacterPackTests.CreatePack(temp.Path, "2.0.0"); Press(window, "OpenPetPack");
            await Until(() => Button(window, "InstallPetPack").IsEnabled); Assert.Equal("저장", Button(window, "InstallPetPack").Content);
            Press(window, "InstallPetPack"); await Until(() => Find<TextBlock>(window, "PackStatus").Text == "저장했어요.");
            using (var update = CharacterPack.Open(file)) Assert.Equal("2.0.0", library.InspectInstall(update).InstalledVersion);
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
    public async Task EnlargedThemePreviewFitsTheMinimumWidthAndDoesNotInstall()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library")); var file = CharacterPackTests.CreatePack(temp.Path);
        var window = new PetPackWindow(library, _ => Task.CompletedTask, () => Task.FromResult<string?>(file)) { Width = 480, Height = 560 };
        try
        {
            window.Show(); Press(window, "OpenPetPack"); await Until(() => Button(window, "PausePackPreview").IsEnabled);
            window.Width = 800; window.Height = 800; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var surface = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PackPreviewSurface");
            Assert.Equal(520, surface.Bounds.Width, 0); Assert.Equal(200, Choice(window, "PackClip").Bounds.Width, 0);
            window.Width = 480; window.Height = 560; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Choice(window, "PackSize").SelectedIndex = 2;
            window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            Assert.Same(DesignSystem.Surface, surface.Background);
            Assert.Equal(384, Preview(window).Bounds.Width); Assert.Equal(384, Preview(window).Bounds.Height);
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            Assert.True(Preview(window).Bounds.Right <= surface.Bounds.Width,
                $"Preview right {Preview(window).Bounds.Right}, surface width {surface.Bounds.Width}, scroll viewport {scroll.Viewport.Width}, client width {window.ClientSize.Width}");
            var status = window.GetVisualDescendants().OfType<TextBlock>().Single(control => control.Name == "PackStatus");
            var statusOrigin = status.TranslatePoint(default, window)!.Value;
            Assert.True(statusOrigin.Y >= 0 && statusOrigin.Y + status.Bounds.Height <= window.ClientSize.Height);
            Assert.Equal("미리보기 크기", AutomationProperties.GetName(Choice(window, "PackSize")));
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComboBox>(), control => control.Name == "PackBackground");
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
    public async Task PackNotesAreRemovedWhileValidWarningsAndInvalidPackValidationRemain()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var library = new CharacterLibrary(Path.Combine(temp.Path, "library"));
        var warningPack = CreateWarningPack(temp.Path); var file = warningPack;
        var window = new PetPackWindow(library, _ => Task.CompletedTask, () => Task.FromResult<string?>(file))
        { Width = 480, Height = 560 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "ReviewPackWarnings");

            Press(window, "OpenPetPack"); await Until(() => Button(window, "InstallPetPack").IsEnabled);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            using (var pack = CharacterPack.Open(warningPack)) Assert.Equal(3, pack.Audit.Warnings.Count);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control =>
                control.Name is "ReviewPackWarnings" or "PackWarnings" or "PackWarningSummary" or "PackWarningBar");
            var save = Button(window, "InstallPetPack");
            var origin = save.TranslatePoint(default, window)!.Value;
            Assert.True(save.Bounds.Height > 0);
            Assert.True(origin.Y >= 0 && origin.Y + save.Bounds.Height <= window.ClientSize.Height + 1);

            file = CharacterPackTests.CreatePack(temp.Path); Press(window, "OpenPetPack");
            await Until(() => Button(window, "InstallPetPack").IsEnabled); Dispatcher.UIThread.RunJobs();
            file = Path.Combine(temp.Path, "broken.unfoldpet"); File.WriteAllText(file, "broken archive");
            Press(window, "OpenPetPack"); await Until(() => Button(window, "OpenPetPack").IsEnabled);
            Dispatcher.UIThread.RunJobs();
            Assert.False(Button(window, "InstallPetPack").IsEnabled);
            Assert.Contains("팩을 열지 못했어요.", Find<TextBlock>(window, "PackStatus").Text);
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
            Assert.Equal(0, completed); Assert.Equal("미리보기 계속", AutomationProperties.GetName(Button(window, "PausePackPreview")));
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

    [AvaloniaFact]
    public void OpenAndPlaybackControlsUseTheRequestedPreviewLayout()
    {
        using var temp = new TempDirectory(); using var profile = new ProfileScope(temp.Path);
        var window = new PetPackWindow(new(Path.Combine(temp.Path, "library")), _ => Task.CompletedTask);
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var clip = Choice(window, "PackClip");
            var open = Button(window, "OpenPetPack");
            var previewOptions = Find<WrapPanel>(window, "PackPreviewOptions");
            var size = Choice(window, "PackSize");
            var surface = Find<Border>(window, "PackPreviewSurface");
            var playback = Find<StackPanel>(window, "PackPlaybackControls");
            var pause = Button(window, "PausePackPreview");
            var replay = Button(window, "ReplayPackPreview");

            Assert.Contains(previewOptions, clip.GetVisualAncestors());
            Assert.Contains(previewOptions, size.GetVisualAncestors());
            Assert.DoesNotContain(previewOptions, open.GetVisualAncestors());
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ComboBox>(), control => control.Name == "PackBackground");
            foreach (var dimensions in new[] { new Size(480, 560), new Size(520, 850), new Size(860, 680) })
            {
                window.Width = dimensions.Width; window.Height = dimensions.Height;
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var clipOrigin = clip.TranslatePoint(default, window)!.Value;
                var sizeOrigin = size.TranslatePoint(default, window)!.Value;
                var openOrigin = open.TranslatePoint(default, window)!.Value;
                var previewOrigin = surface.TranslatePoint(default, window)!.Value;
                Assert.True(openOrigin.Y + open.Bounds.Height <= previewOrigin.Y);
                Assert.True(clipOrigin.Y >= previewOrigin.Y + surface.Bounds.Height);
                Assert.Equal(clipOrigin.Y, sizeOrigin.Y, 1);
                Assert.True(clipOrigin.X + clip.Bounds.Width < sizeOrigin.X);
                var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            }

            Assert.Contains(surface, playback.GetVisualAncestors());
            Assert.Equal(HorizontalAlignment.Center, playback.HorizontalAlignment);
            Assert.IsType<PathIcon>(pause.Content);
            Assert.IsType<PathIcon>(replay.Content);
            Assert.Equal(44, pause.Bounds.Width); Assert.Equal(44, replay.Bounds.Width);
            var surfaceOrigin = surface.TranslatePoint(default, window)!.Value;
            var playbackOrigin = playback.TranslatePoint(default, window)!.Value;
            var playbackCenter = playbackOrigin.X + playback.Bounds.Width / 2;
            var surfaceCenter = surfaceOrigin.X + surface.Bounds.Width / 2;
            Assert.Equal(surfaceCenter, playbackCenter, 1);
            Assert.True(playbackOrigin.Y > Preview(window).TranslatePoint(default, window)!.Value.Y + Preview(window).Bounds.Height);
        }
        finally { window.Close(); }
    }
}
