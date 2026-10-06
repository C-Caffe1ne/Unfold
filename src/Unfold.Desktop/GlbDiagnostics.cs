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
        Window? window = null; GlbPetView? page = null;
        try
        {
            runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            await runtime.Start(true, true); runtime.Reset();
            window = new Window { Width = 620, Height = 850, MinWidth = 480, MinHeight = 560, Background = Ui.Background };
            page = new GlbPetView(window, runtime.Library, runtime.SelectInstalledCharacter, () => Task.FromResult<string?>(file));
            window.Content = Ui.PageFrame(window, page, inset: 10); AppRuntime.PrepareDiagnosticWindow(window); window.Show();
            T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
            Find<Button>("OpenGlbPet").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
            Find<Button>("SaveGlbPet").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => !page.IsBusy && runtime.Selected?.IsGlb == true);
            var selected = runtime.Selected!; var pet = runtime.ActivePet!;
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
            if (!reopened.IsGlb || reopened.Manifest.Animations["idle"].ModelClip != selected.Manifest.Animations["idle"].ModelClip)
                throw new InvalidOperationException("GLB mapping did not persist.");
            AtomicFile.Write(Path.Combine(directory, "result.json"), JsonSerializer.SerializeToUtf8Bytes(new
            { success = true, model = Path.GetFileName(file), triangles = selected.Model.TriangleCount, animations = selected.Model.Animations,
                playback = clips, importedThroughUi = true, persisted = true, hiddenPetStayedHidden = true,
                offScreenWindows = true, physicalInput = false, os = Environment.OSVersion.ToString() }, CharacterLibrary.JsonOptions));
            page.Dispose(); window.Close(); await runtime.Quit();
        }
        catch (Exception e)
        {
            AtomicFile.Write(Path.Combine(directory, "result.json"), JsonSerializer.SerializeToUtf8Bytes(new { success = false, error = e.ToString() }, CharacterLibrary.JsonOptions));
            AppPaths.Log(e); page?.Dispose(); window?.Close(); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    private static async Task Until(Func<bool> ready, int timeout = 15000)
    {
        for (var i = 0; i < timeout / 25 && !ready(); i++) await Task.Delay(25);
        if (!ready()) throw new TimeoutException("GLB diagnostic did not finish.");
    }
    private static void Capture(Control control, string path)
    {
        var size = control.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width)), Math.Max(1, (int)Math.Ceiling(size.Height))), new Vector(96, 96));
        bitmap.Render(control); bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }
}
