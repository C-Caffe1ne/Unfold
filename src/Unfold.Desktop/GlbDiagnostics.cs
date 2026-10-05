using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

internal static class GlbDiagnostics
{
    public static async Task Run(AppRuntime runtime, IClassicDesktopStyleApplicationLifetime desktop, string file)
    {
        var directory = Path.Combine(AppPaths.DataRoot, "verification", "glb"); Directory.CreateDirectory(directory);
        Window? window = null; PetBuilderView? page = null;
        try
        {
            runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            await runtime.Start(true, true); runtime.Reset();
            window = new Window { Width = 620, Height = 850, MinWidth = 480, MinHeight = 560, Background = Ui.Background };
            page = new PetBuilderView(window, runtime.Library, runtime.SelectInstalledCharacter, _ => Task.CompletedTask, chooseMedia: () => Task.FromResult<string?>(file));
            window.Content = Ui.PageFrame(window, page, inset: 10); AppRuntime.PrepareDiagnosticWindow(window); window.Show();
            T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
            await Task.Delay(100); window.UpdateLayout(); Capture(window, Path.Combine(directory, "glb-empty.png"));
            Find<Button>("OpenPetBuilderFile").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !page.IsBusy && Find<Button>("SaveGlbPet").IsEnabled);
            await Task.Delay(250); window.UpdateLayout(); Capture(window, Path.Combine(directory, "glb-editor.png"));
            foreach (var scale in new[] { new Size(480, 560), new Size(860, 680) })
            {
                window.MinWidth = scale.Width; window.MinHeight = scale.Height; window.Width = scale.Width; window.Height = scale.Height; window.UpdateLayout(); await Task.Delay(100);
                if (Math.Abs(window.ClientSize.Width - scale.Width) > 2 || Math.Abs(window.ClientSize.Height - scale.Height) > 2)
                    throw new InvalidOperationException("GLB editor did not reach the requested client size.");
                var scroll = Find<ScrollViewer>("PageBodyScroll");
                if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new InvalidOperationException("GLB editor overflows horizontally.");
                Capture(window, Path.Combine(directory, $"glb-editor-{scale.Width}-{scale.Height}.png"));
            }
            Find<ComboBox>("GlbPreviewAction").SelectedItem = "click";
            Find<ComboBox>("GlbHeading_click").SelectedIndex = 1;
            Find<ComboBox>("GlbRepeat_click").SelectedIndex = 1;
            await Until(() => !window.GetVisualDescendants().OfType<GlbPetView>().Single().IsPreviewLoading);
            Capture(window, Path.Combine(directory, "glb-click-heading.png"));
            Find<Button>("SaveGlbPet").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !page.IsBusy && runtime.Selected?.IsGlb == true);
            var selected = runtime.Selected!; var pet = runtime.ActivePet!;
            await CaptureSettings(runtime, directory);
            var pointerCanvasVerified = await VerifyPointerCanvas(pet, directory);
            var resolution = await VerifyResolution(selected, directory);
            var clips = new List<object>();
            foreach (var key in new[] { "idle", "click", "pickup", "held", "land", "walk" }.Where(selected.Manifest.Animations.ContainsKey))
            {
                var source = (GlbAnimationFrames)await runtime.Clip(key);
                var count = 0; void Completed() => count++;
                var view = new AnimationView { Width = 192, Height = 192 }; var host = new Window { Width = 192, Height = 192, Content = view };
                AppRuntime.PrepareDiagnosticWindow(host); host.Show(); view.Completed += Completed;
                try
                {
                    view.SetFrames(source, false, false); await Task.Delay(100);
                    view.SetRunning(false); await Task.Delay(150); if (count != 0) throw new InvalidOperationException("Paused GLB completed.");
                    view.SetRunning(true); await Task.Delay(TimeSpan.FromSeconds(source.DurationSeconds / 2));
                    Capture(view, Path.Combine(directory, key + ".png"));
                    await Until(() => count == 1, (int)(source.DurationSeconds * 1000 + 5000));
                    await Task.Delay(100); if (count != 1) throw new InvalidOperationException("GLB completed more than once.");
                    clips.Add(new { key, duration = source.DurationSeconds, completed = count });
                }
                finally { view.Completed -= Completed; view.Dispose(); host.Close(); }
            }
            Capture(pet.PetView, Path.Combine(directory, "live-pet.png"));
            await runtime.UpdateSettings(runtime.Settings with { ShowPet = false }); await pet.React("click");
            if (pet.IsVisible) throw new InvalidOperationException("Hidden GLB pet reappeared.");
            var reopened = new CharacterLibrary(runtime.Library.Root).List().Single(p => p.Manifest.Id == selected.Manifest.Id);
            if (!reopened.IsGlb || !reopened.Manifest.Animations.OrderBy(p => p.Key).SequenceEqual(selected.Manifest.Animations.OrderBy(p => p.Key)))
                throw new InvalidOperationException("GLB mapping did not persist.");
            AtomicFile.Write(Path.Combine(directory, "result.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { success = true, appVersion = AppRelease.Version, displayVersion = AppRelease.DisplayVersion, model = Path.GetFileName(file), triangles = selected.Model.TriangleCount, animations = selected.Model.Animations,
                actionSettings = selected.Manifest.Animations, playback = clips, resolution, pointerCanvasVerified, importedThroughUi = true, persisted = true, integratedSettings = true, hiddenPetStayedHidden = true,
                qualityWindowOnScreen = true, otherWindowsOffScreen = true, physicalInput = false, os = Environment.OSVersion.ToString() }, CharacterLibrary.JsonOptions));
            page.Dispose(); window.Close(); await runtime.Quit();
        }
        catch (Exception e)
        {
            AtomicFile.Write(Path.Combine(directory, "result.json"), JsonSerializer.SerializeToUtf8Bytes(new { success = false, error = e.ToString() }, CharacterLibrary.JsonOptions));
            AppPaths.Log(e); page?.Dispose(); window?.Close(); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    private static async Task<IReadOnlyList<object>> VerifyResolution(CharacterPackage pet, string directory)
    {
        var source = (GlbAnimationFrames)pet.LoadAnimation("idle");
        using var view = new AnimationView { Width = 192, Height = 192 };
        var host = new Window { Width = 192, Height = 192, Content = view, Title = "Unfold · GLB 화질 확인" };
        AppRuntime.PrepareDiagnosticWindow(host);
        // Off-screen macOS windows report 1x even on Retina. Briefly place this
        // non-activating quality window on the real display to verify its DPI.
        var screen = host.Screens.Primary ?? throw new InvalidOperationException("GLB quality review requires a display.");
        host.Position = new Avalonia.PixelPoint(screen.WorkingArea.X + 32, screen.WorkingArea.Y + 32);
        host.Show(); view.SetRunning(false); view.SetFrames(source, true, false);
        var results = new List<object>();
        try
        {
            foreach (var size in new[] { 192, 288, 384 })
            {
                host.MinWidth = host.MinHeight = size; host.Width = host.Height = size; view.Width = view.Height = size;
                host.UpdateLayout(); await Task.Delay(100);
                var expected = AnimationView.PixelSizeFor(size, host.RenderScaling);
                await Until(() => view.TargetPixelSize == expected && view.RenderedPixelSize == expected);
                Capture(view, Path.Combine(directory, $"resolution-{size}-dip.png"), host.RenderScaling);
                var started = System.Diagnostics.Stopwatch.StartNew();
                var sample = await Task.Run(() => source.GetFrame(Math.Min(1, source.Count - 1), expected));
                started.Stop();
                if (!sample.Image.Pixels.Any(p => (p >> 24) is > 0 and < 255))
                    throw new InvalidOperationException("Enlarged GLB has no antialiased edge coverage.");
                results.Add(new { logicalSize = size, renderScaling = host.RenderScaling, pixels = view.RenderedPixelSize, renderMs = started.Elapsed.TotalMilliseconds });
            }
            // Preserve a like-for-like reference of the previous fixed 192px path
            // at 150% pet size, rendered through the same native image control.
            var reference = pet.Model.Render(pet.Manifest.Animations["idle"].ModelClip!, 0, pet.Manifest.Model!, pet.Manifest.Animations["idle"].ModelClip!);
            host.MinWidth = host.MinHeight = 288; host.Width = host.Height = 288; view.Width = view.Height = 288;
            view.SetFrames([new AnimationFrame(reference, TimeSpan.FromSeconds(1))], true, false);
            host.UpdateLayout(); await Task.Delay(100);
            Capture(view, Path.Combine(directory, "resolution-before-288-dip.png"), host.RenderScaling);
            return results;
        }
        finally { view.Dispose(); host.Close(); }
    }
    private static async Task<bool> VerifyPointerCanvas(PetWindow pet, string directory)
    {
        if (!pet.HasPointerArt) return false;
        var changed = false;
        void Observe(object? sender, EventArgs args)
        { if (pet.PetView.Pose != PetPose.Neutral || pet.PetView.RenderTransform?.Value != Matrix.Identity) changed = true; }
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += Observe; timer.Start();
        try
        {
            pet.BeginCompanionPress(); pet.AdvanceCompanion(PetPose.LiftDelay);
            await Until(() => pet.PointerPhase == PetPointerPhase.Held);
            await Task.Delay(300); Capture(pet.PetView, Path.Combine(directory, "pointer-held-fixed.png"));
            pet.ReleaseCompanionPress(false); Observe(null, EventArgs.Empty);
            if (pet.PointerPhase != PetPointerPhase.Recovering || pet.ActiveAnimation != "land")
                throw new InvalidOperationException("GLB release did not start the assigned land clip immediately.");
            await Task.Delay(100); Capture(pet.PetView, Path.Combine(directory, "pointer-release-fixed.png"));
            await Until(() => pet.PointerPhase == PetPointerPhase.None && pet.ActiveAnimation == "idle");
            if (changed) throw new InvalidOperationException("GLB pointer interaction transformed the canvas.");
            return true;
        }
        finally { timer.Stop(); timer.Tick -= Observe; }
    }
    private static async Task CaptureSettings(AppRuntime runtime, string directory)
    {
        using var settings = new SettingsWindow(runtime);
        AppRuntime.PrepareDiagnosticWindow(settings); settings.Show();
        T Find<T>(string name) where T : Control => settings.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
        try
        {
            Find<Button>("SettingsNavPacks").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(100); settings.UpdateLayout();
            await Task.Delay(100); settings.UpdateLayout();
            var builder = settings.GetVisualDescendants().OfType<PetBuilderView>().Single();
            await builder.OpenPackage(runtime.Selected!);
            var editor = settings.GetVisualDescendants().OfType<GlbPetView>().Single();
            await Until(() => !editor.IsBusy && editor.HasDraft && !editor.IsPreviewLoading);
            foreach (var size in new[] { new Size(1120, 800), new Size(860, 680), new Size(640, 560) })
            {
                settings.MinWidth = size.Width; settings.MinHeight = size.Height;
                settings.Width = size.Width; settings.Height = size.Height;
                await Task.Delay(150); settings.UpdateLayout();
                if (Math.Abs(settings.ClientSize.Width - size.Width) > 2 || Math.Abs(settings.ClientSize.Height - size.Height) > 2)
                    throw new InvalidOperationException("Settings did not reach the requested client size.");
                var scroll = Find<ScrollViewer>("PageBodyScroll");
                if (scroll.Extent.Width > scroll.Viewport.Width + 1) throw new InvalidOperationException("GLB settings overflow horizontally.");
                Capture(settings, Path.Combine(directory, $"settings-glb-{size.Width}-{size.Height}.png"));
                scroll.Offset = new Vector(0, scroll.Extent.Height); settings.UpdateLayout();
                Capture(settings, Path.Combine(directory, $"settings-mappings-{size.Width}-{size.Height}.png"));
                scroll.Offset = default;
                foreach (var action in new[] { "click", "held" })
                {
                    Find<ComboBox>("GlbPreviewAction").SelectedIndex = GlbPetDraft.Actions.ToList().IndexOf(action);
                    await Until(() => !editor.IsPreviewLoading); settings.UpdateLayout();
                    // Keep the chosen action's controls visible on short windows.
                    Find<StackPanel>("GlbActionPane").BringIntoView(); await Task.Delay(100);
                    Capture(settings, Path.Combine(directory, $"settings-{action}-{size.Width}-{size.Height}.png"));
                }
                Find<ComboBox>("GlbPreviewAction").SelectedIndex = 0;
                await Until(() => !editor.IsPreviewLoading); scroll.Offset = default;
            }
            var originalTheme = runtime.Settings.Theme;
            try
            {
                settings.MinWidth = 640; settings.MinHeight = 560; settings.Width = 860; settings.Height = 680;
                foreach (var theme in DesignSystem.Themes)
                {
                    runtime.SetTheme(theme.Id); await Task.Delay(100); settings.UpdateLayout();
                    var clip = Find<ComboBox>("GlbClip_idle"); var heading = Find<ComboBox>("GlbHeading_idle");
                    if (heading.TranslatePoint(default, settings)!.Value.Y <= clip.TranslatePoint(default, settings)!.Value.Y)
                        throw new InvalidOperationException("Action heading is not below the animation selector.");
                    if (settings.GetVisualDescendants().OfType<Control>().Any(c => c.Name is "GlbAdvanced" or "GlbRoot" or "PetBuilderFormat"))
                        throw new InvalidOperationException("Removed builder controls are still present.");
                    Capture(settings, Path.Combine(directory, $"action-editor-{theme.Id}.png"));
                }
            }
            finally { runtime.SetTheme(originalTheme); }
        }
        finally { settings.Hide(); }
    }
    private static async Task Until(Func<bool> ready, int timeout = 15000)
    {
        for (var i = 0; i < timeout / 25 && !ready(); i++) await Task.Delay(25);
        if (!ready()) throw new TimeoutException("GLB diagnostic did not finish.");
    }
    private static void Capture(Control control, string path, double scale = 1)
    {
        var size = control.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scale)), Math.Max(1, (int)Math.Ceiling(size.Height * scale))), new Vector(96 * scale, 96 * scale));
        bitmap.Render(control); bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }
}
