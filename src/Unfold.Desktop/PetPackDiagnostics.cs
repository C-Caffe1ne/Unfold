using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Opt-in real-time pack playback review using a fresh data profile.</summary>
internal static class PetPackDiagnostics
{
    public static async Task Run(AppRuntime runtime, IClassicDesktopStyleApplicationLifetime desktop, string packPath)
    {
        var directory = Path.Combine(AppPaths.DataRoot, "verification"); Directory.CreateDirectory(directory);
        PetPackWindow? installer = null; Window? review = null;
        using var dark = new AnimationView { Width = 192, Height = 192 };
        using var light = new AnimationView { Width = 192, Height = 192 };
        try
        {
            runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            await runtime.Start(true, true); runtime.Reset();
            using var pack = await Task.Run(() => CharacterPack.Open(packPath));
            installer = new(runtime.Library, runtime.SelectInstalledCharacter, () => Task.FromResult<string?>(Path.GetFullPath(packPath)));
            AppRuntime.PrepareDiagnosticWindow(installer); installer.Show();
            Button Install() => installer.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "InstallPetPack");
            Press(installer, "OpenPetPack"); await Until(() => Install().IsEnabled);
            await Until(() => FindButton(installer, "PausePackPreview").IsEnabled);
            Capture(installer, Path.Combine(directory, "pack-preview.png"));
            var previewControls = await ReviewPreview(installer, pack, directory);
            if (runtime.Characters.Any(character => character.Manifest.Id == pack.Id)) throw new InvalidOperationException("Preview installed the pack.");
            Press(installer, "InstallPetPack"); await Until(() => !Install().IsEnabled &&
                installer.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PackStatus").Text == "저장했어요.");
            if (runtime.Selected?.Manifest.Id != pack.Id || !runtime.Clock.Paused) throw new InvalidOperationException("Installed pack was not selected or resumed the timer.");
            Capture(installer, Path.Combine(directory, "pack-installed.png")); installer.Close(); installer = null;
            var selected = runtime.Selected;
            var audit = CharacterAssetAudit.InspectPackage(selected.DirectoryPath);
            if (audit.Errors.Count != 0) throw new InvalidDataException(string.Join("\n", audit.Errors));
            var heading = Ui.Text(selected.Manifest.Name, 24, Ui.Accent);
            var detail = Ui.Text("", 14);
            review = new Window { Title = "Unfold · Pack playback review", Width = 560, Height = 420, Background = Ui.Background,
                Content = new Border { Padding = new Thickness(24), Child = Ui.Column(heading, detail, Ui.Row(
                    Ui.Column(Ui.Text("Dark · 192 px", 12), new Border { Background = Ui.Panel, Padding = new Thickness(24), Child = dark }),
                    Ui.Column(Ui.Text("Light · 192 px", 12), new Border { Background = Brushes.WhiteSmoke, Padding = new Thickness(24), Child = light }))) } };
            AppRuntime.PrepareDiagnosticWindow(review); review.Show();
            var pet = runtime.ActivePet ?? throw new InvalidOperationException("The desktop pet is not open.");
            var results = new List<object>();
            foreach (var key in selected.Manifest.Animations.Keys.OrderBy(key => key == "idle" ? 0 : 1).ThenBy(key => key, StringComparer.Ordinal))
            {
                var frames = await runtime.Clip(key); var durationMs = frames.Sum(frame => frame.Duration.TotalMilliseconds);
                if (durationMs > 10000) throw new InvalidDataException("Review clips must be at most 10 seconds.");
                var repeat = selected.Manifest.Animations[key].Loop;
                detail.Text = $"{key} · {durationMs / 1000:0.###} seconds · {(repeat ? "loop" : "once, then idle")}";
                var finishedDark = 0; var finishedLight = 0;
                void DarkCompleted() => finishedDark++;
                void LightCompleted() => finishedLight++;
                dark.Completed += DarkCompleted; light.Completed += LightCompleted;
                try
                {
                    dark.SetRunning(true); light.SetRunning(true);
                    var elapsed = Stopwatch.StartNew();
                    dark.SetFrames(frames, repeat, selected.Manifest.RenderStyle == "pixel");
                    light.SetFrames(frames, repeat, selected.Manifest.RenderStyle == "pixel");
                    var reaction = key == "idle" ? Task.CompletedTask : pet.React(key);
                    // PetWindow.PetView is the pet's own animation surface. Reading it keeps this
                    // review on the live pet rather than on a guess about the window's visual tree.
                    var startedInExpectedMode = key == "idle" ? pet.PetView.Repeats : !pet.PetView.Repeats;
                    if (!startedInExpectedMode) throw new InvalidOperationException("The desktop pet did not enter the requested reaction.");
                    await Task.Delay(TimeSpan.FromMilliseconds(durationMs / 2));
                    Capture(review, Path.Combine(directory, key + ".png"));
                    Capture(pet.PetView, Path.Combine(directory, key + "-pet.png"));
                    await Task.Delay(TimeSpan.FromMilliseconds(durationMs / 2 + 100));
                    if (!repeat) await Until(() => finishedDark == 1 && finishedLight == 1);
                    await reaction;
                    if (!pet.PetView.Repeats || (repeat && (finishedDark != 0 || finishedLight != 0)))
                        throw new InvalidOperationException("Playback did not retain or restore the idle loop.");
                    results.Add(new { key, plannedMs = durationMs, observedMs = elapsed.Elapsed.TotalMilliseconds,
                        repeat, completedDark = finishedDark, completedLight = finishedLight, petStartedInExpectedMode = startedInExpectedMode, petReturnedToIdleLoop = pet.PetView.Repeats });
                }
                finally { dark.Completed -= DarkCompleted; light.Completed -= LightCompleted; dark.SetRunning(false); light.SetRunning(false); }
            }
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = false });
            foreach (var key in new[] { "attention", "stretch", "celebrate", "click" })
                if (selected.Manifest.Animations.ContainsKey(key)) await pet.React(key);
            if (pet.IsVisible) throw new InvalidOperationException("Reactions revealed a hidden pet.");
            var persisted = new CharacterLibrary(runtime.Library.Root).List().Single(character => character.Manifest.Id == pack.Id);
            _ = persisted.LoadAnimation("idle");
            var report = new { success = true, id = pack.Id, contentVersion = pack.ContentVersion, audit,
                packSha256 = Convert.ToHexString(SHA256.HashData(ImageCodec.ReadBounded(packPath, CharacterPack.MaxArchiveBytes))).ToLowerInvariant(),
                realTimePlayback = true, physicalInput = false, injectedFilePicker = true, simulatedActionConfirmation = true,
                offScreenWindows = true, capturedLivePetView = true,
                selectedAfterInstall = true, reopenedFromDisk = true, hiddenPetStayedHidden = true, previewControls, playback = results,
                imageFiles = Directory.GetFiles(directory, "*.png").Length, os = Environment.OSVersion.ToString(), framework = Environment.Version.ToString() };
            AtomicFile.Write(Path.Combine(directory, "pet-review.json"), JsonSerializer.SerializeToUtf8Bytes(report, CharacterLibrary.JsonOptions));
            review.Close(); review = null; await runtime.Quit();
        }
        catch (Exception error)
        {
            AtomicFile.Write(Path.Combine(directory, "pet-review.json"), JsonSerializer.SerializeToUtf8Bytes(new { success = false, error = error.ToString() }, CharacterLibrary.JsonOptions));
            AppPaths.Log(error); installer?.Close(); review?.Close(); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    private static async Task<object> ReviewPreview(PetPackWindow window, CharacterPack pack, string directory)
    {
        ComboBox Choice(string name) => window.GetVisualDescendants().OfType<ComboBox>().Single(control => control.Name == name);
        var preview = window.GetVisualDescendants().OfType<AnimationView>().Single();
        var surface = window.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PackPreviewSurface");
        var themeBackground = surface.Background;
        var key = pack.Character.Manifest.Animations.ContainsKey("stretch") ? "stretch" :
            pack.Character.Manifest.Animations.FirstOrDefault(pair => !pair.Value.Loop).Key;
        var pausedForMs = 0d; var completed = 0;
        void Completed() => completed++;
        preview.Completed += Completed;
        try
        {
            Press(window, "PausePackPreview");
            if (key is not null)
            {
                var duration = pack.Audit.Clips.Single(clip => clip.Key == key).DurationMs;
                if (duration > 10000) throw new InvalidDataException("Review clips must be at most 10 seconds.");
                Choice("PackClip").SelectedItem = key;
                await Until(() => FindButton(window, "PausePackPreview").IsEnabled && !preview.Repeats);
                pausedForMs = duration + 150;
                await Task.Delay(TimeSpan.FromMilliseconds(pausedForMs));
                if (preview.Repeats || completed != 0) throw new InvalidOperationException("Paused preview advanced to completion.");
                Press(window, "ReplayPackPreview");
                await Until(() => FindButton(window, "PausePackPreview").IsEnabled && !preview.Repeats);
                await Task.Delay(TimeSpan.FromMilliseconds(duration / 2));
                Press(window, "PausePackPreview");
            }
            if (window.GetVisualDescendants().OfType<ComboBox>().Any(control => control.Name == "PackBackground"))
                throw new InvalidOperationException("The removed preview background setting is still present.");
            Choice("PackSize").SelectedIndex = 2; window.UpdateLayout();
            if (preview.Bounds.Width != 384 || preview.Bounds.Height != 384) throw new InvalidOperationException("Large preview has the wrong size.");
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(view => view.Name == "PageBodyScroll");
            if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new InvalidOperationException("Large preview overflows horizontally.");
            Capture(window, Path.Combine(directory, "pack-preview-large-theme-2x.png"), 2);
            // Alternate backgrounds inspect asset edges only; the product has no background selector.
            surface.Background = Brushes.WhiteSmoke;
            Capture(window, Path.Combine(directory, "pack-preview-large-light-2x.png"), 2);
            surface.Background = themeBackground;
            Choice("PackSize").SelectedIndex = 0; window.UpdateLayout();
            if (preview.Bounds.Width != 192) throw new InvalidOperationException("Default preview size was not restored.");
            Press(window, "PausePackPreview");
            if (key is not null)
            {
                await Until(() => preview.Repeats && FindButton(window, "ReplayPackPreview").IsEnabled);
                if (completed != 1 || Choice("PackClip").SelectedItem as string != key)
                    throw new InvalidOperationException("Preview did not retain the selected reaction after returning to idle.");
            }
            return new { reaction = key, pausedForMs, completedOnce = completed, selectedReactionRetained = key is not null,
                logicalPreviewSizes = new[] { 192, 384 }, captureScale = 2, windowRenderScaling = window.RenderScaling,
                backgrounds = new[] { "theme", "light" }, alternateBackgroundInjected = true,
                backgroundSettingVisible = false, horizontalOverflow = false };
        }
        finally { surface.Background = themeBackground; preview.Completed -= Completed; }
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var attempt = 0; attempt < 400 && !ready(); attempt++) await Task.Delay(25);
        if (!ready()) throw new TimeoutException("Pack review did not finish.");
    }
    private static void Press(Window window, string name)
    {
        var button = FindButton(window, name);
        if (!button.IsEnabled) throw new InvalidOperationException("Review button is disabled: " + name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static Button FindButton(Window window, string name) => window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
    private static void Capture(Window window, string path, double scale = 1)
    {
        using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width * scale), (int)Math.Ceiling(window.ClientSize.Height * scale)), new Vector(96 * scale, 96 * scale));
        image.Render(window); image.Save(path, PngBitmapEncoderOptions.Default);
    }
    private static void Capture(Control control, string path)
    {
        var size = control.Bounds.Size;
        if (size.Width < 1 || size.Height < 1) throw new InvalidOperationException("The pet animation surface was not laid out: " + Path.GetFileName(path));
        using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height)), new Vector(96, 96));
        image.Render(control); image.Save(path, PngBitmapEncoderOptions.Default);
    }
}
