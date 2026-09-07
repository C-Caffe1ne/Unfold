using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
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
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = "default-cat", ShowPet = true });
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
    private static void Capture(Window window, string path)
    {
        var size = window.ClientSize;
        using var image = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)size.Width), Math.Max(1, (int)size.Height)), new Vector(96, 96));
        image.Render(window); image.Save(path, PngBitmapEncoderOptions.Default);
    }
}
