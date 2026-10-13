using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetMotionContinuationTests
{
    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    [InlineData("default-cat")] [InlineData("bori-rabbit")]
    [InlineData("puppy-dog")] [InlineData("hedgehog")] [InlineData("penguin")]
    public async Task PointerEntryNeverShowsHoverOrReplacesTheCurrentMotion(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var pet = scope.Pet;
        var size = pet.ClientSize; var anchor = pet.PetAnchor;
        pet.MouseMove(scope.PaintedPoint()); Layout(pet);
        await Task.Delay(40, TestContext.Current.CancellationToken); Layout(pet);
        Assert.Equal("idle", pet.ActiveAnimation);
        Assert.False(pet.Overlay.Bubble.IsVisible);
        await pet.React("hover");
        Assert.Equal("idle", pet.ActiveAnimation);
        Assert.Equal(size, pet.ClientSize); Assert.Equal(anchor, pet.PetAnchor);
    }

    [AvaloniaTheory]
    [InlineData("original")] [InlineData("glb")]
    public async Task StretchKeepsItsEndPoseUnderThePointerAndTransparentBackgroundUntilCompletion(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind); var pet = scope.Pet;
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        pet.MouseMove(scope.PaintedPoint()); Layout(pet);
        await Until(() => (bool)typeof(AnimationView).GetField("completed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet.PetView)!);
        foreach (var point in new[] { scope.PaintedPoint(), pet.PetView.TranslatePoint(new(1, 1), pet)!.Value })
        {
            pet.MouseMove(point); Layout(pet); Assert.True(pet.IsPointerOver);
            pet.AdvanceCompanion(.1);
            Assert.Equal("stretch", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
            Assert.True(Running(pet.PetView));
            Assert.False(((Stopwatch)typeof(AnimationView).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet.PetView)!).IsRunning);
        }
        Assert.False(pet.AcceptsPointerAt(pet.PetView.TranslatePoint(new(1, 1), pet)!.Value));
        Assert.Equal(PetNotice.Resting, scope.Runtime.Reminder.Notice);
    }

    [AvaloniaFact]
    public async Task AOneShotSpriteWalkReturnsToIdleAfterItsLastFrameOutsideABreak()
    {
        using var scope = new Scope(); await scope.Select("original", walkLoop: false); var pet = scope.Pet;
        await pet.React("walk");
        Assert.Equal("idle", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
        Assert.True(Running(pet.PetView)); Assert.True(pet.PetView.Repeats);
        Assert.Equal(PetNotice.None, scope.Runtime.Reminder.Notice);
    }

    [AvaloniaFact]
    public async Task ClickWithoutAClickClipStillRestoresAPendingIdleAfterACompletedMotion()
    {
        using var scope = new Scope(); await scope.Select("glb"); var pet = scope.Pet;
        Assert.True(pet.HasPointerArt); Assert.False(scope.Runtime.Selected!.Manifest.Animations.ContainsKey("click"));
        var idle = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
        new ClipCacheTestAccess(scope.Runtime)[$"{scope.Runtime.Selected.DirectoryPath}:idle"] = idle.Task;
        var completed = false;
        pet.PetView.Completed += () => completed = true;
        var reaction = pet.React("sleep"); await Until(() => completed);
        Assert.Equal("sleep", pet.ActiveAnimation); Assert.False(reaction.IsCompleted);
        pet.BeginCompanionPress(); pet.ReleaseCompanionPress(clicked: true);
        idle.SetResult(scope.Runtime.Selected.LoadAnimation("idle"));
        await reaction; await Until(() => pet.ActiveAnimation == "idle");
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        Assert.True(Running(pet.PetView)); Assert.True(pet.PetView.Repeats);
    }

    private static bool Running(AnimationView view) => (bool)typeof(AnimationView)
        .GetField("running", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;

    [AvaloniaFact]
    public async Task CompletingThePreviousClipDuringDecodeDoesNotSkipTheNewMotion()
    {
        using var scope = new Scope(); await scope.Select("original"); var pet = scope.Pet;
        await scope.Runtime.Clip("look");
        var oldCompleted = false;
        pet.PetView.Completed += () => oldCompleted = true;
        var oldReaction = pet.React("look");
        var pending = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
        new ClipCacheTestAccess(scope.Runtime)[$"{scope.Runtime.Selected!.DirectoryPath}:click"] = pending.Task;
        var nextReaction = pet.React("click");
        await Until(() => oldCompleted); Assert.False(nextReaction.IsCompleted);
        pending.SetResult(scope.Runtime.Selected.LoadAnimation("click"));
        await Task.Delay(30, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        Assert.Equal("click", pet.ActiveAnimation); Assert.False(nextReaction.IsCompleted);
        await oldReaction; await nextReaction;
        Assert.Equal("idle", pet.ActiveAnimation); Assert.True(pet.PetView.Repeats);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("glb")]
    public async Task LegacyHoverMappingsStayReadableButAreNotOfferedForAuthoring(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var package = scope.Runtime.Selected!;
        Assert.True(package.Manifest.Animations.ContainsKey("hover"));
        Assert.DoesNotContain("hover", CustomPetDraft.Actions); Assert.DoesNotContain("hover", GlbPetDraft.Actions);
        if (kind == "glb")
        {
            var draft = new GlbPetDraft(package);
            Assert.True(draft.Mappings.ContainsKey("hover"));
            Assert.Throws<InvalidDataException>(() => draft.Set("hover", "Idle"));
        }
        else Assert.DoesNotContain("hover", new CustomPetDraft(package).AvailableActions);
    }
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready(), "Pet motion did not continue or restore its base animation.");
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Select(string kind, bool walkLoop = true)
        {
            string id;
            if (kind == "glb")
            {
                var input = Path.Combine(temp.Path, "motion.glb"); File.WriteAllBytes(input, GlbTests.Fixture());
                var draft = new GlbPetDraft(input);
                foreach (var key in new[] { "stretch", "walk", "sleep" }.Concat(GlbPetDraft.PointerClips))
                    draft.Set(key, "Idle", key == "walk", speed: 3);
                draft.Mappings["hover"] = new(ModelClip: "Idle", Loop: true);
                id = draft.Save(Runtime.Library).Manifest.Id;
            }
            else if (kind is "media" or "original")
            {
                var document = new SpriteFixture(16, 16) { Name = "Motion continuation" };
                for (var y = 5; y <= 10; y++) for (var x = 5; x <= 10; x++) document.Pixels[y * 16 + x] = 0xFFFFFFFF;
                var package = Runtime.Library.InstallFixture(document);
                var keys = kind == "original" ? OriginalCompanion.RequiredClips : CustomPetDraft.Actions;
                var mappings = keys.ToDictionary(key => key, key => new AnimationDefinition([0, 0, 0, 0], 20,
                    Loop: key == "idle" || key == "walk" && walkLoop));
                mappings["hover"] = new([0, 0], 20, Loop: true);
                var manifest = package.Manifest with { BehaviorProfile = kind == "original" ? OriginalCompanion.Profile : null, Animations = mappings };
                AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
                id = package.Manifest.Id;
            }
            else
            {
                await Runtime.Start(true, true); id = kind;
                Assert.Contains(Runtime.Characters, c => c.Manifest.Id == id);
            }
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = id, ShowPet = true, ReminderSoundsEnabled = false });
            Pet.Position = new(500, 400); Layout(Pet);
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
