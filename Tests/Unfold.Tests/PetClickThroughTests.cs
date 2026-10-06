using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetClickThroughTests
{
    [Theory]
    [InlineData("NSWindow", 1, true)]
    [InlineData("NSWindow", 0, false)]
    [InlineData("Headless", 1, false)]
    [InlineData("HWND", 1, false)]
    public void MacInputRequiresARealNativeWindow(string descriptor, int value, bool expected)
    {
        Assert.Equal(expected, MacPetWindow.HasNativeHandle(new PlatformHandle((nint)value, descriptor)));
        Assert.False(MacPetWindow.HasNativeHandle(null));
    }

    [AvaloniaFact]
    public async Task TransparentMarginsHolesAndEdgePixelsPassThrough()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        foreach (var point in new Point[] { new(1, 1), new(47, 96), new(97, 97), new(191, 191), new(-1, 96) })
            Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(point)));
        Assert.True(pet.PetView.OpaqueAt(new(47, 96))); // Hover can retain its edge tolerance.
        Assert.True(pet.AcceptsPointerAt(scope.WindowPoint(new(60, 96))));
        // An animation can expose or paint a pixel without moving the pointer.
        var pixels = new uint[32 * 32]; pixels[16 * 32 + 16] = 0xFFFFFFFF;
        pet.PetView.SetFrames([new(new(32, 32, pixels), TimeSpan.FromSeconds(1))], true);
        Assert.True(pet.AcceptsPointerAt(scope.WindowPoint(new(97, 97))));
        Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(60, 96))));
    }

    [AvaloniaTheory]
    [InlineData(50, false)] [InlineData(150, false)] [InlineData(100, true)]
    public async Task PixelPickingFollowsSizeAndFacingDirection(int scale, bool mirrored)
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { PetScalePercent = scale }); Layout(pet);
        var pixels = new uint[32 * 32]; pixels[16 * 32 + 8] = 0xFFFFFFFF;
        pet.PetView.SetFrames([new(new(32, 32, pixels), TimeSpan.FromSeconds(1))], true);
        pet.PetView.SetPose(PetPose.Neutral, mirrored);
        var factor = pet.PetView.Bounds.Width / 32;
        Assert.True(pet.AcceptsPointerAt(scope.WindowPoint(new(8.5 * factor, 16.5 * factor))));
        Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(20.5 * factor, 16.5 * factor))));
    }

    [AvaloniaFact]
    public async Task HoverBubblePassesThroughAndReminderRemainsInteractive()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        pet.MouseMove(scope.WindowPoint(new(60, 96))); Layout(pet);
        var bubble = pet.GetVisualDescendants().OfType<PetSpeechBubble>().Single();
        Assert.True(bubble.IsVisible);
        Assert.False(pet.AcceptsPointerAt(bubble.TranslatePoint(new(30, 30), pet)!.Value));
        Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(1, 1))));
        await scope.Runtime.ShowReminder(); Layout(pet);
        var start = pet.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PetBreakStart");
        Assert.True(pet.AcceptsPointerAt(start.TranslatePoint(new(10, 10), pet)!.Value));
    }

    [AvaloniaFact]
    public async Task CapturedDragRetainsInputUntilReleaseAndHiddenPetCannotCapture()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        pet.MouseDown(scope.WindowPoint(new(60, 96)), MouseButton.Left); Layout(pet);
        Assert.True(pet.AcceptsPointerAt(new(-20, -20)));
        pet.MouseMove(scope.WindowPoint(new(70, 96))); Layout(pet);
        Assert.True(pet.AcceptsPointerAt(scope.WindowPoint(new(1, 1))));
        pet.MouseUp(scope.WindowPoint(new(1, 1)), MouseButton.Left); Layout(pet);
        Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(1, 1))));
        await scope.Runtime.HidePet();
        Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(60, 96))));
    }

    [AvaloniaFact]
    public async Task ContextMenuRetainsInputUntilClosed()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var transparent = scope.WindowPoint(new(1, 1));
        Assert.False(pet.AcceptsPointerAt(transparent));
        pet.ContextMenu!.Open(pet); Layout(pet);
        Assert.True(pet.ContextMenu.IsOpen);
        Assert.True(pet.AcceptsPointerAt(transparent));
        pet.ContextMenu.Close(); Layout(pet);
        Assert.False(pet.AcceptsPointerAt(transparent));
    }

    [AvaloniaFact]
    public async Task CaptureMovingToAnotherWindowDoesNotBlockTransparentPixels()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var other = new Window { Content = new Button { Content = "Other window" } };
        try
        {
            other.Show();
            IPointer? pointer = null;
            pet.PetView.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer,
                Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            pet.MouseDown(scope.WindowPoint(new(60, 96)), MouseButton.Left); Layout(pet);
            Assert.NotNull(pointer);
            pointer.Capture((Button)other.Content!); Layout(pet);
            Assert.False(pet.AcceptsPointerAt(scope.WindowPoint(new(1, 1))));
            pointer.Capture(null);
            pet.MouseUp(scope.WindowPoint(new(1, 1)), MouseButton.Left);
        }
        finally { other.Close(); }
    }

    [AvaloniaFact]
    public async Task NativePollingNeverUsesHeadlessHandles()
    {
        using var scope = new Scope(); await scope.Load();
        Assert.False(MacPetWindow.TryGetPointer(scope.Pet, out _));
        Assert.False(MacPetWindow.SetClickThrough(scope.Pet, true));
        scope.Pet.UpdateClickThrough(); Assert.False(scope.Pet.IsClickThrough);
    }

    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Load()
        {
            var document = new PixelDocument(32, 32) { Name = "Input fixture" };
            for (var y = 8; y < 24; y++) for (var x = 8; x < 24; x++)
                document.Layers[0].Frames[0][y * 32 + x] = x == 16 && y == 16 ? 0 : 0xFFFFFFFFu;
            var package = Runtime.Library.Save(document); await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id,
                ShowPet = true, ReminderSoundsEnabled = false, BubbleDirection = BubbleDirection.Bottom });
            Pet.Position = new(500, 400); Layout(Pet);
        }
        public Point WindowPoint(Point local) => Pet.PetView.TranslatePoint(local, Pet)!.Value;
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
