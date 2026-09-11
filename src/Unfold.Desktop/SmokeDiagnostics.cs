using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Opt-in packaged-app verification with isolated data and off-screen windows.</summary>
internal static class SmokeDiagnostics
{
    public static async Task Run(AppRuntime runtime, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var elapsed = Stopwatch.StartNew();
        var directory = Path.Combine(AppPaths.DataRoot, "verification"); Directory.CreateDirectory(directory);
        try
        {
            await runtime.Start(false, true);
            await Task.Delay(250);
            var startupMs = elapsed.Elapsed.TotalMilliseconds;
            if (runtime.Characters.Count == 0 || runtime.ActivePet is null) throw new InvalidOperationException("Startup did not load the character/pet.");
            Capture(desktop.MainWindow!, Path.Combine(directory, "settings.png"));
            Capture(runtime.ActivePet, Path.Combine(directory, "pet.png"));
            await ClickThroughCharacterPicker(runtime, desktop.MainWindow!);
            var doc = new PixelDocument(32, 32) { Name = "Smoke verification" };
            doc.Draw(PixelTool.Rectangle, new(5, 5), new(25, 25), 0xFFF4B860, 1, 0, 0);
            var saved = runtime.Library.Save(doc); await runtime.Reload();
            await runtime.OpenEditor(saved);
            var editor = runtime.ActiveEditor ?? throw new InvalidOperationException("Editor did not open.");
            editor.Session.BeginStroke(new(1, 1)); editor.Session.ContinueStroke(new(25, 1)); editor.Session.EndStroke();
            if (!await editor.SaveDocument()) throw new InvalidOperationException("Editor save failed.");
            if (!runtime.Library.OpenForEditing(saved.Manifest.Id).Document.ContentEquals(editor.Session.Document)) throw new InvalidOperationException("Saved drawing differs from reopened source.");
            await Task.Delay(250); Capture(editor, Path.Combine(directory, "editor.png"));
            await runtime.ShowReminder();
            if (runtime.ActiveReminder is null) throw new InvalidOperationException("Reminder did not open.");
            await Task.Delay(150); Capture(runtime.ActiveReminder, Path.Combine(directory, "reminder.png"));
            runtime.ActiveReminder.Close(); editor.CloseAfterApproval();
            runtime.HideSettingsForDiagnostics();
            // Every bundled companion has to survive a live swap: the selection sticks,
            // its idle clip decodes, and the pet stays on screen throughout.
            foreach (var character in runtime.Characters.Where(c => c.IsBuiltIn))
            {
                await runtime.SelectCharacter(character);
                if (runtime.Selected != character) throw new InvalidOperationException($"Swapping to {character.Manifest.Id} did not take effect.");
                if ((await runtime.Clip("idle")).Count == 0) throw new InvalidOperationException($"{character.Manifest.Id} has no idle frames after the swap.");
                if (runtime.ActivePet is null) throw new InvalidOperationException($"Swapping to {character.Manifest.Id} dropped the pet.");
            }
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = AppSettings.DefaultCharacterId, ShowPet = true });
            await Task.Delay(1000);
            var process = Process.GetCurrentProcess(); var cpuBefore = process.TotalProcessorTime; var sample = Stopwatch.StartNew();
            await Task.Delay(2000); process.Refresh();
            var report = new { success = true, startupMs, totalMs = elapsed.Elapsed.TotalMilliseconds,
                workingSetBytes = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false),
                oneCoreCpuPercent = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / sample.Elapsed.TotalMilliseconds * 100,
                characters = runtime.Characters.Count, imageFiles = Directory.GetFiles(directory, "*.png").Length,
                idleSeconds = PlatformServices.IdleTime().TotalSeconds, os = Environment.OSVersion.ToString(), framework = Environment.Version.ToString() };
            AtomicFile.Write(Path.Combine(directory, "smoke.json"), JsonSerializer.SerializeToUtf8Bytes(report, CharacterLibrary.JsonOptions));
            await runtime.Quit();
        }
        catch (Exception error)
        {
            AtomicFile.Write(Path.Combine(directory, "smoke.json"), JsonSerializer.SerializeToUtf8Bytes(new { success = false, error = error.ToString() }, CharacterLibrary.JsonOptions));
            AppPaths.Log(error); runtime.Dispose(); desktop.Shutdown(1);
        }
    }
    /// <summary>Swapping is what the picker is for, so drive it the way a user does:
    /// through the real card button, not through the settings record behind it.</summary>
    private static async Task ClickThroughCharacterPicker(AppRuntime runtime, Window settings)
    {
        var cards = settings.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is CharacterPackage).ToArray();
        if (cards.Length != runtime.Characters.Count)
            throw new InvalidOperationException($"The picker offers {cards.Length} of {runtime.Characters.Count} characters.");
        if (cards.FirstOrDefault(button => !ReferenceEquals(button.Tag, runtime.Selected)) is not { } other) return;
        var wanted = (CharacterPackage)other.Tag!;
        other.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        for (var i = 0; i < 50 && runtime.Selected != wanted; i++) await Task.Delay(20);
        if (runtime.Selected != wanted) throw new InvalidOperationException($"Clicking {wanted.Manifest.Name} in the picker did not swap the character.");
    }
    private static void Capture(Window window, string path)
    {
        var size = window.ClientSize;
        using var image = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)), new Vector(96, 96));
        image.Render(window); image.Save(path, PngBitmapEncoderOptions.Default);
    }
}
