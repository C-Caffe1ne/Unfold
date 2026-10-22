using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetSystemResourcesUiTests
{
    [Fact]
    public void ResourcesAreOptInAndRecoverIndependentlyFromDamagedPreferences()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        Assert.False(new AppSettings().ShowSystemResources);
        File.WriteAllText(path, """{"intervalMinutes":37}""");
        var legacy = AppSettings.Load(path, out var needsBackup);
        Assert.False(legacy.ShowSystemResources); Assert.False(needsBackup);
        foreach (var enabled in new[] { true, false })
        {
            (legacy with { ShowSystemResources = enabled }).Save(path);
            var saved = AppSettings.Load(path, out needsBackup);
            Assert.Equal(enabled, saved.ShowSystemResources); Assert.False(needsBackup);
            Assert.Equal(37, saved.IntervalMinutes);
        }
        foreach (var invalid in new[] { "\"broken\"", "null", "[]" })
        {
            var original = "{\"showSystemResources\":" + invalid + ",\"intervalMinutes\":37,\"showPetTimer\":false}";
            File.WriteAllText(path, original);
            var recovered = AppSettings.Load(path, out needsBackup);
            Assert.False(recovered.ShowSystemResources); Assert.True(needsBackup);
            Assert.Equal(37, recovered.IntervalMinutes); Assert.False(recovered.ShowPetTimer);
            Assert.Equal(original, File.ReadAllText(path));
        }
    }

    [AvaloniaFact]
    public async Task CheckboxAutoSavesAndDisablingTheTimerRetainsTheResourcePreference()
    {
        using var scope = new Scope();
        using var window = new SettingsWindow(scope.Runtime);
        try
        {
            window.Show(); Layout(window);
            Find<Button>(window, "SettingsNavSettings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(window);
            var resources = Find<CheckBox>(window, "ShowSystemResources");
            var timer = Find<CheckBox>(window, "ShowPetTimer");
            Assert.False(resources.IsChecked); Assert.True(resources.IsEnabled);
            var help = Assert.IsType<string>(ToolTip.GetTip(resources));
            Assert.Contains(L.T("마우스를 올린 동안 시스템 전체 사용량을 5초마다 표시해요."), help);
            Assert.Contains(L.T("물리 메모리 사용량 · 즉시 사용 가능한 메모리 제외"), help);
            resources.IsChecked = true;
            await Until(() => scope.Runtime.Settings.ShowSystemResources);
            Assert.True(AppSettings.Load(scope.SettingsPath).ShowSystemResources);
            timer.IsChecked = false;
            await Until(() => !scope.Runtime.Settings.ShowPetTimer && !resources.IsEnabled);
            Assert.True(resources.IsChecked);
            var saved = AppSettings.Load(scope.SettingsPath);
            Assert.True(saved.ShowSystemResources); Assert.False(saved.ShowPetTimer);
            timer.IsChecked = true;
            await Until(() => resources.IsEnabled && scope.Runtime.Settings.ShowPetTimer);
            Assert.True(resources.IsChecked);
            resources.IsChecked = false;
            await Until(() => !scope.Runtime.Settings.ShowSystemResources);
            Assert.False(AppSettings.Load(scope.SettingsPath).ShowSystemResources);
            Assert.Empty(window.OwnedWindows);
        }
        finally { window.HideToTray(); }
    }

    [AvaloniaFact]
    public async Task CollectionRequiresBothOptInAndHoverAndStopsAfterExitOffOrHide()
    {
        using var scope = new Scope(); await scope.LoadPet();
        var timer = scope.Timer!; var source = scope.Source;
        Reveal(timer, 0);
        await Settle(); Assert.Equal(0, source.Calls);
        Leave(timer, 1000);
        await scope.SetResources(true);
        await Settle(); Assert.Equal(0, source.Calls);
        timer.Hide(); await Settle(); Assert.Equal(0, source.Calls);
        timer.Refresh(); Layout(timer);
        Reveal(timer, 2000);
        await Until(() => Find<TextBlock>(timer, "PetCpuValue").Text == "80%");
        Assert.Equal("78%", Find<TextBlock>(timer, "PetMemoryValue").Text);
        Assert.True(source.Calls >= 2);

        Leave(timer, 3000); await AssertStopped(scope);
        var before = source.Calls;
        Reveal(timer, 4000); await Until(() => source.Calls > before);
        await scope.SetResources(false); await AssertStopped(scope);
        Assert.False(Find<Grid>(timer, "PetSystemResources").IsVisible);
        before = source.Calls;
        await scope.SetResources(true);
        await Until(() => source.Calls > before);
        timer.Hide(); await AssertStopped(scope);
        Assert.False(timer.IsVisible);
    }

    [AvaloniaFact]
    public async Task ResourceRowsGrowOnlyTheTimerAndPassInputWhileItsButtonsRemainInteractive()
    {
        using var scope = new Scope(); await scope.LoadPet();
        var pet = scope.Pet; var timer = scope.Timer!;
        var anchor = pet.Position; var clientSize = pet.ClientSize; var petBounds = pet.PetView.Bounds;
        Assert.Equal(78, timer.Height);
        await scope.SetResources(true); Layout(timer);
        Assert.Equal(122, timer.Height); Assert.Equal(122, timer.MinHeight); Assert.Equal(122, timer.MaxHeight);
        Reveal(timer, 0);
        await Until(() => Find<TextBlock>(timer, "PetCpuValue").Text == "80%");
        Layout(timer);
        var resources = Find<Grid>(timer, "PetSystemResources");
        Assert.True(resources.IsEffectivelyVisible); Assert.True(resources.Bounds.Height > 0);
        foreach (var part in resources.Children)
        {
            Assert.True(part.Bounds.Width > 0 && part.Bounds.Height > 0);
            var center = part.TranslatePoint(new Point(part.Bounds.Width / 2, part.Bounds.Height / 2), timer)!.Value;
            Assert.False(timer.AcceptsPointerAt(center));
            Assert.InRange(center.Y, 78, 122);
        }
        var cpu = Find<ProgressBar>(timer, "PetCpuGauge"); var memory = Find<ProgressBar>(timer, "PetMemoryGauge");
        Assert.Equal((80d, 78.125), (cpu.Value, memory.Value));
        Assert.Equal(cpu.Bounds.Width, memory.Bounds.Width); Assert.Equal(cpu.Bounds.X, memory.Bounds.X);
        var dark = DesignSystem.Themes.Single(theme => theme.Id == DesignSystem.CurrentTheme).IsDark;
        Assert.Equal(PetTimerWindow.GaugeColor(80, dark), Assert.IsType<SolidColorBrush>(cpu.Foreground).Color);
        // The bar keeps only the pause/resume and stop controls.
        Assert.All(timer.GetVisualDescendants().OfType<Button>(), button => Assert.NotNull(button.FindAncestorOfType<TimerControls>()));
        var toggle = Find<Button>(timer, "PetTimerToggle");
        Assert.True(timer.AcceptsPointerAt(toggle.TranslatePoint(new Point(5, 5), timer)!.Value));
        Assert.Equal(anchor, pet.Position); Assert.Equal(clientSize, pet.ClientSize); Assert.Equal(petBounds, pet.PetView.Bounds);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/verification/pet-resources"));
        Directory.CreateDirectory(directory);
        using var frame = timer.CaptureRenderedFrame(); Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, "hover-resources.png"), PngBitmapEncoderOptions.Default);

        await scope.SetResources(false); Layout(timer);
        Assert.Equal(78, timer.Height); Assert.Equal(78, timer.MinHeight); Assert.Equal(78, timer.MaxHeight);
        Assert.Equal(anchor, pet.Position); Assert.Equal(clientSize, pet.ClientSize); Assert.Equal(petBounds, pet.PetView.Bounds);
    }

    [Fact]
    public void MissingMeasurementsStayUnknownAndMemoryShowsItsShare()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("—", PetTimerWindow.FormatPercent(null));
            Assert.Null(PetTimerWindow.MemoryPercent(null));
            Assert.Equal("13%", PetTimerWindow.FormatPercent(12.6));
            Assert.Equal("0%", PetTimerWindow.FormatPercent(0));
            Assert.Equal("78%", PetTimerWindow.FormatPercent(PetTimerWindow.MemoryPercent(Source.Memory)));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(0, "#2563D9", "#6EA8FE")] [InlineData(29.4, "#2563D9", "#6EA8FE")]
    [InlineData(29.5, "#1F8A4C", "#4CC38A")] [InlineData(59, "#1F8A4C", "#4CC38A")]
    [InlineData(60, "#C2610C", "#F5A04A")] [InlineData(84.4, "#C2610C", "#F5A04A")]
    [InlineData(84.5, "#D13438", "#FF7B7B")] [InlineData(100, "#D13438", "#FF7B7B")]
    public void GaugeColorRisesFromBlueThroughGreenAndOrangeToRedWithTheShownPercent(double percent, string light, string dark)
    {
        Assert.Equal(Color.Parse(light), PetTimerWindow.GaugeColor(percent, dark: false));
        Assert.Equal(Color.Parse(dark), PetTimerWindow.GaugeColor(percent, dark: true));
    }

    [AvaloniaFact]
    public async Task LanguageChangesRefreshTheExistingMemoryLabelAndItsAccessibleMeaning()
    {
        using var scope = new Scope(); await scope.LoadPet(); await scope.SetResources(true);
        var timer = scope.Timer!;
        Reveal(timer, 0);
        await Until(() => Find<TextBlock>(timer, "PetCpuValue").Text == "80%");
        var label = Find<TextBlock>(timer, "PetMemoryLabel");
        Assert.Equal("메모리", label.Text);
        scope.Runtime.SetLanguage(AppLanguage.English);
        timer.Refresh(); Layout(timer);
        Assert.Same(label, Find<TextBlock>(timer, "PetMemoryLabel"));
        Assert.Equal("Memory", label.Text);
        var value = Find<TextBlock>(timer, "PetMemoryValue");
        Assert.Contains(L.T("물리 메모리 사용량 · 즉시 사용 가능한 메모리 제외"), AutomationProperties.GetName(value));
        Assert.Contains(value.Text!, AutomationProperties.GetName(value));
        Assert.False(timer.AcceptsPointerAt(label.TranslatePoint(new Point(5, 5), timer)!.Value));
    }

    [Theory]
    [InlineData(0, 1920, 1)] [InlineData(-1920, 1920, 1)] [InlineData(-3840, 3840, 2)]
    public void ExpandedPanelFitsScreenEdgesWithoutCoveringThePet(int origin, int width, double scale)
    {
        var area = new PixelRect(origin, 25, width, (int)(1055 * scale));
        var petWidth = (int)(120 * scale); var petHeight = (int)(160 * scale);
        foreach (var x in new[] { area.X, area.X + area.Width / 2, area.Right - petWidth })
        foreach (var y in new[] { area.Y, area.Bottom - petHeight })
        foreach (var side in new[] { BubbleDirection.Left, BubbleDirection.Right })
        {
            var body = new PixelRect(x, y, petWidth, petHeight);
            var position = PetTimerPlacement.Calculate(body, scale, area, side, surfaceHeight: 122);
            var panel = new PixelRect(position, new PixelSize((int)Math.Ceiling(208 * scale), (int)Math.Ceiling(122 * scale)));
            Assert.True(area.Contains(panel), $"Resource panel {panel} must fit {area}.");
            Assert.False(body.Intersects(panel), $"Resource panel {panel} must not cover {body}.");
        }
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Reveal(PetTimerWindow timer, int milliseconds)
    {
        timer.UpdateHover(true, false, TimeSpan.FromMilliseconds(milliseconds));
        timer.UpdateHover(true, false, TimeSpan.FromMilliseconds(milliseconds + 180));
    }
    private static void Leave(PetTimerWindow timer, int milliseconds)
    {
        timer.UpdateHover(false, false, TimeSpan.FromMilliseconds(milliseconds));
        timer.UpdateHover(false, false, TimeSpan.FromMilliseconds(milliseconds + 180));
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Dispatcher.UIThread.RunJobs(); Assert.True(ready());
    }
    private static async Task Settle()
    {
        await Task.Delay(80, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
    }
    private static async Task AssertStopped(Scope scope)
    {
        await Settle(); var calls = scope.Source.Calls;
        await Settle(); Assert.Equal(calls, scope.Source.Calls);
        Assert.Same(SystemResourceSnapshot.Empty, scope.Monitor!.Current);
    }

    private sealed class Source : ISystemResourceSource
    {
        internal static readonly SystemMemoryUsage Memory = new(12_500_000_000, 16_000_000_000);
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public SystemResourceReading Read(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = (ulong)Interlocked.Increment(ref calls);
            return new(new(call * 20, call * 100), Memory);
        }
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly AppLanguage previousLanguage = L.Current;
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public Source Source { get; } = new();
        public SystemResourceMonitor? Monitor { get; private set; }
        public PetTimerWindow? Timer { get; private set; }
        public PetWindow Pet => Runtime.ActivePet!;
        public string SettingsPath => Path.Combine(temp.Path, "settings.json");
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime);
        }
        public async Task LoadPet()
        {
            var fixture = new SpriteFixture(16, 16) { Name = "Resource hover fixture" };
            for (var y = 5; y < 11; y++) for (var x = 5; x < 11; x++) fixture.Pixels[y * 16 + x] = 0xffffffff;
            var id = Runtime.Library.InstallFixture(fixture).Manifest.Id;
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = id, ShowPet = true,
                ShowPetTimer = true, ShowSystemResources = false, ReminderSoundsEnabled = false });
            Pet.Position = new(500, 300); Pet.RefreshSpeech(); Layout(Pet); Layout(Pet.Overlay);
            // Only the injected collector is used when this fixture enables resources.
            Pet.Overlay.Timer.Close();
            Monitor = new(Source, TimeSpan.FromMilliseconds(20),
                utcNow: () => new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero).AddSeconds(Source.Calls * 5));
            Timer = new(Pet, Runtime, Monitor); Timer.Refresh(); Layout(Timer);
        }
        public async Task SetResources(bool enabled)
        {
            await Runtime.UpdateSettings(Runtime.Settings with { ShowSystemResources = enabled });
            Timer!.Refresh(); Layout(Timer);
        }
        public void Dispose()
        {
            Timer?.Close(); Monitor?.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
            L.Use(previousLanguage); DesignSystem.ApplyLanguage(previousLanguage);
        }
    }
}
