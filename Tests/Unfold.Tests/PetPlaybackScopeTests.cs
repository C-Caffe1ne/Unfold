using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetPlaybackScopeTests
{
    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task AlarmLoopsEndWithTheirBubbleEvenWhenTheTimerOverlayStaysVisible(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var runtime = scope.Runtime; var pet = scope.Pet;

        await runtime.ShowReminder();
        AssertReaction(pet, "attention", repeat: true);
        runtime.SnoozeBreak();
        await AssertIdleWithoutBubble(scope);

        await runtime.ShowReminder(); runtime.StartBreak();
        AssertReaction(pet, "stretch", repeat: true);
        runtime.Stop();
        await AssertIdleWithoutBubble(scope);

        await runtime.ShowReminder(); runtime.StartBreak();
        AssertReaction(pet, "stretch", repeat: true);
        runtime.CompleteBreak();
        AssertReaction(pet, "celebrate", repeat: false);
        runtime.Reminder.Tick(runtime.Reminder.NoticeExpiresAt!.Value);
        pet.RefreshSpeech();
        await AssertIdleWithoutBubble(scope);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task ClosingTheBubbleCancelsALongOneShotBeforeItsLastFrame(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, longAttention: true);
        var pet = scope.Pet;
        var frames = await scope.Runtime.Clip("attention");
        var duration = frames is GlbAnimationFrames live ? live.DurationSeconds : frames.Sum(frame => frame.Duration.TotalSeconds);
        Assert.True(duration >= 10);
        await scope.Runtime.ShowReminder();
        AssertReaction(pet, "attention", repeat: false);
        var completed = 0;
        pet.PetView.Completed += () => completed++;

        scope.Runtime.SnoozeBreak();
        await AssertIdleWithoutBubble(scope);
        Assert.Equal(0, completed);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task MomentaryActionsPlayOnceAndReturnToIdleDespiteLoopMetadata(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var pet = scope.Pet;
        var actions = kind == "media" ? new[] { "click", "pointerUp" } : new[] { "click", "land", "yawn", "sleep" };
        foreach (var key in actions)
        {
            Assert.True(scope.Runtime.Selected!.Manifest.Animations[key].Loop);
            var reaction = pet.React(key);
            AssertReaction(pet, key, repeat: false, hasBubble: false);
            await Until(() => reaction.IsCompleted); await reaction;
            Assert.Equal("idle", pet.ActiveAnimation);
            Assert.True(pet.PetView.Repeats);
        }
    }

    [AvaloniaTheory]
    [InlineData("original", false)] [InlineData("original", true)]
    [InlineData("glb", false)] [InlineData("glb", true)]
    public async Task LiftKeepsItsConfiguredPlaybackUntilReleaseAndLandFinishesOnce(string kind, bool heldLoop)
    {
        using var scope = new Scope(); await scope.Select(kind, heldLoop: heldLoop);
        var pet = scope.Pet;
        Assert.True(pet.HasPointerArt);
        Assert.Equal(kind != "glb" || heldLoop, scope.Runtime.Selected!.Manifest.Animations["pickup"].Loop);
        Assert.True(scope.Runtime.Selected.Manifest.Animations["land"].Loop);
        pet.BeginCompanionPress(); pet.AdvanceCompanion(PetPose.LiftDelay);
        Assert.Equal(kind == "glb" && heldLoop ? PetPointerPhase.Held : PetPointerPhase.Pickup, pet.PointerPhase);
        AssertReaction(pet, "pickup", repeat: kind == "glb" && heldLoop, hasBubble: false);
        await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        var holdingAction = kind == "glb" ? "pickup" : "held";
        AssertReaction(pet, holdingAction, repeat: heldLoop, hasBubble: false);
        if (kind == "glb")
        {
            Assert.DoesNotContain("held", scope.Runtime.Selected.Manifest.Animations.Keys);
            await pet.React("held");
            Assert.Equal("pickup", pet.ActiveAnimation);
        }
        if (!heldLoop)
        {
            await Until(() => PlaybackCompleted(pet.PetView));
            pet.AdvanceCompanion(.2);
            Assert.Equal(PetPointerPhase.Held, pet.PointerPhase);
            Assert.Equal(holdingAction, pet.ActiveAnimation);
        }
        else
        {
            await Task.Delay(450, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
            Assert.False(PlaybackCompleted(pet.PetView));
            AssertReaction(pet, holdingAction, repeat: true, hasBubble: false);
        }
        pet.ReleaseCompanionPress(clicked: false);
        Assert.Equal(PetPointerPhase.Recovering, pet.PointerPhase);
        AssertReaction(pet, "land", repeat: false, hasBubble: false);
        await Until(() => pet.PointerPhase == PetPointerPhase.None && pet.ActiveAnimation == "idle");
        Assert.True(pet.PetView.Repeats);
    }

    [AvaloniaFact]
    public async Task MediaPointerDownRepeatsOnlyUntilReleaseAndPointerUpFinishesOnce()
    {
        using var scope = new Scope(); await scope.Select("media");
        var pet = scope.Pet; var point = scope.PaintedPoint();
        pet.MouseDown(point, MouseButton.Left);
        AssertReaction(pet, "pointerDown", repeat: true, hasBubble: false);
        pet.MouseMove(point + new Vector(20, 0));
        pet.MouseUp(point + new Vector(20, 0), MouseButton.Left);
        AssertReaction(pet, "pointerUp", repeat: false, hasBubble: false);
        await Until(() => pet.ActiveAnimation == "idle");
        Assert.True(pet.PetView.Repeats);

        var reaction = pet.React("pointerDown");
        AssertReaction(pet, "pointerDown", repeat: false, hasBubble: false);
        await Until(() => reaction.IsCompleted); await reaction;
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task MediaLiftRemainsUntilReleaseEvenWhenAReminderArrives(bool loop)
    {
        using var scope = new Scope(); await scope.Select("media", heldLoop: loop);
        var pet = scope.Pet; var point = scope.PaintedPoint();
        pet.MouseDown(point, MouseButton.Left);
        AssertReaction(pet, "pointerDown", repeat: loop, hasBubble: false);
        if (!loop) await Until(() => PlaybackCompleted(pet.PetView));
        await scope.Runtime.ShowReminder();
        await Task.Delay(350, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        AssertReaction(pet, "pointerDown", repeat: loop);
        pet.MouseUp(point, MouseButton.Left);
        AssertReaction(pet, "pointerUp", repeat: false);
        await Until(() => pet.ActiveAnimation == "attention");
        AssertReaction(pet, "attention", repeat: true);
        scope.Runtime.SnoozeBreak(); await AssertIdleWithoutBubble(scope);
    }

    [AvaloniaTheory]
    [InlineData("media", false)] [InlineData("media", true)]
    [InlineData("original", false)] [InlineData("original", true)] [InlineData("glb", false)]
    public async Task MomentaryMotionCompletesItsFullDurationAndFinalFrameBeforeIdle(string kind, bool pingPong)
    {
        using var scope = new Scope(); await scope.Select(kind, motionDuration: 1.8f, pingPong: pingPong);
        var pet = scope.Pet;
        foreach (var key in kind == "media" ? new[] { "click", "pointerUp" } : new[] { "click", "land", "yawn", "sleep" })
        {
            var clip = await scope.Runtime.Clip(key);
            if (clip is not GlbAnimationFrames)
            {
                clip = clip.Select(frame => frame with { Duration = TimeSpan.FromMilliseconds(150) }).ToArray();
                new ClipCacheTestAccess(scope.Runtime)[$"{scope.Runtime.Selected!.DirectoryPath}:{key}"] = Task.FromResult(clip);
            }
            var duration = clip is GlbAnimationFrames live ? live.DurationSeconds : clip.Sum(frame => frame.Duration.TotalSeconds);
            var count = clip.Count;
            if (pingPong && clip.Count > 2)
            {
                duration += clip.Skip(1).Take(clip.Count - 2).Sum(frame => frame.Duration.TotalSeconds);
                count += clip.Count - 2;
            }
            double observed = 0; var finalIndex = -1;
            void Finished()
            {
                if (pet.ActiveAnimation != key) return;
                observed = ((System.Diagnostics.Stopwatch)typeof(AnimationView).GetField("elapsed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet.PetView)!).Elapsed.TotalSeconds;
                finalIndex = (int)typeof(AnimationView).GetField("index", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet.PetView)!;
            }
            pet.PetView.Completed += Finished;
            try
            {
                var reaction = pet.React(key);
                await Task.Delay(80, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
                Assert.Equal(key, pet.ActiveAnimation); Assert.False(reaction.IsCompleted);
                await Until(() => reaction.IsCompleted); await reaction;
                Assert.True(observed >= duration, $"{key} completed at {observed}s before {duration}s.");
                Assert.Equal(count - 1, finalIndex); Assert.Equal("idle", pet.ActiveAnimation);
            }
            finally { pet.PetView.Completed -= Finished; }
        }
    }

    [AvaloniaTheory]
    [InlineData("media", false)] [InlineData("media", true)]
    [InlineData("original", false)] [InlineData("original", true)]
    [InlineData("glb", false)] [InlineData("glb", true)]
    public async Task LateNoticeDecodeCannotRestoreAClosedOrPreviousNotice(string kind, bool startBreak)
    {
        using var scope = new Scope(); await scope.Select(kind);
        await scope.Runtime.ShowReminder();
        var pet = scope.Pet;
        var pending = scope.Defer("attention");
        var oldReaction = pet.React("attention");
        Assert.False(oldReaction.IsCompleted);
        if (startBreak) scope.Runtime.StartBreak(); else scope.Runtime.SnoozeBreak();
        await Until(() => pet.ActiveAnimation == (startBreak ? "stretch" : "idle"));

        pending.SetResult(scope.Runtime.Selected!.LoadAnimation("attention"));
        await Until(() => oldReaction.IsCompleted); await oldReaction;
        AssertReaction(pet, startBreak ? "stretch" : "idle", repeat: true, hasBubble: startBreak);
        if (!startBreak) Assert.False(scope.Runtime.Reminder.HasNotice);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task ReplacingOrClosingAPreviewCancelsItsOldActionSource(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var runtime = scope.Runtime; var pet = scope.Pet;
        await runtime.ShowReminderPreview(PetNotice.Invitation);
        var firstSource = runtime.PresentedReminder;
        var first = pet.React("attention");
        await runtime.ShowReminderPreview(PetNotice.Invitation);
        Assert.NotSame(firstSource, runtime.PresentedReminder);
        await Until(() => first.IsCompleted); await first;
        AssertReaction(pet, "attention", repeat: true);
        runtime.CloseReminderPreview();
        await Until(() => pet.ActiveAnimation == "idle");
        Assert.False(runtime.PresentedReminder.HasNotice);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task SelectingAnotherCharacterDoesNotResumeThePreviousSessionAction(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind);
        var other = scope.Runtime.Library.InstallFixture(new SpriteFixture(16, 16) { Name = "Other reminder pet" });
        await scope.Runtime.Reload();
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        var session = scope.Runtime.Reminder.Session;
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { SelectedCharacterId = other.Manifest.Id });
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.Same(session, scope.Runtime.Reminder.Session);
        Assert.Equal(PetNotice.Resting, scope.Runtime.Reminder.Notice);
        Assert.NotEqual(session!.CharacterId, scope.Runtime.Selected!.Manifest.Id);
        scope.Pet.RefreshSpeech();
        Assert.Equal("idle", scope.Pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task OneShotIdleHoldsItsEndPoseAndAllowsTheNextAction(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, idleLoop: false);
        var pet = scope.Pet;
        await Until(() => PlaybackCompleted(pet.PetView));
        AssertReaction(pet, "idle", repeat: false, hasBubble: false);
        var reaction = pet.React("click");
        AssertReaction(pet, "click", repeat: false, hasBubble: false);
        await Until(() => reaction.IsCompleted); await reaction;
        await Until(() => PlaybackCompleted(pet.PetView));
        AssertReaction(pet, "idle", repeat: false, hasBubble: false);
    }

    private static void AssertReaction(PetWindow pet, string key, bool repeat, bool hasBubble = true)
    {
        Assert.Equal(key, pet.ActiveAnimation);
        Assert.Equal(repeat, pet.PetView.Repeats);
        Assert.Equal(hasBubble, pet.Overlay.Bubble.IsVisible);
    }
    private static async Task AssertIdleWithoutBubble(Scope scope)
    {
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.False(scope.Runtime.Reminder.HasNotice);
        AssertReaction(scope.Pet, "idle", repeat: true, hasBubble: false);
        Assert.True(scope.Pet.IsVisible);
        Assert.True(scope.Pet.Overlay.Timer.IsVisible);
        Assert.False(scope.Pet.IsRoaming);
    }
    private static bool PlaybackCompleted(AnimationView view) => (bool)typeof(AnimationView)
        .GetField("completed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 300 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready(), "Playback did not finish or follow its current state.");
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime);
        }
        public async Task Select(string kind, bool heldLoop = true, bool longAttention = false, bool idleLoop = true, float motionDuration = .3f, bool pingPong = false)
        {
            bool Repeats(string key) => key == "idle" ? idleLoop : key is "held" or "pointerDown" || key == "pickup" && kind == "glb"
                ? heldLoop : key != "attention" || !longAttention;
            string id;
            if (kind == "glb")
            {
                var input = Path.Combine(temp.Path, "scope.glb");
                File.WriteAllBytes(input, GlbTests.Fixture(morph: true, duration: longAttention ? 30 : motionDuration));
                var draft = new GlbPetDraft(input);
                foreach (var key in GlbPetDraft.Actions) draft.Set(key, "Idle", Repeats(key), speed: 3);
                id = draft.Save(Runtime.Library).Manifest.Id;
            }
            else
            {
                var document = new SpriteFixture(16, 16) { Name = "Playback scope" };
                for (var y = 5; y <= 10; y++) for (var x = 5; x <= 10; x++) document.Pixels[y * 16 + x] = 0xFFFFFFFF;
                var package = Runtime.Library.InstallFixture(document);
                var keys = kind == "original" ? OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips) : CustomPetDraft.Actions;
                Dictionary<string, AnimationDefinition> mappings;
                if (kind == "media")
                {
                    File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"), Path.Combine(package.DirectoryPath, "motion.gif"));
                    mappings = keys.ToDictionary(key => key, key => new AnimationDefinition(Gif: "motion.gif", Loop: Repeats(key), PingPong: pingPong));
                }
                else mappings = keys.ToDictionary(key => key, key => new AnimationDefinition([0, 0, 0, 0], 20, Loop: Repeats(key), PingPong: pingPong));
                var manifest = package.Manifest with { BehaviorProfile = kind == "original" ? OriginalCompanion.Profile : null, Animations = mappings };
                AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
                id = package.Manifest.Id;
            }
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with
            { SelectedCharacterId = id, ShowPet = true, ShowPetTimer = true, ReminderSoundsEnabled = false });
            foreach (var key in Runtime.Selected!.Manifest.Animations.Keys) await Runtime.Clip(key);
            if (longAttention && kind != "glb")
            {
                var frames = await Runtime.Clip("attention");
                IReadOnlyList<AnimationFrame> slow = frames.Select(frame => frame with { Duration = TimeSpan.FromSeconds(10) }).ToArray();
                new ClipCacheTestAccess(Runtime)[$"{Runtime.Selected.DirectoryPath}:attention"] = Task.FromResult(slow);
            }
            Pet.Position = new(500, 400);
            Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public TaskCompletionSource<IReadOnlyList<AnimationFrame>> Defer(string key)
        {
            var pending = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
            new ClipCacheTestAccess(Runtime)[$"{Runtime.Selected!.DirectoryPath}:{key}"] = pending.Task;
            return pending;
        }
        public Point PaintedPoint()
        {
            for (var y = 1; y < Pet.PetView.Bounds.Height; y += 3)
            for (var x = 1; x < Pet.PetView.Bounds.Width; x += 3)
                if (Pet.PetView.OpaqueAt(new(x, y), includeEdgeTolerance: false)) return Pet.PetView.TranslatePoint(new(x, y), Pet)!.Value;
            throw new InvalidOperationException("Fixture has no painted pet pixels.");
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
