using Avalonia;
using PixelPoint = Avalonia.PixelPoint;
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
    [InlineData("HWND", 1, true)]
    [InlineData("HWND", 0, false)]
    [InlineData("Headless", 1, false)]
    [InlineData("NSWindow", 1, false)]
    public void CursorPollingRequiresANonzeroWindowsNativeHandle(string descriptor, int value, bool expected)
    {
        Assert.Equal(expected, WindowsPetWindow.HasNativeHandle(new Avalonia.Platform.PlatformHandle((nint)value, descriptor)));
        Assert.False(WindowsPetWindow.HasNativeHandle(null));
    }

    [AvaloniaFact]
    public async Task HeadlessPetNeverQualifiesForNativeCursorPolling()
    {
        using var scope = new Scope(); await scope.Load();
        Assert.False(WindowsPetWindow.HasNativeHandle(scope.Pet.TryGetPlatformHandle()));
        scope.Hover(); Assert.True(scope.Bubble.IsVisible);
    }

    [AvaloniaFact]
    public void TimeAndCountdownRefreshAndShowPausedStoppedAndIdleStates()
    {
        var clock = new StretchClock(TimeSpan.FromMinutes(240)); clock.Start(TimeSpan.Zero);
        var bubble = new PetSpeechBubble(() => { }, () => { }, () => { });
        var window = new Window { Content = bubble, SizeToContent = SizeToContent.WidthAndHeight };
        try
        {
            window.Show();
            var now = new DateTime(2026, 9, 23, 23, 59, 59);
            bubble.RefreshHover(now, clock); Layout(window);
            Assert.Equal("오후 11:59", Text(window, "PetHoverTime").Text);
            Assert.Equal("스트레칭 240:00", Text(window, "PetHoverRemaining").Text);
            clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.FromMinutes(5));
            bubble.RefreshHover(now.AddSeconds(1), clock);
            Assert.Equal("오전 12:00", Text(window, "PetHoverTime").Text);
            Assert.Equal("스트레칭 239:59", Text(window, "PetHoverRemaining").Text);
            clock.TogglePause(TimeSpan.FromSeconds(1));
            clock.Tick(TimeSpan.FromSeconds(5), TimeSpan.Zero, TimeSpan.FromMinutes(5));
            bubble.RefreshHover(now.AddSeconds(5), clock); Layout(window);
            Assert.Equal("스트레칭 239:59 · 일시정지", Text(window, "PetHoverRemaining").Text);
            var line = Text(window, "PetHoverRemaining");
            Assert.True(line.DesiredSize.Width <= bubble.Width - bubble.Padding.Left - bubble.Padding.Right);
            clock.Stop(TimeSpan.FromSeconds(5)); clock.SetInterval(TimeSpan.FromMinutes(15));
            bubble.RefreshHover(now, clock);
            Assert.Equal("스트레칭 15:00 · 중지", line.Text);
            clock.Start(TimeSpan.FromSeconds(5));
            clock.Tick(TimeSpan.FromSeconds(6), TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(5));
            bubble.RefreshHover(now, clock);
            Assert.Equal("스트레칭 15:00 · 자리 비움", line.Text);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.IsEffectivelyVisible);
            Assert.False(bubble.IsHitTestVisible);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData(1, 96)] [InlineData(1.5, 192)] [InlineData(2, 288)]
    public void HoverFitsAtEveryMonitorCornerWithoutMovingThePet(double scale, double size)
    {
        var work = new PixelRect(-2560, -100, 2560, 1440);
        foreach (var direction in Enum.GetValues<BubbleDirection>())
        foreach (var x in new[] { work.X, work.X + 900, work.Right - (int)(size * scale) })
        foreach (var y in new[] { work.Y, work.Y + 650, work.Bottom - (int)(size * scale) })
        {
            var anchor = new PixelPoint(x, y);
            var layout = PetBubbleLayout.CreateHover(direction, anchor, scale, work, size);
            var position = layout.Position(anchor, scale, work);
            Assert.Equal(anchor, new PixelPoint(position.X + (int)Math.Round(layout.Pet.X * scale), position.Y + (int)Math.Round(layout.Pet.Y * scale)));
            Assert.InRange(position.X, work.X, work.Right - (int)Math.Ceiling(layout.Size.Width * scale));
            Assert.InRange(position.Y, work.Y, work.Bottom - (int)Math.Ceiling(layout.Size.Height * scale));
            var bubble = new Rect(layout.Bubble, new Size(DesignSystem.SpeechHoverWidth, DesignSystem.SpeechHoverHeight));
            Assert.False(bubble.Intersects(new Rect(layout.Pet, new Size(size, size))));
        }
    }

    [AvaloniaFact]
    public async Task PointerHoverShowsBothLinesAndLeavingOrHidingClosesIt()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        var anchor = pet.PetAnchor;
        pet.MouseMove(new(1, 1)); Layout(pet); Assert.False(scope.Bubble.IsVisible);
        scope.Hover(); Assert.True(scope.Bubble.IsVisible);
        Assert.True(Text(pet, "PetHoverTime").IsEffectivelyVisible);
        Assert.Contains("스트레칭", Text(pet, "PetHoverRemaining").Text);
        Assert.Equal(anchor, pet.PetAnchor);
        scope.Runtime.Clock.Start(TimeSpan.Zero);
        scope.Runtime.Clock.Tick(TimeSpan.FromSeconds(3), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        pet.RefreshSpeech(); Assert.Contains("14:57", Text(pet, "PetHoverRemaining").Text);
        pet.MouseMove(new(-10, -10)); Layout(pet); Assert.False(scope.Bubble.IsVisible);
        Assert.Equal(anchor, pet.PetAnchor);
        scope.Hover(); await scope.Runtime.HidePet(); Layout(pet);
        Assert.False(pet.IsVisible); Assert.False(scope.Bubble.IsVisible);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = true }); Layout(pet);
        Assert.False(scope.Bubble.IsVisible);
    }

    [AvaloniaFact]
    public async Task ReminderTakesPriorityAndTheHoverCannotReplaceItsActions()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        scope.Hover();
        await scope.Runtime.ShowReminder(); Layout(pet);
        Assert.False(Text(pet, "PetHoverTime").IsEffectivelyVisible);
        Assert.Equal(DesignSystem.SpeechBubbleWidth, scope.Bubble.Width);
        Assert.True(scope.Bubble.IsHitTestVisible);
        Assert.True(pet.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PetBreakStart").IsEffectivelyVisible);
        pet.MouseMove(new(-10, -10)); Layout(pet); Assert.True(scope.Bubble.IsVisible);
        scope.Hover(); scope.Runtime.StartBreak(); Layout(pet);
        Assert.True(Text(pet, "PetBreakTimer").IsEffectivelyVisible);
        Assert.False(Text(pet, "PetHoverTime").IsEffectivelyVisible);
        scope.Runtime.CompleteBreak(); Layout(pet);
        Assert.Equal("스트레칭을 마쳤어요!", Text(pet, "PetBreakTitle").Text);
        scope.Runtime.Reminder.Cancel(); pet.RefreshSpeech(); Layout(pet);
        scope.Hover(); Assert.True(Text(pet, "PetHoverTime").IsEffectivelyVisible);
        Assert.Single(scope.Runtime.BreakHistory.Completions);
    }

    [AvaloniaFact]
    public async Task HoverDoesNotInterfereWithDraggingOrPersistAnAutomaticPosition()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        scope.Hover(); Assert.True(scope.Bubble.IsVisible);
        Assert.Null(scope.Runtime.Settings.PetX); Assert.Null(scope.Runtime.Settings.PetY);
        var point = pet.PetView.TranslatePoint(new(96, 96), pet)!.Value;
        var size = pet.ClientSize;
        pet.MouseDown(point, MouseButton.Left); Layout(pet); Assert.True(scope.Bubble.IsVisible);
        var start = pet.PetAnchor;
        pet.MouseMove(point + new Vector(25, 0)); Layout(pet);
        Assert.Equal(size, pet.ClientSize);
        Assert.Equal(start.X + 25, pet.PetAnchor.X);
        pet.MouseUp(point, MouseButton.Left); Layout(pet);
        Assert.Equal(pet.PetAnchor.X, scope.Runtime.Settings.PetX);
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task RepeatedClicksDoNotResizeOrMoveTheHoverWindow(bool original)
    {
        using var scope = new Scope(); await scope.Load(original); var pet = scope.Pet;
        scope.Hover();
        var size = pet.ClientSize; var position = pet.Position; var anchor = pet.PetAnchor;
        var petBounds = pet.PetView.Bounds; var changes = 0;
        pet.SizeChanged += (_, _) => changes++;
        for (var i = 0; i < 20; i++)
        {
            var point = pet.PetView.TranslatePoint(new(96, 96), pet)!.Value;
            pet.MouseDown(point, MouseButton.Left); pet.AdvanceCompanion(.06); Layout(pet);
            Assert.True(scope.Bubble.IsVisible);
            Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
            Assert.Equal(petBounds, pet.PetView.Bounds); Assert.Equal(anchor, pet.PetAnchor);
            point = pet.PetView.TranslatePoint(new(96, 96), pet)!.Value;
            pet.MouseUp(point, MouseButton.Left); pet.AdvanceCompanion(.08); Layout(pet);
            pet.RefreshSpeech(); Layout(pet);
            Assert.True(scope.Bubble.IsVisible);
            Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
            Assert.Equal(petBounds, pet.PetView.Bounds); Assert.Equal(anchor, pet.PetAnchor);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.Equal(0, changes);
        Assert.Empty(scope.Runtime.BreakHistory.Completions); Assert.False(scope.Runtime.Reminder.HasNotice);
        pet.MouseMove(new(-10, -10)); Layout(pet); Assert.False(scope.Bubble.IsVisible);
    }

    [AvaloniaFact]
    public async Task PoseChangesCannotCancelHoverButLeavingTheStationaryPetAreaDoes()
    {
        using var scope = new Scope(); await scope.Load(); var pet = scope.Pet;
        scope.Hover(); var size = pet.ClientSize;
        // The image shrinks away from a stationary cursor during squash. The
        // logical pet area still owns that hover until the cursor leaves it.
        pet.PetView.SetPose(new PetPose(.5, .5, .2)); Layout(pet);
        pet.MouseMove(new Point(pet.PetView.Bounds.X + 40, pet.PetView.Bounds.Y + 40)); Layout(pet);
        Assert.True(scope.Bubble.IsVisible); Assert.Equal(size, pet.ClientSize);
        pet.MouseMove(new Point(pet.PetView.Bounds.Right + 10, pet.PetView.Bounds.Y + 40)); Layout(pet);
        Assert.False(scope.Bubble.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData(BubbleDirection.Top)] [InlineData(BubbleDirection.Bottom)]
    [InlineData(BubbleDirection.Left)] [InlineData(BubbleDirection.Right)]
    public async Task HoverWindowMovesOnlyAfterThePetVisualHasItsNewOffset(BubbleDirection direction)
    {
        using var scope = new Scope(); await scope.Load(original: true); var pet = scope.Pet;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { BubbleDirection = direction });
        Layout(pet);
        PixelPoint VisualOrigin()
        {
            var local = pet.PetView.TranslatePoint(default, pet)!.Value;
            return new(pet.Position.X + (int)Math.Round(local.X * pet.DesktopScaling),
                pet.Position.Y + (int)Math.Round(local.Y * pet.DesktopScaling));
        }
        var expected = VisualOrigin();
        var intermediate = new List<PixelPoint>();
        pet.PositionChanged += (_, _) => intermediate.Add(VisualOrigin());
        for (var i = 0; i < 3; i++)
        {
            scope.Hover();
            pet.MouseMove(new(-10, -10)); Layout(pet);
            Assert.Equal(expected, VisualOrigin());
        }
        Assert.All(intermediate, actual => Assert.Equal(expected, actual));
    }

    [AvaloniaTheory]
    [InlineData(BubbleDirection.Top)] [InlineData(BubbleDirection.Bottom)]
    [InlineData(BubbleDirection.Left)] [InlineData(BubbleDirection.Right)]
    public async Task HoverEntryAndExitNeverResizeOrMoveTheNativePetSurface(BubbleDirection direction)
    {
        using var scope = new Scope(); await scope.Load(original: true); var pet = scope.Pet;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { BubbleDirection = direction }); Layout(pet);
        var position = pet.Position; var size = pet.ClientSize; var origin = pet.PetView.TranslatePoint(default, pet);
        var anchor = pet.PetAnchor; var moves = 0; var sizes = 0;
        pet.PositionChanged += (_, _) => moves++; pet.SizeChanged += (_, _) => sizes++;
        for (var i = 0; i < 5; i++)
        {
            scope.Hover(); Assert.True(scope.Bubble.IsVisible);
            Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
            Assert.Equal(origin, pet.PetView.TranslatePoint(default, pet)); Assert.Equal(anchor, pet.PetAnchor);
            // The visible clock and the reserved hidden area both remain click-through.
            var clockPoint = scope.Bubble.TranslatePoint(new Point(5, 5), pet)!.Value;
            Assert.False(pet.AcceptsPointerAt(clockPoint));
            pet.MouseMove(new(-10, -10)); Layout(pet); Assert.False(scope.Bubble.IsVisible);
            Assert.False(pet.AcceptsPointerAt(clockPoint));
            Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
            Assert.Equal(origin, pet.PetView.TranslatePoint(default, pet)); Assert.Equal(anchor, pet.PetAnchor);
        }
        Assert.Equal(0, sizes); Assert.Equal(0, moves);
    }

    [AvaloniaFact]
    public async Task PressBeforeQueuedHoverLayoutDoesNotJumpWhenDraggingStarts()
    {
        using var scope = new Scope(); await scope.Load(original: true); var pet = scope.Pet;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { BubbleDirection = BubbleDirection.Top });
        Layout(pet);
        var before = pet.PetAnchor;
        var local = (from y in Enumerable.Range(1, 191) from x in Enumerable.Range(1, 191)
            let sample = new Point(x, y) where pet.PetView.OpaqueAt(sample, includeEdgeTolerance: false) select sample).First();
        var point = pet.PetView.TranslatePoint(local, pet)!.Value;
        // Enter queues the hover layout; pressing may arrive before that callback.
        pet.MouseDown(point, MouseButton.Left);
        Layout(pet);
        Assert.True(scope.Bubble.IsVisible);
        var movedPoint = pet.PetView.TranslatePoint(local + new Vector(25, 0), pet)!.Value;
        pet.MouseMove(movedPoint); Layout(pet);
        Assert.Equal(new PixelPoint(before.X + 25, before.Y), pet.PetAnchor);
        pet.MouseUp(movedPoint, MouseButton.Left);
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
