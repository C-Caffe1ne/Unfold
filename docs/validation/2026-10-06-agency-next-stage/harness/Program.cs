using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace AgencySoak;

internal static class Entry
{
    public static Config Configuration = null!;
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Configuration = Config.Parse(args);
            Directory.CreateDirectory(Configuration.Root);
            // Always a newly-created per-run directory. Never accept an existing user profile.
            Directory.CreateDirectory(Configuration.Profile);
            if (Directory.EnumerateFileSystemEntries(Configuration.Profile).Any())
                throw new InvalidOperationException("Soak profile must be empty.");
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Configuration.Profile);
            Environment.SetEnvironmentVariable("AVALONIA_TELEMETRY_OPTOUT", "1");
            using var profileLock = new FileStream(Path.Combine(Configuration.Profile, ".instance.lock"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            Console.WriteLine($"SOAK_ROOT={Configuration.Root}");
            // Use exactly the product's OS rendering options, without entering its purchase UI.
            return Unfold.Desktop.Program.ConfigureRendering(AppBuilder.Configure<SoakApplication>().UsePlatformDetect())
                .LogToTrace().StartWithClassicDesktopLifetime([]);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (Configuration is not null)
                File.WriteAllText(Path.Combine(Configuration.Root, "startup-error.txt"), error.ToString());
            return 1;
        }
    }
}

internal sealed record Config(string Root, double Seconds, double Warmup, double Sample, string[] Models)
{
    public string Profile => Path.Combine(Root, "profile");
    public static Config Parse(string[] args)
    {
        double seconds = 1800, warmup = 120, sample = 5;
        var models = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--seconds" or "--warmup" or "--sample")
            {
                var name = args[i]; var value = double.Parse(args[++i], CultureInfo.InvariantCulture);
                if (name == "--seconds") seconds = value;
                else if (name == "--warmup") warmup = value;
                else sample = value;
            }
            else models.Add(Path.GetFullPath(args[i]));
        }
        if (models.Count < 2 || models.Any(m => !File.Exists(m)))
            throw new ArgumentException("Supply at least two existing GLB files.");
        if (!double.IsFinite(seconds) || seconds <= 0 || !double.IsFinite(warmup) || warmup < 0 || warmup >= seconds || !double.IsFinite(sample) || sample < 1)
            throw new ArgumentException("Invalid duration, warmup, or sample interval.");
        var root = Path.Combine("/tmp/unfold-agency-soak-20261006/runs", DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        return new(root, seconds, warmup, sample, models.ToArray());
    }
}

public sealed class SoakApplication : Application
{
    public override void Initialize() => DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new InvalidOperationException("Native desktop lifetime required.");
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async () => await new SoakRunner(Entry.Configuration, desktop).Run());
        base.OnFrameworkInitializationCompleted();
    }
}

internal sealed record UiState(string Character, bool Glb, string Action, string PointerPhase, bool Visible,
    int RenderedPixels, int TargetPixels, double Scaling, int WindowX, int WindowY, int FrameIndex, bool Rendering, ulong ImageFingerprint);

internal sealed class SoakRunner
{
    private readonly Config cfg;
    private readonly IClassicDesktopStyleApplicationLifetime desktop;
    private readonly Stopwatch elapsed = new();
    private readonly CancellationTokenSource cancel = new();
    private readonly object fileGate = new();
    private readonly List<object> imports = [];
    private readonly List<Task> reactions = [];
    private AppRuntime? runtime;
    private volatile UiState? state;
    private string stage = "setup";
    private long heartbeatTicks;
    private int playbackFailures, events, modelSwitches, twoDSwitches, hideShowChecks, pointerChecks, actionChecks;
    private double maxHeartbeatGap;
    private readonly Dictionary<string, Liveness> liveness = new();
    // Avoid sampling at an exact 1-second multiple of common animation periods.
    private readonly DispatcherTimer heartbeat = new() { Interval = TimeSpan.FromMilliseconds(777) };
    private static readonly FieldInfo IndexField = typeof(AnimationView).GetField("index", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo RenderingField = typeof(AnimationView).GetField("renderingLive", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ImageField = typeof(AnimationView).GetField("liveImage", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private sealed class Liveness
    {
        public int Observations { get; set; }
        public int FrameIndexChanges { get; set; }
        public int ImageFingerprintChanges { get; set; }
        public int LastIndex { get; set; }
        public ulong LastFingerprint { get; set; }
        public int MaxRenderedPixels { get; set; }
        public double Scaling { get; set; }
    }
    public SoakRunner(Config cfg, IClassicDesktopStyleApplicationLifetime desktop) { this.cfg = cfg; this.desktop = desktop; }

    public async Task Run()
    {
        Task? samples = null;
        var successful = false;
        string? errorText = null;
        try
        {
            Write("environment.json", new
            {
                utcStarted = DateTimeOffset.UtcNow, os = Environment.OSVersion.ToString(), processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                processorCount = Environment.ProcessorCount, framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                appAssembly = typeof(AppRuntime).Assembly.Location, appAssemblySha256 = Hash(typeof(AppRuntime).Assembly.Location),
                appVersion = AppRelease.Version, appDisplayVersion = AppRelease.DisplayVersion,
                sourceBaseline = "1a74cbf2720937b3e4712f821f481603064281de", cfg,
                nativeDesktop = true, productRenderingOptions = true, diagnosticPurchaseBypass = true,
                physicalInput = false, actualWindows = OperatingSystem.IsWindows(), forcedGcDuringMeasurement = false,
                importThroughUi = false, launcherVelopackPath = false,
                measuredScope = "AppRuntime + native PetWindow + product AnimationView, imported through GlbPetDraft.Save; automatic internal action/pointer methods"
            });
            runtime = new AppRuntime(desktop);
            runtime.ConfirmActionOverride = (_, _, _) => Task.FromResult(0);
            await runtime.Start(true, true);
            runtime.Stop();
            runtime.HideSettingsForDiagnostics();
            await runtime.UpdateSettings(runtime.Settings with { ReminderSoundsEnabled = false, PetScalePercent = 100, ShowPet = true });
            var ids = new List<string>();
            foreach (var model in cfg.Models)
            {
                var importWatch = Stopwatch.StartNew();
                var imported = await Task.Run(() =>
                {
                    var draft = new GlbPetDraft(model);
                    var detail = new { file = model, fileBytes = new FileInfo(model).Length, sha256 = Hash(model), triangles = draft.Model.TriangleCount,
                        animations = draft.Model.Animations, actionSettings = draft.Mappings.ToDictionary(kv => kv.Key, kv => kv.Value) };
                    return (Package: draft.Save(runtime.Library), Detail: detail);
                });
                ids.Add(imported.Package.Manifest.Id);
                imports.Add(new { imported.Detail, installedId = imported.Package.Manifest.Id, durationMs = importWatch.Elapsed.TotalMilliseconds });
            }
            Write("imports.json", imports);
            await runtime.Reload();
            var twoD = runtime.Characters.FirstOrDefault(c => !c.IsGlb) ?? throw new InvalidOperationException("A bundled 2D control pet is required.");
            var pet = runtime.ActivePet ?? throw new InvalidOperationException("Runtime did not create PetWindow.");
            pet.PetView.PlaybackFailed += failure => { playbackFailures++; Event("playback-failure", new { error = failure.ToString() }); };
            heartbeatTicks = Stopwatch.GetTimestamp();
            heartbeat.Tick += (_, _) =>
            {
                var tick = Stopwatch.GetTimestamp();
                maxHeartbeatGap = Math.Max(maxHeartbeatGap, (tick - heartbeatTicks) / (double)Stopwatch.Frequency);
                heartbeatTicks = tick;
                UpdateState();
            };
            await Select(ids[0]);
            await Until(() => pet.PetView.RenderedPixelSize == pet.PetView.TargetPixelSize && pet.PetView.RenderedPixelSize > 0, 30);
            Capture(pet.PetView, "start-pet.png", pet.RenderScaling);
            elapsed.Start(); heartbeat.Start();
            Event("measurement-start", new { elapsedSeconds = elapsed.Elapsed.TotalSeconds });
            samples = Task.Run(SampleLoop);
            var cycle = 0;
            while (elapsed.Elapsed.TotalSeconds < cfg.Seconds)
            {
                stage = elapsed.Elapsed.TotalSeconds < cfg.Warmup ? "warmup" : "measurement";
                var id = ids[cycle % ids.Count];
                await Select(id); modelSwitches++;
                await Hold(7);
                await Action("click", 4);
                if (pet.HasPointerArt)
                {
                    pet.BeginCompanionPress();
                    await Hold(.4); pet.AdvanceCompanion(.4);
                    if (pet.PointerPhase is not (PetPointerPhase.Pickup or PetPointerPhase.Held))
                        throw new InvalidOperationException("GLB pickup did not enter pointer playback.");
                    Event("pointer-hold", new { character = id, phase = pet.PointerPhase.ToString() });
                    await Hold(5);
                    if (!pet.ReleaseCompanionPress(false)) throw new InvalidOperationException("GLB pointer release was not handled.");
                    pointerChecks++; await Hold(4);
                }
                foreach (var action in new[] { "hover", "walk", "celebrate" }) await Action(action, 4);
                await runtime.HidePet(); UpdateState();
                if (pet.IsVisible) throw new InvalidOperationException("Hidden pet remained visible.");
                await Hold(1);
                await runtime.UpdateSettings(runtime.Settings with { ShowPet = true }); PlaceOnScreen();
                if (!pet.IsVisible) throw new InvalidOperationException("Pet did not resume after hide.");
                hideShowChecks++; Event("hide-show", new { character = id });
                await Hold(4);
                // A short 2D dwell is a lifetime/memory control, not the dominant workload.
                await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = twoD.Manifest.Id });
                PlaceOnScreen(); twoDSwitches++; Event("2d-control", new { character = twoD.Manifest.Id });
                await Hold(4);
                cycle++;
            }
            elapsed.Stop();
            Capture(pet.PetView, "end-pet.png", pet.RenderScaling);
            successful = playbackFailures == 0 && modelSwitches >= cfg.Models.Length && twoDSwitches > 0 && hideShowChecks > 0;
        }
        catch (Exception error)
        {
            errorText = error.ToString(); Console.Error.WriteLine(error); Event("failure", new { error = errorText });
        }
        finally
        {
            heartbeat.Stop(); cancel.Cancel();
            if (samples is not null) { try { await samples; } catch (OperationCanceledException) { } }
            var wallSeconds = elapsed.Elapsed.TotalSeconds;
            var renderedModels = liveness.Where(item => runtime?.Characters.FirstOrDefault(c => c.Manifest.Id == item.Key)?.IsGlb == true).ToArray();
            var animationLive = renderedModels.Length > 0 && renderedModels.All(item => item.Value.FrameIndexChanges > 0 && item.Value.ImageFingerprintChanges > 0 && item.Value.MaxRenderedPixels > 0);
            if (!animationLive) successful = false;
            var productLog = Path.Combine(cfg.Profile, "unfold.log");
            var logContents = File.Exists(productLog) ? File.ReadAllText(productLog) : "";
            if (logContents.Length > 0) successful = false;
            Write("result.json", new
            {
                success = successful, completedThirtyMinutes = successful && wallSeconds >= 1800,
                requestedSeconds = cfg.Seconds, actualMeasurementSeconds = wallSeconds,
                warmupSeconds = cfg.Warmup, postWarmupSeconds = Math.Max(0, wallSeconds - cfg.Warmup),
                playbackFailures, productLogEmpty = logContents.Length == 0, events, modelSwitches, twoDSwitches,
                hideShowChecks, pointerChecks, actionChecks, maxUiHeartbeatGapSeconds = maxHeartbeatGap,
                animationLivenessVerified = animationLive, renderLiveness = liveness,
                actualMacOS = OperatingSystem.IsMacOS(), actualWindows = OperatingSystem.IsWindows(),
                physicalInput = false, nativeRendering = true, forcedGc = false,
                rssMetric = "Process.WorkingSet64", cpuMetric = "delta Process.TotalProcessorTime / monotonic wall seconds * 100; one full core = 100%",
                managedHeapMetric = "GC.GetTotalMemory(false); GC.GetGCMemoryInfo().HeapSizeBytes is last GC snapshot",
                sustainedFramesPerSecond = "not instrumented; frame index/fingerprint and UI heartbeat samples demonstrate liveness, not FPS",
                slaStatus = "UNDEFINED: baseline only; no approved memory/CPU SLA and no 95% statistical SLA claim",
                error = errorText
            });
            Console.WriteLine($"SOAK_COMPLETE success={successful} seconds={wallSeconds:F1} root={cfg.Root}");
            runtime?.Dispose(); desktop.Shutdown(successful ? 0 : 1);
        }
    }

    private async Task Select(string id)
    {
        var watch = Stopwatch.StartNew();
        await runtime!.SelectInstalledCharacter(runtime.Characters.Single(c => c.Manifest.Id == id));
        PlaceOnScreen(); UpdateState();
        if (runtime.Selected?.Manifest.Id != id || runtime.ActivePet?.IsVisible != true)
            throw new InvalidOperationException("Requested GLB did not become the visible selected pet.");
        Event("model-select", new { character = id, durationMs = watch.Elapsed.TotalMilliseconds, scaling = runtime.ActivePet.RenderScaling });
    }
    private void PlaceOnScreen()
    {
        var pet = runtime!.ActivePet!;
        var work = pet.Screens.Primary?.WorkingArea ?? throw new InvalidOperationException("A physical display is required.");
        pet.Position = new(work.X + 40, work.Bottom - (int)Math.Ceiling(pet.Height * pet.DesktopScaling) - 60);
        pet.UpdateLayout();
    }
    private async Task Action(string key, double seconds)
    {
        if (!runtime!.Selected!.Manifest.Animations.ContainsKey(key)) { Event("action-unassigned", new { key }); return; }
        var pet = runtime.ActivePet!;
        var reaction = pet.React(key); reactions.Add(reaction);
        await Hold(.1);
        if (!reaction.IsCompleted && pet.ActiveAnimation != key)
            throw new InvalidOperationException($"Requested action {key} did not start.");
        Event("action-start", new { key, character = runtime.Selected.Manifest.Id }); actionChecks++;
        await Hold(seconds);
        // Cancel infinite loop reactions using the same hide/show lifecycle used by the product.
        await runtime.HidePet();
        await runtime.UpdateSettings(runtime.Settings with { ShowPet = true }); PlaceOnScreen();
        await reaction.WaitAsync(TimeSpan.FromSeconds(10));
        reactions.Remove(reaction);
    }
    private async Task Hold(double seconds)
    {
        UpdateState();
        var remaining = cfg.Seconds - elapsed.Elapsed.TotalSeconds;
        if (elapsed.IsRunning && remaining <= 0) return;
        await Task.Delay(TimeSpan.FromSeconds(elapsed.IsRunning ? Math.Min(seconds, remaining) : seconds));
        UpdateState();
    }
    private async Task Until(Func<bool> predicate, double seconds)
    {
        var until = Stopwatch.StartNew();
        while (!predicate())
        {
            if (until.Elapsed.TotalSeconds > seconds) throw new TimeoutException("Native GLB render did not become ready.");
            await Task.Delay(50);
        }
    }
    private void UpdateState()
    {
        var pet = runtime?.ActivePet;
        if (pet is null) return;
        var image = ImageField.GetValue(pet.PetView) as PixelImage;
        ulong fingerprint = 1469598103934665603;
        if (image is not null)
            for (var i = 0; i < image.Pixels.Length; i += Math.Max(1, image.Pixels.Length / 128))
                fingerprint = unchecked((fingerprint ^ image.Pixels[i]) * 1099511628211);
        var next = new UiState(runtime!.Selected?.Manifest.Id ?? "", runtime.Selected?.IsGlb == true, pet.ActiveAnimation, pet.PointerPhase.ToString(), pet.IsVisible,
            pet.PetView.RenderedPixelSize, pet.PetView.TargetPixelSize, pet.RenderScaling, pet.Position.X, pet.Position.Y,
            (int)IndexField.GetValue(pet.PetView)!, (bool)RenderingField.GetValue(pet.PetView)!, fingerprint);
        if (next.Visible && next.Glb && image is not null)
        {
            if (!liveness.TryGetValue(next.Character, out var live)) liveness[next.Character] = live = new();
            if (live.Observations > 0)
            {
                if (live.LastIndex != next.FrameIndex) live.FrameIndexChanges++;
                if (live.LastFingerprint != fingerprint) live.ImageFingerprintChanges++;
            }
            live.Observations++; live.LastIndex = next.FrameIndex; live.LastFingerprint = fingerprint;
            live.MaxRenderedPixels = Math.Max(live.MaxRenderedPixels, next.RenderedPixels); live.Scaling = next.Scaling;
        }
        state = next;
    }
    private async Task SampleLoop()
    {
        using var process = Process.GetCurrentProcess();
        using var output = new StreamWriter(Path.Combine(cfg.Root, "samples.jsonl"), false) { AutoFlush = true };
        var lastTime = elapsed.Elapsed.TotalSeconds;
        var lastCpu = process.TotalProcessorTime.TotalSeconds;
        void Append()
        {
            process.Refresh();
            var now = elapsed.Elapsed.TotalSeconds; var cpu = process.TotalProcessorTime.TotalSeconds;
            var gc = GC.GetGCMemoryInfo();
            output.WriteLine(JsonSerializer.Serialize(new
            {
                utc = DateTimeOffset.UtcNow, elapsedSeconds = now, phase = now < cfg.Warmup ? "warmup" : "measurement", stage,
                rssBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
                managedBytes = GC.GetTotalMemory(false), heapAfterLastGcBytes = gc.HeapSizeBytes, fragmentedAfterLastGcBytes = gc.FragmentedBytes,
                allocatedBytes = GC.GetTotalAllocatedBytes(false), gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
                cpuSeconds = cpu, cpuOneCorePercent = now > lastTime ? (cpu - lastCpu) / (now - lastTime) * 100 : 0,
                cpuMachinePercent = now > lastTime ? (cpu - lastCpu) / (now - lastTime) * 100 / Environment.ProcessorCount : 0,
                threadCount = process.Threads.Count, uiHeartbeatAgeSeconds = (Stopwatch.GetTimestamp() - Interlocked.Read(ref heartbeatTicks)) / (double)Stopwatch.Frequency,
                playbackFailures, ui = state
            }));
            lastTime = now; lastCpu = cpu;
        }
        Append();
        try
        {
            while (!cancel.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(cfg.Sample), cancel.Token);
                Append();
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
        finally { Append(); }
    }
    private void Capture(Control view, string file, double scale)
    {
        var size = view.Bounds.Size;
        using var bitmap = new RenderTargetBitmap(new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scale)), Math.Max(1, (int)Math.Ceiling(size.Height * scale))), new Vector(96 * scale, 96 * scale));
        bitmap.Render(view); bitmap.Save(Path.Combine(cfg.Root, file), PngBitmapEncoderOptions.Default);
    }
    private void Event(string kind, object data)
    {
        lock (fileGate)
        {
            events++;
            File.AppendAllText(Path.Combine(cfg.Root, "events.jsonl"), JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, elapsedSeconds = elapsed.Elapsed.TotalSeconds, kind, data }) + "\n");
        }
    }
    private void Write(string name, object value) => File.WriteAllText(Path.Combine(cfg.Root, name), JsonSerializer.Serialize(value, Json));
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
}
