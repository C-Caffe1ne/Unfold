using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Text.Json;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetHoverTests
{
    [Theory]
    [InlineData("HWND", 1, true)] [InlineData("HWND", 0, false)]
    [InlineData("Headless", 1, false)] [InlineData("NSWindow", 1, false)]
    public void CursorPollingRequiresANonzeroWindowsNativeHandle(string descriptor, int value, bool expected)
    {
        Assert.Equal(expected, WindowsPetWindow.HasNativeHandle(new Avalonia.Platform.PlatformHandle((nint)value, descriptor)));
        Assert.False(WindowsPetWindow.HasNativeHandle(null));
    }

    [AvaloniaFact]
    public async Task HeadlessPetNeverQualifiesForNativeCursorPollingOrShowsAHoverBubble()
    {
        using var scope = new Scope(); await scope.Load();
        Assert.False(WindowsPetWindow.HasNativeHandle(scope.Pet.TryGetPlatformHandle()));
        scope.Hover(); Assert.False(scope.Bubble.IsVisible);
        Assert.DoesNotContain(scope.Pet.GetVisualDescendants().OfType<Control>(), c => c.Name is "PetHoverTime" or "PetHoverRemaining");
    }

    [AvaloniaFact]
    public async Task ReminderActionsRemainAvailableAndPointerEntryCannotReopenADismissedBubble()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        scope.Hover(); Assert.False(scope.Bubble.IsVisible);
        await scope.Runtime.ShowReminder(); Layout(pet);
        var start = pet.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PetBreakStart");
        Assert.True(start.IsEffectivelyVisible);
        Assert.True(pet.AcceptsPointerAt(start.TranslatePoint(new(10, 10), pet)!.Value));
        pet.MouseMove(new(-10, -10)); Layout(pet); Assert.True(scope.Bubble.IsVisible);
        scope.Hover(); scope.Runtime.StartBreak(); Layout(pet);
        Assert.True(Text(pet, "PetBreakTimer").IsEffectivelyVisible);
        scope.Runtime.CompleteBreak(); Layout(pet);
        Assert.Equal("스트레칭을 마쳤어요!", Text(pet, "PetBreakTitle").Text);
        scope.Runtime.Reminder.Cancel(); pet.RefreshSpeech(); Layout(pet);
        scope.Hover(); Assert.False(scope.Bubble.IsVisible);
        Assert.Single(scope.Runtime.BreakHistory.Completions);
    }

    [AvaloniaFact]
    public async Task DragStillPersistsPositionWithoutOpeningAHoverBubble()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        scope.Hover(); Assert.False(scope.Bubble.IsVisible);
        var point = pet.PetView.TranslatePoint(new(96, 96), pet)!.Value;
        var size = pet.ClientSize; var start = pet.PetAnchor;
        pet.MouseDown(point, MouseButton.Left); Layout(pet);
        pet.MouseMove(point + new Vector(25, 0)); Layout(pet);
        Assert.Equal(size, pet.ClientSize); Assert.Equal(start.X + 25, pet.PetAnchor.X);
        pet.MouseUp(point, MouseButton.Left); Layout(pet);
        Assert.False(scope.Bubble.IsVisible); Assert.Equal(pet.PetAnchor.X, scope.Runtime.Settings.PetX);
    }

    [AvaloniaTheory]
    [InlineData(BubbleDirection.Top)] [InlineData(BubbleDirection.Bottom)]
    [InlineData(BubbleDirection.Left)] [InlineData(BubbleDirection.Right)]
    public async Task CursorEntryAndExitKeepThePetSurfaceStableAndItsBackgroundTransparent(BubbleDirection direction)
    {
        using var scope = new Scope(); await scope.Load(original: true); var pet = scope.Pet;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { BubbleDirection = direction }); Layout(pet);
        var position = pet.Position; var size = pet.ClientSize; var origin = pet.PetView.TranslatePoint(default, pet);
        var anchor = pet.PetAnchor; var moves = 0; var sizes = 0;
        pet.PositionChanged += (_, _) => moves++; pet.SizeChanged += (_, _) => sizes++;
        for (var i = 0; i < 5; i++)
        {
            scope.Hover(); Assert.False(scope.Bubble.IsVisible);
            pet.MouseMove(pet.PetView.TranslatePoint(new(1, 1), pet)!.Value); Layout(pet);
            Assert.False(pet.AcceptsPointerAt(pet.PetView.TranslatePoint(new(1, 1), pet)!.Value));
            Assert.False(pet.AcceptsPointerAt(scope.Bubble.TranslatePoint(new(5, 5), pet)!.Value));
            pet.MouseMove(new(-10, -10)); Layout(pet);
            Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
            Assert.Equal(origin, pet.PetView.TranslatePoint(default, pet)); Assert.Equal(anchor, pet.PetAnchor);
        }
        Assert.Equal(0, moves); Assert.Equal(0, sizes);
    }

    private static TextBlock Text(Window window, string name) => window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public PetSpeechBubble Bubble => Pet.GetVisualDescendants().OfType<PetSpeechBubble>().Single();
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Load(bool original = false)
        {
            var document = new PixelDocument(16, 16) { Name = "Hover fixture" };
            // Leave enough transparent pixels around the hit-test's 1px tolerance.
            for (var y = 5; y <= 10; y++)
            for (var x = 5; x <= 10; x++) document.Layers[0].Frames[0][y * 16 + x] = 0xFFFFFFFFu;
            var package = Runtime.Library.Save(document);
            if (original)
            {
                var manifest = package.Manifest with
                {
                    BehaviorProfile = OriginalCompanion.Profile,
                    Animations = OriginalCompanion.RequiredClips.ToDictionary(key => key,
                        key => new AnimationDefinition([0, 0], 100, Loop: key is "idle" or "walk"))
                };
                AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            }
            await Runtime.Reload();
            Runtime.Stop();
            // Keep native-window movement out of headless pointer routing. Top/edge
            // placement is covered independently by the screen-coordinate test.
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false, IntervalMinutes = 15, BubbleDirection = BubbleDirection.Bottom });
            Pet.Position = new(500, 400); Layout(Pet);
        }
        public void Hover() { Pet.MouseMove(Pet.PetView.TranslatePoint(new(96, 96), Pet)!.Value); Layout(Pet); }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
