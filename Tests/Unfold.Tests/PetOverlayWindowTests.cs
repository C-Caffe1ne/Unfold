using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetOverlayWindowTests
{
    [AvaloniaFact]
    public void ReminderSurfaceNeverSharesThePetRenderWindow()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        try
        {
            pet.Show(); pet.Position = new(500, 400); pet.RefreshSpeech();
            Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
            Assert.Empty(pet.GetVisualDescendants().OfType<PetSpeechBubble>());
            Assert.Equal(new Size(192, 192), pet.ClientSize);
            Assert.Equal(new Point(0, 0), pet.PetView.TranslatePoint(default, pet));
            Assert.Equal(pet.Position, pet.PetAnchor);
        }
        finally { pet.ClosePet(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

    [Theory]
    [InlineData(0, 1920, 1, 96)]
    [InlineData(-1920, 1920, 1, 288)]
    [InlineData(0, 3840, 2, 288)]
    public void OverlayFitsScreenEdgesWithoutIntersectingThePet(int origin, int width, double scale, double petSize)
    {
        var area = new PixelRect(origin, 25, width, 1415);
        foreach (var x in new[] { area.X, area.X + width / 2 - (int)(petSize * scale / 2), area.Right - (int)(petSize * scale) })
        foreach (var y in new[] { area.Y, area.Bottom - (int)(petSize * scale) })
        {
            var anchor = new PixelPoint(x, y);
            var layout = PetOverlayPlacement.Calculate(anchor, petSize, scale, area);
            var overlay = new PixelRect(layout.Position, new PixelSize((int)Math.Ceiling(layout.Width * scale),
                (int)Math.Ceiling(PetOverlayWindow.SurfaceHeight * scale)));
            var pet = new PixelRect(anchor, new PixelSize((int)(petSize * scale), (int)(petSize * scale)));
            Assert.False(overlay.Intersects(pet));
            Assert.True(area.Contains(overlay));
        }
    }

    [Fact]
    public void AutomaticSideHasAStableTransitionBand()
    {
        var area = new PixelRect(0, 0, 1920, 1080);
        Assert.Equal(BubbleDirection.Right, PetOverlayPlacement.Calculate(new(870, 400), 192, 1, area, BubbleDirection.Right).Direction);
        Assert.Equal(BubbleDirection.Left, PetOverlayPlacement.Calculate(new(900, 400), 192, 1, area, BubbleDirection.Right).Direction);
        Assert.Equal(BubbleDirection.Left, PetOverlayPlacement.Calculate(new(860, 400), 192, 1, area, BubbleDirection.Left).Direction);
        Assert.Equal(BubbleDirection.Right, PetOverlayPlacement.Calculate(new(800, 400), 192, 1, area, BubbleDirection.Left).Direction);
    }

    [AvaloniaFact]
    public async Task TimerCommandsUseTheSharedClockAndStopImmediatelyWithoutConfirmation()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var remaining = scope.Runtime.Clock.Remaining;
        scope.Runtime.Clock.Start(TimeSpan.Zero); pet.RefreshSpeech();
        Press(pet.Overlay.Timer, "PetTimerToggle");
        Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
        Assert.Equal("타이머 계속", AutomationProperties.GetName(Find<Button>(pet.Overlay.Timer, "PetTimerToggle")));
        Assert.Contains("일시정지", AutomationProperties.GetName(Find<Border>(pet.Overlay.Timer, "PetTimerBar")));
        Press(pet.Overlay.Timer, "PetTimerToggle"); Assert.False(scope.Runtime.Clock.Paused);
        Assert.Contains("진행 중", AutomationProperties.GetName(Find<Border>(pet.Overlay.Timer, "PetTimerBar")));
        var confirmations = 0;
        scope.Runtime.ConfirmActionOverride = (_, _, _) => { confirmations++; return Task.FromResult(1); };
        Press(pet.Overlay.Timer, "PetTimerStop");
        Assert.True(scope.Runtime.Clock.Stopped); Assert.Equal(0, confirmations);
        Assert.Equal(scope.Runtime.Clock.Interval, scope.Runtime.Clock.Remaining);
        Assert.Contains("중지", AutomationProperties.GetName(Find<Border>(pet.Overlay.Timer, "PetTimerBar")));
        Assert.False(Find<Button>(pet.Overlay.Timer, "PetTimerStop").IsEnabled);
        Assert.Equal("타이머 시작", AutomationProperties.GetName(Find<Button>(pet.Overlay.Timer, "PetTimerToggle")));
        Press(pet.Overlay.Timer, "PetTimerToggle"); Assert.False(scope.Runtime.Clock.Stopped);
        Assert.Equal("60:00", Find<AnimatedCountdown>(pet.Overlay.Timer, "PetTimerCountdown").Text);
    }

    [AvaloniaFact]
    public async Task ReminderTransitionsAndSideUpdatesCannotArrangeThePet()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var size = pet.ClientSize; var bounds = pet.PetView.Bounds;
        var positions = 0; var sizes = 0; var arrangements = 0;
        pet.PositionChanged += (_, _) => positions++;
        pet.SizeChanged += (_, _) => sizes++;
        pet.PetView.PropertyChanged += (_, e) => { if (e.Property == Visual.BoundsProperty) arrangements++; };
        foreach (var x in new[] { 200, 1400, 800, 1100 })
        {
            pet.Position = new(x, 400); positions = 0;
            scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat")); pet.RefreshSpeech(); Layout(pet.Overlay);
            Press(pet.Overlay, "PetBreakSnooze");
            scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat")); pet.RefreshSpeech(); Layout(pet.Overlay);
            Press(pet.Overlay, "PetBreakStart"); Press(pet.Overlay, "PetBreakComplete");
            scope.Runtime.Stop(); pet.RefreshSpeech(); Layout(pet);
            Assert.Equal(new PixelPoint(x, 400), pet.Position); Assert.Equal(pet.Position, pet.PetAnchor);
            Assert.Equal(size, pet.ClientSize); Assert.Equal(bounds, pet.PetView.Bounds);
            Assert.Equal(0, positions); Assert.Equal(0, sizes); Assert.Equal(0, arrangements);
        }
    }

    [AvaloniaFact]
    public async Task OnlyVisibleButtonsCaptureInputAndCapturedPressSurvivesPointerExit()
    {
        using var scope = new Scope(); await scope.Load(); var overlay = scope.Pet.Overlay;
        Assert.False(overlay.AcceptsPointerAt(new(1, 1)));
        var timer = scope.Pet.Overlay.Timer;
        timer.UpdateHover(true, false, TimeSpan.Zero); timer.UpdateHover(true, false, TimeSpan.FromMilliseconds(180));
        Layout(timer);
        var clock = Find<AnimatedCountdown>(timer, "PetTimerCountdown");
        Assert.False(timer.AcceptsPointerAt(clock.TranslatePoint(new(10, 10), timer)!.Value));
        var toggle = Find<Button>(timer, "PetTimerToggle");
        var point = toggle.TranslatePoint(new(10, 10), timer)!.Value;
        Assert.True(timer.AcceptsPointerAt(point));
        timer.MouseDown(point, Avalonia.Input.MouseButton.Left); Layout(timer);
        Assert.True(timer.AcceptsPointerAt(new(-100, -100)));
        timer.MouseUp(new(-100, -100), Avalonia.Input.MouseButton.Left); Layout(timer);
        Assert.False(timer.AcceptsPointerAt(new(-100, -100)));
        scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat")); scope.Pet.RefreshSpeech(); Layout(overlay);
        var title = Find<TextBlock>(overlay, "PetBreakTitle");
        Assert.False(overlay.AcceptsPointerAt(title.TranslatePoint(new(20, 10), overlay)!.Value));
        Assert.True(overlay.AcceptsPointerAt(Find<Button>(overlay, "PetBreakStart").TranslatePoint(new(10, 10), overlay)!.Value));
    }

    [AvaloniaFact]
    public async Task TurningOffTimerBarKeepsReminderControlsAndPetVisibilitySeparate()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        Assert.True(pet.Overlay.Timer.IsVisible); Assert.False(pet.Overlay.IsVisible);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPetTimer = false });
        Assert.True(pet.IsVisible); Assert.False(pet.Overlay.IsVisible); Assert.False(pet.Overlay.Timer.IsVisible);
        Assert.False(AppSettings.Load(Path.Combine(scope.Root, "settings.json")).ShowPetTimer);
        await scope.Runtime.ShowReminder(); Layout(pet.Overlay);
        Assert.True(pet.Overlay.IsVisible); Assert.True(pet.Overlay.Bubble.IsVisible);
        Assert.False(pet.Overlay.Timer.IsVisible);
        await scope.Runtime.HidePet(); Assert.False(pet.IsVisible); Assert.False(pet.Overlay.IsVisible);
        pet.ClosePet(); Assert.False(pet.Overlay.IsVisible); Assert.False(pet.Overlay.Timer.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(260)] [InlineData(1180)]
    public async Task StopFromThePetDoesNotOpenADialogOrHoldItsPlacement(int x)
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        pet.Position = new(x, 400); pet.RefreshSpeech(); Layout(pet.Overlay);
        scope.Runtime.Clock.Start(TimeSpan.Zero); pet.RefreshSpeech();
        var position = pet.Position;
        Press(pet.Overlay.Timer, "PetTimerStop");
        Assert.Empty(pet.Overlay.OwnedWindows);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(pet.Overlay.HoldsPlacement);
        Assert.Equal(position, pet.Position);
    }

    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Press(Window window, string name) { Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window); }
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root); Runtime = new(lifetime); }
        public async Task Load()
        {
            var fixture = new SpriteFixture(16, 16) { Name = "Overlay fixture" };
            for (var y = 5; y < 11; y++) for (var x = 5; x < 11; x++) fixture.Pixels[y * 16 + x] = 0xffffffff;
            var package = Runtime.Library.InstallFixture(fixture); await Runtime.Reload();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ReminderSoundsEnabled = false });
            Pet.Position = new(500, 400); Pet.RefreshSpeech(); Layout(Pet); Layout(Pet.Overlay);
        }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
