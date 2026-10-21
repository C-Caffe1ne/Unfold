using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetThrowTests
{
    [AvaloniaTheory]
    [InlineData("original")] [InlineData("media")]
    public async Task AFastReleaseThrowsThePetOntoTheWorkAreaFloorAndSavesWhereItLands(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind); var pet = scope.Pet;
        var saved = (scope.Runtime.Settings.PetX, scope.Runtime.Settings.PetY);
        var last = Drag(pet, scope.PaintedPoint(), new Vector(40, -12), stepMs: 16);
        pet.MouseUp(last, MouseButton.Left); Layout(pet);
        Assert.True(pet.IsThrown);
        Assert.Equal(saved, (scope.Runtime.Settings.PetX, scope.Runtime.Settings.PetY)); // Not saved mid-air.
        if (kind == "original") Assert.Contains(pet.ActiveAnimation, new[] { "pickup", "held" });
        for (var tick = 0; tick < 4000 && pet.IsThrown; tick++) pet.AdvanceThrow(.016);
        Assert.False(pet.IsThrown);
        var bounds = pet.ThrowBounds();
        Assert.Equal(bounds.Bottom, pet.Position.Y); Assert.InRange(pet.Position.X, bounds.X, bounds.Right);
        if (kind == "original") Assert.Equal("land", pet.ActiveAnimation);
        await Until(() => scope.Runtime.Settings.PetX == pet.PetAnchor.X && scope.Runtime.Settings.PetY == pet.PetAnchor.Y);
    }

    [AvaloniaFact]
    public async Task ASlowReleaseStaysUnderThePointerLikeBefore()
    {
        using var scope = new Scope(); await scope.Select("original"); var pet = scope.Pet;
        var start = pet.Position;
        var last = Drag(pet, scope.PaintedPoint(), new Vector(30, 0), stepMs: 120);
        pet.MouseUp(last, MouseButton.Left); Layout(pet);
        Assert.False(pet.IsThrown);
        Assert.Equal(new PixelPoint(start.X + 180, start.Y), pet.Position);
        await Until(() => scope.Runtime.Settings.PetX == pet.PetAnchor.X);
    }

    [AvaloniaFact]
    public async Task ReduceMotionKeepsTheDirectDragAndNeverThrows()
    {
        using var scope = new Scope(); await scope.Select("original"); var pet = scope.Pet;
        pet.ReduceMotion = () => true;
        var start = pet.Position;
        var last = Drag(pet, scope.PaintedPoint(), new Vector(40, 0), stepMs: 16);
        Assert.Equal(new PixelPoint(start.X + 240, start.Y), pet.Position); // No spring lag while dragging.
        pet.MouseUp(last, MouseButton.Left); Layout(pet);
        Assert.False(pet.IsThrown);
        Assert.Equal(new PixelPoint(start.X + 240, start.Y), pet.Position);
    }

    [AvaloniaFact]
    public async Task TheSpringTrailsAFastPointerWhileDragging()
    {
        using var scope = new Scope(); await scope.Select("original"); var pet = scope.Pet;
        var start = pet.Position;
        var last = Drag(pet, scope.PaintedPoint(), new Vector(40, 0), stepMs: 16);
        Assert.InRange(pet.Position.X, start.X + 1, start.X + 239);
        pet.MouseUp(last, MouseButton.Left); Layout(pet);
        for (var tick = 0; tick < 4000 && pet.IsThrown; tick++) pet.AdvanceThrow(.016);
    }

    [AvaloniaFact]
    public async Task GrabbingAPetMidAirStopsTheThrow()
    {
        using var scope = new Scope(); await scope.Select("original"); var pet = scope.Pet;
        var last = Drag(pet, scope.PaintedPoint(), new Vector(0, -40), stepMs: 16);
        pet.MouseUp(last, MouseButton.Left); Layout(pet);
        Assert.True(pet.IsThrown);
        pet.AdvanceThrow(.05); Layout(pet);
        pet.MouseDown(scope.PaintedPoint(), MouseButton.Left);
        Assert.False(pet.IsThrown);
        pet.MouseUp(scope.PaintedPoint(), MouseButton.Left);
    }

    // Moves the pointer by `step` in screen space per sample. Headless input is window-local,
    // so each sample subtracts how far the window itself has moved since the press.
    private static Point Drag(PetWindow pet, Point start, Vector step, int stepMs)
    {
        var now = TimeSpan.Zero; pet.MotionClock = () => now;
        var origin = pet.Position; var scale = pet.DesktopScaling;
        pet.MouseDown(start, MouseButton.Left);
        var point = start;
        for (var i = 1; i <= 6; i++)
        {
            now += TimeSpan.FromMilliseconds(stepMs);
            point = start + step * i - new Vector(pet.Position.X - origin.X, pet.Position.Y - origin.Y) / scale;
            pet.MouseMove(point, RawInputModifiers.LeftMouseButton);
            pet.AdvanceThrow(stepMs / 1000d);
        }
        now += TimeSpan.FromMilliseconds(8);
        return start + step * 6 - new Vector(pet.Position.X - origin.X, pet.Position.Y - origin.Y) / scale;
    }

    private static void Layout(PetWindow pet) { Dispatcher.UIThread.RunJobs(); pet.UpdateLayout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready());
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Select(string kind)
        {
            var document = new SpriteFixture(16, 16) { Name = "Throw" };
            for (var y = 4; y <= 11; y++) for (var x = 4; x <= 11; x++) document.Pixels[y * 16 + x] = 0xFFFFFFFF;
            var package = Runtime.Library.InstallFixture(document);
            var keys = kind == "original" ? OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips) : CustomPetDraft.Actions;
            var mappings = keys.ToDictionary(key => key, key => new AnimationDefinition([0, 0, 0, 0], 20,
                Loop: key is "idle" or "held" or "walk"));
            var manifest = package.Manifest with { BehaviorProfile = kind == "original" ? OriginalCompanion.Profile : null, Animations = mappings };
            AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false });
            Pet.ReduceMotion = () => false;
            Pet.Position = new(500, 300); Layout(Pet);
        }
        public Point PaintedPoint()
        {
            for (var y = 1; y < Pet.PetView.Bounds.Height; y += 3)
            for (var x = 1; x < Pet.PetView.Bounds.Width; x += 3)
                if (Pet.PetView.OpaqueAt(new(x, y), includeEdgeTolerance: false)) return Pet.PetView.TranslatePoint(new(x, y), Pet)!.Value;
            throw new InvalidOperationException("Fixture has no painted pet pixels.");
        }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
