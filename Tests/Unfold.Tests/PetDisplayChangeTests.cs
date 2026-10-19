using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetDisplayChangeTests
{
    [AvaloniaFact]
    public async Task DisplayChangeReturnsAnOffscreenPetWithoutRewritingItsSavedPosition()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var saved = (scope.Runtime.Settings.PetX, scope.Runtime.Settings.PetY);
        var screen = Assert.IsAssignableFrom<Screen>(pet.Screens.ScreenFromWindow(pet) ?? pet.Screens.Primary);
        var area = PetWindow.PlacementArea(screen.Bounds, screen.WorkingArea, OperatingSystem.IsMacOS());
        pet.Position = new(area.Right + 4000, area.Bottom + 3000); // The pet's monitor was disconnected.
        pet.HandleDisplaysChanged();
        var size = (int)Math.Ceiling(pet.PetView.Width * pet.DesktopScaling);
        Assert.InRange(pet.Position.X, area.X, area.Right - size);
        Assert.InRange(pet.Position.Y, area.Y, area.Bottom - size);
        // Automatic placement must not replace the user's manual position.
        Assert.Equal(saved, (scope.Runtime.Settings.PetX, scope.Runtime.Settings.PetY));
    }

    [AvaloniaFact]
    public async Task HiddenPetKeepsItsPositionUntilShownAgain()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var offscreen = new PixelPoint(-40000, -40000);
        pet.Position = offscreen; pet.HidePet();
        pet.HandleDisplaysChanged();
        Assert.Equal(offscreen, pet.Position);
    }

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
            var document = new SpriteFixture(32, 32) { Name = "Display fixture" };
            for (var y = 8; y < 24; y++) for (var x = 8; x < 24; x++) document.Pixels[y * 32 + x] = 0xFFFFFFFFu;
            var package = Runtime.Library.InstallFixture(document); await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id,
                ShowPet = true, ReminderSoundsEnabled = false });
            Pet.Position = new(500, 400); Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
