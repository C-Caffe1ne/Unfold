using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Unfold.Core;
using Unfold.Desktop;

internal static class Entry
{
    internal static string Model = "", Output = "";
    [STAThread] public static int Main(string[] args)
    {
        if (args.Length != 2 || !File.Exists(args[0]) || File.Exists(args[1]))
        {
            Console.Error.WriteLine("Usage: <model.glb> <new-report.json>"); return 2;
        }
        Model = Path.GetFullPath(args[0]); Output = Path.GetFullPath(args[1]);
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(Path.GetTempPath(), "Unfold-glb-memory-" + Guid.NewGuid().ToString("N")));
        return Program.ConfigureRendering(AppBuilder.Configure<ProbeApp>().UsePlatformDetect()).StartWithClassicDesktopLifetime([]);
    }
}
public sealed class ProbeApp : Application
{
    public override void Initialize() => DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async () =>
        {
            using var runtime = new AppRuntime(desktop);
            var exitCode = 0;
            try
            {
                await runtime.Start(true, true); runtime.Stop();
                await Import(runtime);
                var pet = runtime.ActivePet!;
                var screen = pet.Screens.Primary!.WorkingArea;
                pet.Position = new(screen.X + 32, screen.Y + 200);
                var phases = new List<object>();
                foreach (var scenario in new[] { "pet-100", "pet-150", "pet-and-settings", "hidden" })
                {
                    if (scenario == "pet-150") await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 150 });
                    if (scenario == "pet-and-settings")
                    {
                        runtime.ShowSettings(); desktop.MainWindow!.Position = new(screen.X + 400, screen.Y + 40);
                    }
                    if (scenario == "hidden")
                    {
                        ((SettingsWindow)desktop.MainWindow!).HideToTray();
                        await runtime.UpdateSettings(runtime.Settings with { ShowPet = false });
                    }
                    await Task.Delay(4000);
                    var samples = new List<object>();
                    var allocated = GC.GetTotalAllocatedBytes();
                    var start = Stopwatch.GetTimestamp(); var gc = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                    for (var i = 0; i < 16; i++)
                    {
                        await Task.Delay(1000);
                        using var process = Process.GetCurrentProcess();
                        samples.Add(new { seconds = Stopwatch.GetElapsedTime(start).TotalSeconds, workingSetBytes = process.WorkingSet64, managedBytes = GC.GetTotalMemory(false), committedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes });
                    }
                    phases.Add(new { scenario, renderPixels = pet.PetView.RenderedPixelSize, scale = pet.RenderScaling, allocatedBytes = GC.GetTotalAllocatedBytes() - allocated, genCollections = Enumerable.Range(0,3).Select(g => GC.CollectionCount(g) - gc[g]).ToArray(), samples });
                }
                File.WriteAllText(Entry.Output, JsonSerializer.Serialize(new { success = true, coreSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(GlbModel).Assembly.Location))), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, phases }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e)
            {
                exitCode = 1;
                File.WriteAllText(Entry.Output, JsonSerializer.Serialize(new { success = false, error = e.ToString() }));
            }
            finally { runtime.Dispose(); desktop.Shutdown(exitCode); }
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Import(AppRuntime runtime)
    {
        var package = await Task.Run(() => new GlbPetDraft(Entry.Model).Save(runtime.Library));
        await runtime.SelectInstalledCharacter(package);
    }
}
