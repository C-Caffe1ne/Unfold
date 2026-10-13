using System.Diagnostics;
using System.Reflection;
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
public class PetReminderActionLifetimeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task OneShotAttentionAndStretchKeepTheirTerminalPoseUntilTheirNoticeEnds(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: false);
        await scope.Invite();
        await Until(() => Completed(scope.Pet.PetView));
        await AssertTerminalAction(scope, "attention", PetNotice.Invitation);
        scope.Runtime.SnoozeBreak(); await AssertIdle(scope);

        await scope.StartBreak();
        await Until(() => Completed(scope.Pet.PetView));
        await AssertTerminalAction(scope, "stretch", PetNotice.Resting);
        var session = scope.Runtime.Reminder.Session;
        for (var i = 0; i < 12; i++) scope.Pet.AdvanceCompanion(.2);
        Assert.Same(session, scope.Runtime.Reminder.Session); Assert.False(scope.Pet.IsRoaming);
        Assert.Equal("stretch", scope.Pet.ActiveAnimation);
        scope.Runtime.CompleteBreak();
        Assert.Equal(PetNotice.Completed, scope.Runtime.Reminder.Notice);
        Assert.Equal("celebrate", scope.Pet.ActiveAnimation); Assert.False(scope.Pet.PetView.Repeats);
        await Until(() => scope.Pet.ActiveAnimation == "idle");
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task LoopingAttentionAndStretchKeepRepeatingUntilSnoozeOrStop(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: true);
        await scope.Invite(); await PastOneCycle(scope, "attention");
        AssertAction(scope, "attention", PetNotice.Invitation, repeat: true);
        Assert.False(Completed(scope.Pet.PetView));
        scope.Runtime.SnoozeBreak(); await AssertIdle(scope);
        await scope.StartBreak(); await PastOneCycle(scope, "stretch");
        AssertAction(scope, "stretch", PetNotice.Resting, repeat: true);
        Assert.False(Completed(scope.Pet.PetView)); Assert.False(scope.Pet.IsRoaming);
        scope.Runtime.Stop(); await AssertIdle(scope);
        Assert.True(scope.Runtime.Clock.Stopped);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task AutomaticSnoozeEndsAHeldAttentionAndSchedulesTheConfiguredDelay(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: false);
        scope.Runtime.Clock.Start(TimeSpan.Zero);
        await scope.Invite(); await Until(() => Completed(scope.Pet.PetView));
        var session = scope.Runtime.Reminder.Session!;
        scope.Runtime.Reminder.Tick(scope.Runtime.Reminder.NoticeExpiresAt!.Value);
        RefreshNotice(scope.Runtime); await AssertIdle(scope);
        Assert.Equal(BreakSessionState.Snoozed, session.State);
        Assert.Equal(1, scope.Runtime.Reminder.ConsecutiveSnoozes);
        Assert.Equal(TimeSpan.FromMinutes(scope.Runtime.Settings.SnoozeMinutes), scope.Runtime.Clock.Remaining);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task AdvanceAttentionHoldsItsLastPoseUntilTheAdvanceNoticeExpires(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: false);
        await scope.Runtime.ShowReminderPreview(PetNotice.Advance);
        _ = scope.Pet.React("attention");
        await Until(() => Completed(scope.Pet.PetView));
        await AssertTerminalAction(scope, "attention", PetNotice.Advance);
        var source = scope.Runtime.PresentedReminder;
        source.Tick(source.NoticeExpiresAt!.Value); RefreshNotice(scope.Runtime);
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.False(scope.Pet.Overlay.Bubble.IsVisible);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")] [InlineData("glb")]
    public async Task CelebrateIgnoresLoopMetadataAndFinishesTheWholeClipOnce(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: true);
        var clip = await scope.Runtime.Clip("celebrate");
        var duration = Duration(clip); var completions = 0; var terminalIndex = -1; double elapsedAtEnd = 0;
        await scope.StartBreak();
        scope.Pet.PetView.Completed += () =>
        {
            if (scope.Pet.ActiveAnimation != "celebrate") return;
            completions++; terminalIndex = Field<int>(scope.Pet.PetView, "index");
            elapsedAtEnd = Field<Stopwatch>(scope.Pet.PetView, "elapsed").Elapsed.TotalSeconds;
        };
        scope.Runtime.CompleteBreak();
        Assert.True(scope.Runtime.Selected!.Manifest.Animations["celebrate"].Loop);
        Assert.False(scope.Pet.PetView.Repeats); Assert.Equal("celebrate", scope.Pet.ActiveAnimation);
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.Equal(1, completions); Assert.Equal(clip.Count - 1, terminalIndex);
        Assert.True(elapsedAtEnd >= duration, $"Celebrate ended at {elapsedAtEnd}s before its {duration}s clip finished.");
        Assert.Equal(PetNotice.Completed, scope.Runtime.Reminder.Notice);
    }

    [AvaloniaTheory]
    [InlineData("media")] [InlineData("original")]
    public async Task ALongCelebrateOutlivesItsFiveSecondNoticeAndReachesItsLastFrameOnce(string kind)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: true);
        var clip = await scope.SlowCelebrate(); var completions = 0; var terminalIndex = -1;
        await scope.StartBreak();
        scope.Pet.PetView.Completed += () =>
        {
            if (scope.Pet.ActiveAnimation != "celebrate") return;
            completions++; terminalIndex = Field<int>(scope.Pet.PetView, "index");
        };
        scope.Runtime.CompleteBreak(); Assert.Equal("celebrate", scope.Pet.ActiveAnimation);
        ExpireCompletedNotice(scope); await Task.Delay(30, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        Assert.Equal("celebrate", scope.Pet.ActiveAnimation); Assert.False(Completed(scope.Pet.PetView));
        Assert.False(scope.Pet.Overlay.Bubble.IsVisible); Assert.Equal(0, completions);
        // Exercise the real terminal path using the same boundary seam as AnimationMemoryTests.
        // The notice clock has expired; the animation boundary remains independently controlled.
        CompleteSpriteClip(scope.Pet.PetView);
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.Equal(1, completions); Assert.Equal(clip.Count - 1, terminalIndex);
        Assert.Equal(PetNotice.None, scope.Runtime.Reminder.Notice);
    }

    [AvaloniaFact]
    public async Task StopCancelsALongGlbCelebrateEvenAfterItsNoticeHasNaturallyExpired()
    {
        using var scope = new Scope(); await scope.Select("glb", alarmLoop: true, motionDuration: 6);
        var clip = Assert.IsType<GlbAnimationFrames>(await scope.Runtime.Clip("celebrate"));
        Assert.Equal(6, clip.DurationSeconds); Assert.Equal(1, scope.Runtime.Selected!.Manifest.Animations["celebrate"].Speed);
        await scope.StartBreak(); scope.Runtime.CompleteBreak();
        Assert.Equal("celebrate", scope.Pet.ActiveAnimation); Assert.False(scope.Pet.PetView.Repeats);
        ExpireCompletedNotice(scope); Dispatcher.UIThread.RunJobs();
        Assert.Equal("celebrate", scope.Pet.ActiveAnimation); Assert.False(Completed(scope.Pet.PetView));
        scope.Runtime.Stop(); await AssertIdle(scope);
        Assert.True(scope.Runtime.Clock.Stopped); Assert.False(scope.Pet.IsCompletingReminder);
    }

    [AvaloniaTheory]
    [InlineData("media", false)] [InlineData("media", true)]
    [InlineData("original", false)] [InlineData("original", true)]
    [InlineData("glb", false)] [InlineData("glb", true)]
    public async Task AClickReturnsToTheSameValidAttentionOrStretchAfterItsReaction(string kind, bool resting)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: false);
        if (resting) await scope.StartBreak(); else await scope.Invite();
        var key = resting ? "stretch" : "attention"; var notice = resting ? PetNotice.Resting : PetNotice.Invitation;
        await Until(() => Completed(scope.Pet.PetView));
        var session = scope.Runtime.Reminder.Session;
        var point = scope.PaintedPoint();
        scope.Pet.MouseDown(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
        scope.Pet.MouseUp(point, MouseButton.Left); Dispatcher.UIThread.RunJobs();
        await Until(() => scope.Pet.ActiveAnimation == key && scope.Pet.PointerPhase == PetPointerPhase.None);
        await Until(() => Completed(scope.Pet.PetView));
        Assert.Same(session, scope.Runtime.Reminder.Session);
        await AssertTerminalAction(scope, key, notice);
        Assert.False(scope.Pet.IsRoaming);
    }

    [AvaloniaTheory]
    [InlineData("original", false)] [InlineData("original", true)]
    [InlineData("glb", false)] [InlineData("glb", true)]
    public async Task PickupAndReleaseRestoreTheSameStillValidReminderAction(string kind, bool resting)
    {
        using var scope = new Scope(); await scope.Select(kind, alarmLoop: false);
        if (resting) await scope.StartBreak(); else await scope.Invite();
        var key = resting ? "stretch" : "attention"; var notice = resting ? PetNotice.Resting : PetNotice.Invitation;
        await Until(() => Completed(scope.Pet.PetView));
        var session = scope.Runtime.Reminder.Session;
        Assert.True(scope.Pet.HasPointerArt);
        scope.Pet.BeginCompanionPress(); scope.Pet.AdvanceCompanion(PetPose.LiftDelay);
        Assert.True(scope.Runtime.Selected!.Manifest.Animations["pickup"].Loop);
        Assert.Equal(kind == "glb" ? PetPointerPhase.Held : PetPointerPhase.Pickup, scope.Pet.PointerPhase);
        Assert.Equal(kind == "glb", scope.Pet.PetView.Repeats);
        scope.Pet.ReleaseCompanionPress(clicked: false);
        Assert.True(scope.Runtime.Selected.Manifest.Animations["land"].Loop);
        Assert.Equal(PetPointerPhase.Recovering, scope.Pet.PointerPhase);
        Assert.Equal("land", scope.Pet.ActiveAnimation); Assert.False(scope.Pet.PetView.Repeats);
        await Until(() => scope.Pet.PointerPhase == PetPointerPhase.None && scope.Pet.ActiveAnimation == key);
        await Until(() => Completed(scope.Pet.PetView));
        Assert.Same(session, scope.Runtime.Reminder.Session);
        await AssertTerminalAction(scope, key, notice);
        Assert.False(scope.Pet.IsRoaming);
    }

    [AvaloniaFact]
    public async Task ATemporarilyShownPetStaysVisibleForCelebrateAfterNoticeExpiryAndHidesAtItsEnd()
    {
        using var scope = new Scope(); await scope.Select("original", alarmLoop: true);
        await scope.SlowCelebrate(); await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = false });
        Assert.False(scope.Pet.IsVisible);
        await scope.StartBreak(); scope.Runtime.CompleteBreak();
        Assert.True(scope.Pet.IsVisible); Assert.True(scope.Pet.IsCompletingReminder);
        ExpireCompletedNotice(scope); Dispatcher.UIThread.RunJobs();
        Assert.True(scope.Pet.IsVisible); Assert.True(scope.Pet.IsCompletingReminder);
        Assert.Equal("celebrate", scope.Pet.ActiveAnimation); Assert.False(scope.Pet.Overlay.Bubble.IsVisible);
        CompleteSpriteClip(scope.Pet.PetView);
        await Until(() => !scope.Pet.IsVisible);
        Assert.False(scope.Pet.IsCompletingReminder); Assert.Equal(PetNotice.None, scope.Runtime.Reminder.Notice);
        Assert.False(scope.Runtime.Settings.ShowPet);
    }

    private static void AssertAction(Scope scope, string key, PetNotice notice, bool repeat)
    {
        Assert.Equal(key, scope.Pet.ActiveAnimation); Assert.Equal(repeat, scope.Pet.PetView.Repeats);
        Assert.Equal(notice, scope.Runtime.PresentedReminder.Notice); Assert.True(scope.Pet.Overlay.Bubble.IsVisible);
    }
    private static async Task AssertTerminalAction(Scope scope, string key, PetNotice notice)
    {
        var clip = await scope.Runtime.Clip(key);
        AssertAction(scope, key, notice, repeat: false); Assert.True(Completed(scope.Pet.PetView));
        Assert.Equal(clip.Count - 1, Field<int>(scope.Pet.PetView, "index"));
        if (clip is GlbAnimationFrames live)
        {
            var current = Field<PixelImage>(scope.Pet.PetView, "liveImage");
            var end = new uint[current.Pixels.Length]; live.RenderTerminalFrameInto(current.Width, end);
            Assert.Equal(end, current.Pixels);
        }
        Dispatcher.UIThread.RunJobs(); scope.Pet.RefreshSpeech();
        AssertAction(scope, key, notice, repeat: false); Assert.True(Completed(scope.Pet.PetView));
    }
    private static async Task AssertIdle(Scope scope)
    {
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.Equal(PetNotice.None, scope.Runtime.Reminder.Notice); Assert.Null(scope.Runtime.Reminder.Session);
        Assert.False(scope.Pet.Overlay.Bubble.IsVisible); Assert.False(scope.Pet.IsRoaming);
        Assert.True(scope.Pet.PetView.Repeats);
    }
    private static async Task PastOneCycle(Scope scope, string key)
    {
        var seconds = Duration(await scope.Runtime.Clip(key));
        await Task.Delay(TimeSpan.FromSeconds(seconds + .03), TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs(); scope.Pet.RefreshSpeech();
    }
    private static double Duration(IReadOnlyList<AnimationFrame> clip) =>
        clip is GlbAnimationFrames live ? live.DurationSeconds : clip.Sum(frame => frame.Duration.TotalSeconds);
    private static T Field<T>(AnimationView view, string name) =>
        (T)typeof(AnimationView).GetField(name, Private)!.GetValue(view)!;
    private static bool Completed(AnimationView view) => Field<bool>(view, "completed");
    private static void CompleteSpriteClip(AnimationView view)
    {
        Assert.Null(typeof(AnimationView).GetField("live", Private)!.GetValue(view));
        typeof(AnimationView).GetField("totalMs", Private)!.SetValue(view, double.Epsilon);
        typeof(AnimationView).GetMethod("Advance", Private)!.Invoke(view, null);
    }
    private static void RefreshNotice(AppRuntime runtime) => typeof(AppRuntime).GetMethod("RefreshPetNotice", Private)!.Invoke(runtime, null);
    private static void ExpireCompletedNotice(Scope scope)
    {
        Assert.Equal(PetNotice.Completed, scope.Runtime.Reminder.Notice);
        scope.Runtime.Reminder.Tick(scope.Runtime.Reminder.NoticeExpiresAt!.Value);
        Assert.Equal(PetNotice.None, scope.Runtime.Reminder.Notice); RefreshNotice(scope.Runtime);
    }
    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 400 && !ready(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(10, TestContext.Current.CancellationToken); }
        Assert.True(ready(), "The pet action did not follow its notice lifetime or finish its assigned clip.");
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Select(string kind, bool alarmLoop, float motionDuration = .18f)
        {
            bool Repeats(string key) => key is "attention" or "stretch" ? alarmLoop : true;
            string id;
            if (kind == "glb")
            {
                var input = Path.Combine(temp.Path, "reminder.glb");
                File.WriteAllBytes(input, GlbTests.Fixture(rootMotion: false, morph: true, duration: motionDuration));
                var draft = new GlbPetDraft(input);
                foreach (var key in GlbPetDraft.Actions) draft.Set(key, "Idle", Repeats(key), speed: 1);
                id = draft.Save(Runtime.Library).Manifest.Id;
            }
            else
            {
                var artwork = new SpriteFixture(16, 16) { Name = "Reminder action lifetime" };
                for (var y = 5; y <= 10; y++) for (var x = 5; x <= 10; x++) artwork.Pixels[y * 16 + x] = 0xffffffff;
                var package = Runtime.Library.InstallFixture(artwork);
                var keys = kind == "original" ? OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips).Distinct()
                    : CustomPetDraft.Actions;
                Dictionary<string, AnimationDefinition> mappings;
                if (kind == "media")
                {
                    File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"), Path.Combine(package.DirectoryPath, "motion.gif"));
                    mappings = keys.ToDictionary(key => key, key => new AnimationDefinition(Gif: "motion.gif", Loop: Repeats(key)));
                }
                else mappings = keys.ToDictionary(key => key, key => new AnimationDefinition([0, 0, 0], 30, Loop: Repeats(key)));
                var manifest = package.Manifest with { BehaviorProfile = kind == "original" ? OriginalCompanion.Profile : null, Animations = mappings };
                AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
                id = package.Manifest.Id;
            }
            await Runtime.Reload(); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = id, ShowPet = true,
                ShowPetTimer = true, ReminderSoundsEnabled = false });
            foreach (var key in Runtime.Selected!.Manifest.Animations.Keys) await Runtime.Clip(key);
            Pet.Position = new(500, 400); Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public async Task Invite()
        {
            await Runtime.ShowReminder(); await Until(() => Pet.ActiveAnimation == "attention");
            Assert.Equal(PetNotice.Invitation, Runtime.Reminder.Notice);
        }
        public async Task StartBreak()
        {
            await Invite(); Runtime.StartBreak(); await Until(() => Pet.ActiveAnimation == "stretch");
            Assert.Equal(PetNotice.Resting, Runtime.Reminder.Notice);
        }
        public async Task<IReadOnlyList<AnimationFrame>> SlowCelebrate()
        {
            var original = await Runtime.Clip("celebrate");
            Assert.IsNotType<GlbAnimationFrames>(original);
            IReadOnlyList<AnimationFrame> slow = original.Select(frame => frame with { Duration = TimeSpan.FromSeconds(6d / original.Count) }).ToArray();
            new ClipCacheTestAccess(Runtime)[$"{Runtime.Selected!.DirectoryPath}:celebrate"] = Task.FromResult(slow);
            Assert.True(Duration(slow) > PetReminder.NoticeDuration.TotalSeconds);
            return slow;
        }
        public Point PaintedPoint()
        {
            for (var y = 1; y < Pet.PetView.Bounds.Height; y += 3)
            for (var x = 1; x < Pet.PetView.Bounds.Width; x += 3)
                if (Pet.PetView.OpaqueAt(new Point(x, y), includeEdgeTolerance: false))
                    return Pet.PetView.TranslatePoint(new Point(x, y), Pet)!.Value;
            throw new InvalidOperationException("Fixture has no painted pet pixels.");
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
