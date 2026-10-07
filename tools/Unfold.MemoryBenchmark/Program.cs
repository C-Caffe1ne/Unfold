using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

internal static class Benchmark
{
    public static string Output = "", Mode = "", Phase = "initializing", MediaFixture = "";
    public static (string Name, string Path)[] GlbInputs = [];
    public static Stopwatch Watch = Stopwatch.StartNew();
    public static double DurationFactor = 1;
    public static IReadOnlyDictionary<string, object> GcConfiguration = new Dictionary<string, object>();
    public static int? ExpectedConserveMemory;
    public static object? ActualConserveMemory;
    public static double? RequestedScreenScale;
    public static Screen? TargetScreen;
    public static object[] ScreenInventory = [];
    public static long? GpuCacheBudgetBytes;
    public static bool ExplicitSkiaOptions;
    public static string[] NativeRenderingModes = [];
    public static string? PlatformGraphicsType;
    public static readonly List<object> Events = [];
    private static StreamWriter? samples;
    private static CancellationTokenSource? sampling;
    private static Task? sampler;
    [DllImport("libunfoldmemory.dylib")] private static extern int unfold_memory([Out] ulong[] metrics);
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    [StructLayout(LayoutKind.Sequential)] private struct DisplayRect { public double X, Y, Width, Height; }
    [DllImport(CoreGraphics)] private static extern int CGGetActiveDisplayList(uint maximum, [Out] uint[] displays, out uint count);
    [DllImport(CoreGraphics)] private static extern DisplayRect CGDisplayBounds(uint display);
    [DllImport(CoreGraphics)] private static extern IntPtr CGDisplayCopyDisplayMode(uint display);
    [DllImport(CoreGraphics)] private static extern nuint CGDisplayModeGetWidth(IntPtr mode);
    [DllImport(CoreGraphics)] private static extern nuint CGDisplayModeGetPixelWidth(IntPtr mode);
    [DllImport(CoreGraphics)] private static extern void CGDisplayModeRelease(IntPtr mode);
    private static double NativeDisplayScale(Screen screen)
    {
        // On macOS Screen.Scaling represents desktop coordinates and can be 1
        // on a Retina display. Resolve actual backing pixels from CoreGraphics.
        var displays = new uint[32];
        if (CGGetActiveDisplayList((uint)displays.Length, displays, out var count) != 0)
            throw new InvalidOperationException("Cannot enumerate native macOS displays.");
        foreach (var display in displays.Take((int)count))
        {
            var bounds = CGDisplayBounds(display);
            if (Math.Abs(bounds.X - screen.Bounds.X) > 1 || Math.Abs(bounds.Y - screen.Bounds.Y) > 1 ||
                Math.Abs(bounds.Width - screen.Bounds.Width) > 1 || Math.Abs(bounds.Height - screen.Bounds.Height) > 1) continue;
            var mode = CGDisplayCopyDisplayMode(display);
            if (mode == IntPtr.Zero) throw new InvalidOperationException("Cannot read native display mode.");
            try { return (double)CGDisplayModeGetPixelWidth(mode) / (double)CGDisplayModeGetWidth(mode); }
            finally { CGDisplayModeRelease(mode); }
        }
        throw new InvalidOperationException("Cannot match an Avalonia screen to a native macOS display: " + screen.Bounds);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("This native benchmark requires macOS.");
        if (args.Length < 3) throw new ArgumentException("Usage: output complete|media|2d|soak factor [media-fixture] [hikari.glb] [kazusa.glb]");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        Output = Path.GetFullPath(args[0]); Mode = args[1]; DurationFactor = double.Parse(args[2]);
        if (Environment.GetEnvironmentVariable("UNFOLD_MEMORY_SCREEN_SCALE") is { Length: > 0 } screenScale)
        {
            RequestedScreenScale = double.Parse(screenScale);
            if (!double.IsFinite(RequestedScreenScale.Value) || RequestedScreenScale <= 0)
                throw new ArgumentException("UNFOLD_MEMORY_SCREEN_SCALE must be a positive native display scale.");
        }
        if (Mode is not ("complete" or "media" or "2d" or "soak") || !double.IsFinite(DurationFactor) || DurationFactor <= 0)
            throw new ArgumentException("Select a supported mode and a positive finite duration factor.");
        if (Mode == "soak" && DurationFactor != 1) throw new ArgumentException("The 30-minute soak requires duration factor 1.");
        if (File.Exists(Path.Combine(Output, "samples.csv")) || File.Exists(Path.Combine(Output, "run.json")))
            throw new IOException("Choose a fresh output directory.");
        var profile = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        if (string.IsNullOrWhiteSpace(profile) || Directory.Exists(profile) && Directory.EnumerateFileSystemEntries(profile).Any())
            throw new IOException("UNFOLD_DATA_DIR must name a new empty test profile.");
        if (Mode == "media")
        {
            MediaFixture = Path.GetFullPath(args.Length > 3 && args[3].Length > 0 ? args[3] : throw new ArgumentException("Pass the generated media fixture directory."));
            foreach (var file in new[] { "idle.gif", "spritesheet.png", "character.json" })
                if (!File.Exists(Path.Combine(MediaFixture, file))) throw new FileNotFoundException("Missing generated fixture file.", file);
        }
        if (Mode is "complete" or "soak")
        {
            string Fixture(int index, string name) => Path.GetFullPath(args.Length > index && args[index].Length > 0 ? args[index] : throw new ArgumentException("Pass the " + name + " GLB fixture path."));
            GlbInputs = [("Hikari", Fixture(4, "Hikari")), ("Kazusa", Fixture(5, "Kazusa"))];
            foreach (var input in GlbInputs) if (!File.Exists(input.Path)) throw new FileNotFoundException("Missing GLB fixture.", input.Path);
        }
        Directory.CreateDirectory(Output);
        foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
        {
            var key = variable.Key.ToString()!;
            if ((key.StartsWith("DOTNET_GCHeapHardLimit", StringComparison.OrdinalIgnoreCase) || key.StartsWith("COMPlus_GCHeapHardLimit", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(variable.Value?.ToString()))
                throw new InvalidOperationException("Memory benchmark must run without a heap cap: " + key);
        }
        using (var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Unfold.Tests.runtimeconfig.json"))))
        {
            if (config.RootElement.GetProperty("runtimeOptions").TryGetProperty("configProperties", out var properties) && properties.TryGetProperty("System.GC.ConserveMemory", out var value))
                ExpectedConserveMemory = value.GetInt32();
        }
        GcConfiguration = GC.GetConfigurationVariables();
        foreach (var setting in GcConfiguration)
            if (setting.Key.Contains("HeapHardLimit", StringComparison.OrdinalIgnoreCase) && Convert.ToDecimal(setting.Value, System.Globalization.CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("An active GC heap cap invalidates this benchmark: " + setting.Key);
        ActualConserveMemory = GcConfiguration.GetValueOrDefault("GCConserveMem") ?? GcConfiguration.GetValueOrDefault("GCConserveMemory") ?? GcConfiguration.GetValueOrDefault("System.GC.ConserveMemory");
        if (ExpectedConserveMemory is { } expected && (ActualConserveMemory is null || Convert.ToInt64(ActualConserveMemory) != expected))
            throw new InvalidOperationException("Product GC policy did not become active: " + JsonSerializer.Serialize(GcConfiguration));
        Console.WriteLine("GC configuration: " + JsonSerializer.Serialize(new { expectedConserveMemory = ExpectedConserveMemory, actual = GcConfiguration }));
        return Unfold.Desktop.Program.ConfigureRendering(AppBuilder.Configure<MemoryApp>().UsePlatformDetect())
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
    }
    public static void CaptureRenderingPolicy()
    {
        // Read the options installed by the selected product's rendering method.
        // Reflection avoids compiling against Avalonia's unstable locator API.
        var locatorType = typeof(AvaloniaObject).Assembly.GetType("Avalonia.AvaloniaLocator", throwOnError: true)!;
        var locator = locatorType.GetProperty("Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var getter = locator.GetType().GetMethod("GetService", [typeof(Type)])
            ?? locator.GetType().GetInterfaces().Select(type => type.GetMethod("GetService", [typeof(Type)])).First(method => method is not null)!;
        var installedOptions = getter.Invoke(locator, [typeof(SkiaOptions)]) as SkiaOptions;
        ExplicitSkiaOptions = installedOptions is not null;
        // Match UseSkia's read-only default fallback for older products that did
        // not install explicit options. Do not inject options into the product.
        var options = installedOptions ?? new SkiaOptions();
        GpuCacheBudgetBytes = options.MaxGpuResourceSizeBytes;
        var nativeOptions = getter.Invoke(locator, [typeof(AvaloniaNativePlatformOptions)]) as AvaloniaNativePlatformOptions;
        NativeRenderingModes = nativeOptions?.RenderingMode.Select(mode => mode.ToString()).ToArray() ?? [];
        var graphicsInterface = typeof(AvaloniaObject).Assembly.GetType("Avalonia.Platform.IPlatformGraphics", throwOnError: true)!;
        PlatformGraphicsType = getter.Invoke(locator, [graphicsInterface])?.GetType().FullName;
        if (Environment.GetEnvironmentVariable("UNFOLD_MEMORY_EXPECT_SOFTWARE") == "1" &&
            (!NativeRenderingModes.SequenceEqual(["Software"]) || PlatformGraphicsType is not null))
            throw new InvalidOperationException("The selected product did not initialize the expected native Software rendering policy.");
        if (Environment.GetEnvironmentVariable("UNFOLD_MEMORY_EXPECTED_GPU_CACHE_BYTES") is { Length: > 0 } expected && GpuCacheBudgetBytes != long.Parse(expected))
            throw new InvalidOperationException("The actual product GPU cache budget differs from the required value.");
        Console.WriteLine("Rendering configuration: " + JsonSerializer.Serialize(new { source = typeof(Unfold.Desktop.Program).Assembly.Location, gpuCacheBudgetBytes = GpuCacheBudgetBytes, explicitSkiaOptions = ExplicitSkiaOptions, nativeRenderingModes = NativeRenderingModes, platformGraphicsType = PlatformGraphicsType, requestedScreenScale = RequestedScreenScale }));
    }
    public static void PlaceOnRequestedScreen(Window window)
    {
        if (RequestedScreenScale is not { } scale) return;
        if (TargetScreen is null)
        {
            ScreenInventory = window.Screens.All.Select(screen => (object)new { screen.DisplayName, screen.Scaling, nativeBackingScale = NativeDisplayScale(screen), bounds = screen.Bounds.ToString(), workingArea = screen.WorkingArea.ToString(), screen.IsPrimary }).ToArray();
            Console.WriteLine("Available displays: " + JsonSerializer.Serialize(ScreenInventory));
            TargetScreen = window.Screens.All.FirstOrDefault(screen => Math.Abs(NativeDisplayScale(screen) - scale) < .001)
                ?? throw new InvalidOperationException("No active native display matches requested scale " + scale);
            Console.WriteLine("Display configuration: " + JsonSerializer.Serialize(new { requestedScreenScale = RequestedScreenScale, screens = ScreenInventory }));
        }
        if (window.Screens.ScreenFromWindow(window) != TargetScreen || Math.Abs(window.RenderScaling - scale) > .001)
            window.Position = new Avalonia.PixelPoint(TargetScreen.WorkingArea.X + 40, TargetScreen.WorkingArea.Y + 40);
    }
    public static async Task<object> VerifyDisplay(AppRuntime runtime, SettingsWindow settings, object? details)
    {
        PlaceOnRequestedScreen(settings);
        if (runtime.ActivePet is { } pet) PlaceOnRequestedScreen(pet);
        if (RequestedScreenScale is { } scale)
        {
            await Until(() => Math.Abs(settings.RenderScaling - scale) < .001 && (runtime.ActivePet is not { } active || Math.Abs(active.RenderScaling - scale) < .001), 5);
            if (runtime.Selected?.IsGlb == true && runtime.ActivePet is { IsVisible: true } active)
            {
                var expectedPixels = AnimationView.PixelSizeFor(DesignSystem.PetBaseSize * runtime.Settings.PetScalePercent / 100d, scale);
                await Until(() => active.PetView.TargetPixelSize == expectedPixels && active.PetView.RenderedPixelSize == expectedPixels, 10);
            }
        }
        return new { details, requestedScreenScale = RequestedScreenScale, settingsScale = settings.RenderScaling,
            petScale = runtime.ActivePet?.RenderScaling, petPercent = runtime.Settings.PetScalePercent,
            targetPixels = runtime.Selected?.IsGlb == true ? runtime.ActivePet?.PetView.TargetPixelSize : null,
            renderedPixels = runtime.Selected?.IsGlb == true ? runtime.ActivePet?.PetView.RenderedPixelSize : null };
    }
    public static void BeginSamples()
    {
        samples = new(Path.Combine(Output, "samples.csv"));
        samples.WriteLine("elapsed_ms,phase,rss_bytes,rss_peak_bytes,physical_footprint_bytes,physical_peak_bytes,internal_bytes,compressed_bytes,managed_bytes,heap_last_gc_bytes,committed_last_gc_bytes,allocated_bytes,gen0,gen1,gen2,mach_error,cpu_total_ms,graphics_footprint_bytes,graphics_footprint_compressed_bytes,purgeable_nonvolatile_bytes,purgeable_nonvolatile_compressed_bytes,device_bytes,device_peak_bytes,reusable_bytes,external_bytes,graphics_nofootprint_bytes,graphics_nofootprint_compressed_bytes");
        sampling = new();
        sampler = Task.Run(async () =>
        {
            var native = new ulong[16];
            using var process = Process.GetCurrentProcess();
            try
            {
                while (!sampling.IsCancellationRequested)
                {
                    var error = unfold_memory(native); var gc = GC.GetGCMemoryInfo();
                    samples.WriteLine($"{Watch.Elapsed.TotalMilliseconds:F3},{Phase},{native[0]},{native[1]},{native[2]},{native[3]},{native[4]},{native[5]},{GC.GetTotalMemory(false)},{gc.HeapSizeBytes},{gc.TotalCommittedBytes},{GC.GetTotalAllocatedBytes(false)},{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)},{error},{process.TotalProcessorTime.TotalMilliseconds:F3}," + string.Join(',', native.Skip(6)));
                    samples.Flush();
                    await Task.Delay(200, sampling.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
    }
    public static async Task EndSamples()
    { sampling!.Cancel(); await sampler!; samples!.Dispose(); sampling.Dispose(); }
    public static void Mark(string phase, object? details = null)
    {
        Phase = phase; Events.Add(new { elapsedMs = Watch.Elapsed.TotalMilliseconds, phase, details });
        Console.WriteLine($"{Watch.Elapsed.TotalSeconds:F1}s {phase} {JsonSerializer.Serialize(details)}");
    }
    public static Task Wait(double seconds) => Task.Delay(TimeSpan.FromSeconds(seconds * DurationFactor));
    public static async Task Until(Func<bool> ready, int timeoutSeconds = 40)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.Elapsed.TotalSeconds < timeoutSeconds) await Task.Delay(50);
        if (!ready()) throw new TimeoutException("Benchmark phase did not become ready: " + Phase);
    }
    public static T Find<T>(Control root, string? name = null) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => name is null || c.Name == name);
    public static void Click(Control root, string name) => Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static void Save(object report) => File.WriteAllText(Path.Combine(Output, "run.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}

public sealed class MemoryApp : Application
{
    public override void Initialize() => DesignSystem.Install(this);
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        Dispatcher.UIThread.Post(async () =>
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            if (Benchmark.Mode == "media")
            {
                var directory = Path.Combine(AppPaths.DataRoot, "Characters", "media-stress"); Directory.CreateDirectory(directory);
                foreach (var file in Directory.EnumerateFiles(Benchmark.MediaFixture)) File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }
            new AppSettings { ShowPet = true, IdleMinutes = 60, IntervalMinutes = 240 }.Save(Path.Combine(AppPaths.DataRoot, "settings.json"));
            using var runtime = new AppRuntime(desktop) { AccountServiceFactory = () => new MockAccountService() };
            var exit = 1; var accountWindows = 0;
            using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
            {
                if (window is AccountWindow) accountWindows++;
                Benchmark.PlaceOnRequestedScreen(window);
                Dispatcher.UIThread.Post(() => Benchmark.PlaceOnRequestedScreen(window), DispatcherPriority.Loaded);
            });
            Benchmark.BeginSamples();
            try
            {
                Benchmark.CaptureRenderingPolicy();
                Benchmark.Mark("startup"); await runtime.Start(false);
                if (!runtime.AccessAllowed || runtime.ActiveAccount is not null) throw new InvalidOperationException("Mock restored entitlement did not start app normally.");
                var settings = (SettingsWindow)desktop.MainWindow!;
                if (Benchmark.Mode == "media") await RunMedia(runtime, settings);
                else if (Benchmark.Mode == "soak") await RunSoak(runtime, settings);
                else
                {
                await Mark(runtime, settings, "2d-default-home", new { character = runtime.Selected!.Manifest.Id, settings.RenderScaling, petScale = runtime.ActivePet!.RenderScaling });
                await Benchmark.Wait(20);
                settings.HideToTray(); await Mark(runtime, settings, "2d-default-pet"); await Benchmark.Wait(20);
                foreach (var character in runtime.Characters.ToArray())
                {
                    if (character.IsGlb) continue;
                    await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = character.Manifest.Id });
                    await Mark(runtime, settings, "2d-" + character.Manifest.Id);
                    await Benchmark.Wait(12);
                }
                await Mark(runtime, settings, "settings-open-hide-20");
                for (var i = 0; i < 20; i++) { runtime.ShowSettings(); await Benchmark.Wait(.5); settings.HideToTray(); await Benchmark.Wait(.5); }
                await Mark(runtime, settings, "rest-invitation"); await runtime.ShowReminder(); await Benchmark.Wait(5);
                runtime.StartBreak(); await Mark(runtime, settings, "rest-active"); await Benchmark.Wait(10);
                runtime.CompleteBreak(); await Mark(runtime, settings, "rest-completed"); await Benchmark.Wait(5);
                if (Benchmark.Mode != "2d")
                {
                    foreach (var (name, file) in Benchmark.GlbInputs)
                    {
                        if (!File.Exists(file)) throw new FileNotFoundException("Required GLB fixture is unavailable.", file);
                        runtime.ShowSettings(); Benchmark.Click(settings, "SettingsNavPacks"); await Task.Delay(150);
                        var builder = Benchmark.Find<PetBuilderView>(settings);
                        await Mark(runtime, settings, "glb-" + name + "-import", new { bytes = new FileInfo(file).Length });
                        await builder.OpenPath(file); var editor = Benchmark.Find<GlbPetView>(settings);
                        await Benchmark.Until(() => !editor.IsBusy && !editor.IsPreviewLoading);
                        await Mark(runtime, settings, "glb-" + name + "-editor"); await Benchmark.Wait(15);
                        await Mark(runtime, settings, "glb-" + name + "-save"); Benchmark.Click(settings, "SaveGlbPet");
                        await Benchmark.Until(() => !builder.IsBusy && runtime.Selected?.IsGlb == true && runtime.Selected.Manifest.Name == Path.GetFileNameWithoutExtension(file));
                        await Mark(runtime, settings, "glb-" + name + "-home-pet"); Benchmark.Click(settings, "SettingsNavTimer"); await Benchmark.Wait(15);
                        settings.HideToTray();
                        await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = 150 });
                        await Mark(runtime, settings, "glb-" + name + "-pet-150", new { runtime.ActivePet!.RenderScaling, pixels = runtime.ActivePet.PetView.TargetPixelSize });
                        await Benchmark.Wait(60);
                        runtime.ShowSettings(); Benchmark.Click(settings, "SettingsNavPacks");
                        builder = Benchmark.Find<PetBuilderView>(settings); await builder.OpenPackage(runtime.Selected!);
                        await Mark(runtime, settings, "glb-" + name + "-reopen-editor"); await Benchmark.Wait(15);
                        settings.HideToTray();
                    }
                }
                await Mark(runtime, settings, "all-hidden-retained-draft"); await runtime.UpdateSettings(runtime.Settings with { ShowPet = false }); await Benchmark.Wait(30);
                await Mark(runtime, settings, "2d-return"); await runtime.UpdateSettings(runtime.Settings with { ShowPet = true, SelectedCharacterId = "default-cat", PetScalePercent = 100 }); await Benchmark.Wait(30);
                }
                await Mark(runtime, settings, "finished"); exit = 0;
                Benchmark.Save(new { success = true, source = typeof(AppRuntime).Assembly.Location, version = AppRelease.Version,
                    os = RuntimeInformation.OSDescription, framework = RuntimeInformation.FrameworkDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    mode = Benchmark.Mode, durationFactor = Benchmark.DurationFactor, sampleIntervalMs = 200, budgetBytes = 400_000_000,
                    glbInputs = Benchmark.GlbInputs.Select(input => new { name = input.Name, path = input.Path }), mediaFixture = Benchmark.MediaFixture,
                    serverGc = System.Runtime.GCSettings.IsServerGC, gcLatencyMode = System.Runtime.GCSettings.LatencyMode.ToString(),
                    expectedConserveMemory = Benchmark.ExpectedConserveMemory, gcConfiguration = Benchmark.GcConfiguration,
                    gcConserveMemory = Benchmark.ActualConserveMemory,
                    requestedScreenScale = Benchmark.RequestedScreenScale, screens = Benchmark.ScreenInventory,
                    gpuCacheBudgetBytes = Benchmark.GpuCacheBudgetBytes, explicitSkiaOptions = Benchmark.ExplicitSkiaOptions,
                    nativeRenderingModes = Benchmark.NativeRenderingModes, platformGraphicsType = Benchmark.PlatformGraphicsType,
                    renderingPolicySource = typeof(Unfold.Desktop.Program).Assembly.Location,
                    elapsedSeconds = Benchmark.Watch.Elapsed.TotalSeconds, dataRoot = AppPaths.DataRoot, accountWindows, events = Benchmark.Events,
                    boundary = "Actual macOS native Avalonia rendering. Mock restored session/purchase entitlement. UI actions dispatched programmatically. No live credentials/backend. No forced GC/heap cap/process kill." });
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Benchmark.Save(new { success = false, error = ex.ToString(), events = Benchmark.Events }); }
            finally { await Benchmark.EndSamples(); runtime.Dispose(); desktop.Shutdown(exit); }
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Mark(AppRuntime runtime, SettingsWindow settings, string phase, object? details = null)
    {
        Benchmark.Mark(phase, await Benchmark.VerifyDisplay(runtime, settings, details));
    }
    private static async Task RunMedia(AppRuntime runtime, SettingsWindow settings)
    {
        await Mark(runtime, settings, "media-default-home"); await Benchmark.Wait(10);
        await Mark(runtime, settings, "media-512x512-120frames-select");
        await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = "media-stress" });
        await Mark(runtime, settings, "media-home-pet"); await Benchmark.Wait(30);
        settings.HideToTray(); await Mark(runtime, settings, "media-pet-only"); await Benchmark.Wait(30);
        runtime.ShowSettings(); Benchmark.Click(settings, "SettingsNavPacks"); await Task.Delay(150);
        await Mark(runtime, settings, "media-draft-import"); var builder = Benchmark.Find<PetBuilderView>(settings);
        await builder.OpenPath(Path.Combine(Benchmark.MediaFixture, "idle.gif"));
        Benchmark.Find<TextBox>(settings, "CustomPetName").Text = "Memory fixture";
        await Benchmark.Until(() => Benchmark.Find<Button>(settings, "CreateCustomPetPack").IsEnabled, 5);
        await Mark(runtime, settings, "media-draft-preview-and-pet"); await Benchmark.Wait(30);
        settings.HideToTray(); await runtime.UpdateSettings(runtime.Settings with { ShowPet = false });
        await Mark(runtime, settings, "media-all-hidden-retained-draft"); await Benchmark.Wait(20);
        await Mark(runtime, settings, "media-return-2d"); await runtime.UpdateSettings(runtime.Settings with { ShowPet = true, SelectedCharacterId = "default-cat" }); await Benchmark.Wait(20);
    }
    private static async Task RunSoak(AppRuntime runtime, SettingsWindow settings)
    {
        foreach (var (name, file) in Benchmark.GlbInputs)
        {
            runtime.ShowSettings(); Benchmark.Click(settings, "SettingsNavPacks"); await Task.Delay(150);
            var builder = Benchmark.Find<PetBuilderView>(settings);
            await Mark(runtime, settings, "soak-install-" + name); await builder.OpenPath(file);
            var editor = Benchmark.Find<GlbPetView>(settings); await Benchmark.Until(() => !editor.IsBusy && !editor.IsPreviewLoading);
            Benchmark.Click(settings, "SaveGlbPet"); await Benchmark.Until(() => !builder.IsBusy && runtime.Selected?.Manifest.Name == Path.GetFileNameWithoutExtension(file));
            Benchmark.Click(settings, "SettingsNavTimer"); settings.HideToTray();
        }
        var characters = runtime.Characters.ToArray();
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 60; i++)
        {
            var selected = characters[i % characters.Length];
            await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = selected.Manifest.Id, PetScalePercent = i % 2 == 0 ? 100 : 150 });
            await Mark(runtime, settings, "soak-cycle-" + i, new { character = selected.Manifest.Name, isGlb = selected.IsGlb });
            runtime.ShowSettings(); Benchmark.Click(settings, "SettingsNavTimer"); await Benchmark.Wait(5);
            settings.HideToTray();
            if (i % 10 == 0)
            {
                await runtime.ShowReminder();
                await Mark(runtime, settings, "soak-cycle-" + i + "-invitation");
                runtime.StartBreak(); await Mark(runtime, settings, "soak-cycle-" + i + "-active");
                await Benchmark.Wait(1); runtime.CompleteBreak();
                await Mark(runtime, settings, "soak-cycle-" + i + "-completed");
                await Benchmark.Wait(24);
            }
            else await Benchmark.Wait(25);
        }
        await Mark(runtime, settings, "soak-completed", new { minutes = watch.Elapsed.TotalMinutes, cycles = 60 });
    }
}

internal sealed class MockAccountService : IAccountScreenService
{
    private static AccountSession Session => new(Guid.Parse("22222222-2222-4222-8222-222222222222"), "mock-access", "mock-refresh", DateTimeOffset.UtcNow.AddHours(1), "memory@example.test");
    public bool CanSignIn => true;
    public bool CanCheckout(string market) => true;
    public Task<AccountSession?> RestoreSessionAsync(CancellationToken token) => Task.FromResult<AccountSession?>(Session);
    public Task<PurchaseAccess> CheckPurchaseAsync(AccountSession session, CancellationToken token) => Task.FromResult(PurchaseAccess.Active);
    public Task<AccountSession> RefreshSessionAsync(AccountSession session, CancellationToken token) => Task.FromResult(Session);
    public Task<AccountSession> SignInAsync(CancellationToken token) => throw new InvalidOperationException("Benchmark must not start live sign-in.");
    public Task SignOutAsync(AccountSession session, CancellationToken token) => Task.CompletedTask;
    public Task<AccessCodeResult> RedeemCodeAsync(AccountSession session, string code, CancellationToken token) => throw new InvalidOperationException("Unexpected redemption.");
    public Task StartCheckoutAsync(AccountSession session, string market, Guid requestId, CancellationToken token) => throw new InvalidOperationException("Unexpected checkout.");
    public void Dispose() { }
}
