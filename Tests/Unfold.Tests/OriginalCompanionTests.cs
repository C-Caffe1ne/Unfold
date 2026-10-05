using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class OriginalCompanionTests
{
    [Theory]
    [InlineData("default-cat")]
    [InlineData("bori-rabbit")]
    [InlineData("puppy-dog")]
    [InlineData("hedgehog")]
    [InlineData("penguin")]
    public void OriginalPacksRoundTripAllBehaviorsWithinBudgets(string id)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", id);
        var character = CharacterLibrary.LoadPackage(directory);
        Assert.True(character.HasOriginalBehavior);
        var report = CharacterAssetAudit.InspectPackage(directory);
        Assert.Empty(report.Errors); Assert.Empty(report.Warnings);
        Assert.True(character.HasPointerArt);
        Assert.Equal(OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips).Order(), report.Clips.Select(clip => clip.Key).Order());
        Assert.True(report.Clips.Sum(clip => clip.DecodedBytes) < 32 * 1024 * 1024);
        Assert.All(report.Clips, clip =>
        {
            Assert.Equal(256, clip.Width); Assert.Equal(256, clip.Height);
            Assert.Equal(clip.Frames, clip.TransparentFrames); Assert.Equal(0, clip.EmptyFrames);
            Assert.InRange(clip.DurationMs, OriginalCompanion.PointerClips.Contains(clip.Key) ? 250 : 500, 10000);
        });
        foreach (var key in OriginalCompanion.RequiredClips.Concat(OriginalCompanion.PointerClips))
            foreach (var frame in character.LoadAnimation(key))
            {
                var image = frame.Image;
                for (var edge = 0; edge < image.Width; edge++)
                {
                    Assert.InRange(image.Pixels[edge] >> 24, 0u, 25u);
                    Assert.InRange(image.Pixels[(image.Height - 1) * image.Width + edge] >> 24, 0u, 25u);
                    Assert.InRange(image.Pixels[edge * image.Width] >> 24, 0u, 25u);
                    Assert.InRange(image.Pixels[edge * image.Width + image.Width - 1] >> 24, 0u, 25u);
                }
            }
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, id + ".unfoldpet");
        CharacterPack.Create(directory, "2.0.0", file);
        using var pack = CharacterPack.Open(file);
        var installed = new CharacterLibrary(Path.Combine(temp.Path, "library")).Install(pack, null);
        Assert.True(installed.HasOriginalBehavior);
        Assert.True(installed.HasPointerArt);
        Assert.NotEqual(character.LoadAnimation("idle")[0].Image.Pixels, character.LoadAnimation("held")[0].Image.Pixels);
        Assert.Equal(character.LoadAnimation("walk")[0].Image.Pixels, installed.LoadAnimation("walk")[0].Image.Pixels);
    }

    [Fact]
    public void SnoozeStreakCountsSuccessfulInvitationsAndResetsOnStartOrCancel()
    {
        var reminder = new PetReminder();
        Assert.False(reminder.Snooze()); Assert.Equal(0, reminder.ConsecutiveSnoozes);
        for (var i = 1; i <= 4; i++)
        {
            reminder.Invite(new(BreakRoutines.All[0], "default-cat")); Assert.True(reminder.Snooze());
            Assert.False(reminder.Snooze()); Assert.Equal(i, reminder.ConsecutiveSnoozes);
        }
        reminder.Invite(new(BreakRoutines.All[0], "default-cat")); reminder.Start(TimeSpan.Zero);
        Assert.Equal(0, reminder.ConsecutiveSnoozes); Assert.False(reminder.Snooze());
        reminder.Cancel(); reminder.Invite(new(BreakRoutines.All[0], "default-cat")); reminder.Snooze();
        reminder.Cancel(); Assert.Equal(0, reminder.ConsecutiveSnoozes);
    }

    [Fact]
    public void IdleActionsAreSpacedNonRepeatingAndDoNotCatchUpAfterSleepOrInteraction()
    {
        var schedule = new CompanionIdleSchedule(new Random(25));
        for (var i = 0; i < 1000; i++) Assert.Null(schedule.Tick(.1, false));
        Assert.Null(schedule.Tick(3600, true));
        var choices = new List<string>(); var ticks = 0;
        while (choices.Count < 30)
        {
            ticks++;
            if (schedule.Tick(.1, true) is not { } key) continue;
            Assert.InRange(ticks, 200, 401); ticks = 0;
            if (choices.Count > 0) Assert.NotEqual(choices[^1], key);
            choices.Add(key);
        }
        Assert.Equal(new[] { "look", "sleep", "yawn" }, choices.Distinct().Order());
    }

    [Theory]
    [InlineData(1, 96)] [InlineData(1.5, 192)] [InlineData(2, 288)]
    public void WalkingKeepsThePetAndBubbleInsideNegativeOriginMonitorWithoutSleepJumps(double scale, double petSize)
    {
        var work = new PixelRect(-2560, -100, 2560, 1440);
        foreach (var direction in Enum.GetValues<BubbleDirection>())
        {
            var motion = new PetWanderMotion(new Random(42));
            var layout = PetBubbleLayout.Create(direction, true, DesignSystem.SpeechRestingHeight, petSize);
            var position = layout.Position(new(-2000, 300), scale, work);
            var start = position;
            for (var frame = 0; frame < 6000; frame++)
            {
                var next = motion.Step(position, layout.Size, work, scale, .04);
                Assert.InRange(next.X, work.X, work.Right - (int)Math.Ceiling(layout.Size.Width * scale));
                Assert.InRange(next.Y, work.Y, work.Bottom - (int)Math.Ceiling(layout.Size.Height * scale));
                Assert.InRange(Math.Abs(next.X - position.X), 0, 4);
                Assert.InRange(Math.Abs(next.Y - position.Y), 0, 4); position = next;
            }
            Assert.NotEqual(start, position);
            Assert.Equal(position, motion.Step(position, layout.Size, work, scale, 120));
        }
    }

    [AvaloniaTheory]
    [InlineData(.06)] [InlineData(.10)] [InlineData(.18)] [InlineData(.22)]
    public async Task ShortPressKeepsTheCanvasFixed(double seconds)
    {
        using var scope = new Scope(); await scope.Select(original: true);
        scope.Pet.BeginCompanionPress(); scope.Pet.AdvanceCompanion(seconds);
        Assert.Equal(PetPose.Neutral, scope.Pet.PetView.Pose);
        Assert.Equal(Matrix.Identity, scope.Pet.PetView.RenderTransform?.Value ?? Matrix.Identity);
    }

    [AvaloniaFact]
    public async Task PointerDownAndReleaseKeepCanvasFixedAndDragDoesNotClick()
    {
        using var scope = new Scope(); await scope.Select(original: true); var pet = scope.Pet;
        var point = pet.PetView.TranslatePoint(new(96, 85), pet)!.Value;
        Assert.True(pet.AcceptsPointerAt(point));
        pet.MouseDown(point, MouseButton.Left); pet.AdvanceCompanion(.1);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.MouseUp(point, MouseButton.Left); pet.AdvanceCompanion(.12);
        Assert.Equal("click", pet.ActiveAnimation); Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        await Until(() => pet.ActiveAnimation == "idle");
        pet.MouseDown(point, MouseButton.Left); pet.MouseMove(point + new Vector(20, 0));
        pet.MouseUp(point + new Vector(20, 0), MouseButton.Left);
        pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22);
        Assert.Equal("idle", pet.ActiveAnimation); Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
    }

    [AvaloniaFact]
    public async Task HoldingAndReleasingKeepCanvasFixedWithoutClicking()
    {
        using var scope = new Scope();
        // This test advances pose time explicitly. ShowPet starts the real timer
        // even in diagnostic mode, so stop it before MouseUp drains UI jobs.
        await scope.Runtime.Start(true, true);
        await scope.Select(original: true); var pet = scope.Pet;
        ((DispatcherTimer)typeof(PetWindow).GetField("hitTimer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pet)!).Stop();
        var point = pet.PetView.TranslatePoint(new(96, 85), pet)!.Value;
        Assert.True(pet.AcceptsPointerAt(point));
        pet.MouseDown(point, MouseButton.Left);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.MouseMove(point + new Vector(25, 0));
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.AdvanceCompanion(.2);
        var held = pet.PetView.Pose;
        pet.MouseUp(point, MouseButton.Left);
        Assert.Equal(held, pet.PetView.Pose);
        Assert.Equal("idle", pet.ActiveAnimation);
        pet.AdvanceCompanion(.18);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData("default-cat")]
    [InlineData("bori-rabbit")]
    [InlineData("puppy-dog")]
    [InlineData("hedgehog")]
    [InlineData("penguin")]
    public async Task OriginalPetsSeparateQuickClicksFromHoldingAndLanding(string id)
    {
        using var scope = new Scope(); await scope.SelectBuiltIn(id); var pet = scope.Pet;
        // This test advances pose time explicitly. ShowPet starts the real pose
        // timer even in diagnostic mode; MouseUp must not drain an extra tick.
        ((DispatcherTimer)typeof(PetWindow).GetField("hitTimer",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(pet)!).Stop();
        var local = Enumerable.Range(40, 120).SelectMany(y => Enumerable.Range(40, 120).Select(x => new Point(x, y)))
            .First(point => pet.PetView.OpaqueAt(point, includeEdgeTolerance: false));
        Point PointerPoint() => pet.PetView.TranslatePoint(local, pet)!.Value;
        pet.MouseMove(PointerPoint()); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();
        // Diagnostic windows start off-screen; place the headless window inside
        // its virtual screen so pointer-up clamping is not part of this assertion.
        pet.Position = new(0, 0);
        var size = pet.ClientSize; var position = pet.Position;
        pet.MouseDown(PointerPoint(), MouseButton.Left);
        Assert.Equal("idle", pet.ActiveAnimation);
        pet.MouseUp(PointerPoint(), MouseButton.Left);
        await Until(() => pet.ActiveAnimation == "click");
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        await Until(() => pet.ActiveAnimation == "idle");

        pet.MouseDown(PointerPoint(), MouseButton.Left);
        pet.AdvanceCompanion(PetPose.LiftDelay);
        await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        var held = pet.PetView.Pose;
        Assert.Equal(PetPose.Neutral, held);
        for (var i = 0; i < 50; i++) pet.AdvanceCompanion(.2);
        Assert.Equal(held, pet.PetView.Pose);
        Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
        pet.MouseUp(PointerPoint(), MouseButton.Left);
        Assert.Equal(PetPointerPhase.Recovering, pet.PointerPhase);
        Assert.Equal("land", pet.ActiveAnimation);
        for (var i = 0; i < 65; i++)
        {
            pet.AdvanceCompanion(.01);
            Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
            Assert.Equal(Matrix.Identity, pet.PetView.RenderTransform?.Value ?? Matrix.Identity);
        }
        await Until(() => pet.PointerPhase == PetPointerPhase.None);
        Assert.Equal("idle", pet.ActiveAnimation);
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        Assert.Equal(size, pet.ClientSize); Assert.Equal(position, pet.Position);
    }

    [AvaloniaFact]
    public async Task QuickRepeatedClicksDoNotEnterPickupOrLanding()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        // Drive the repeated short-click state directly: headless MouseDown renders
        // before returning and can exceed the 220 ms hold threshold on a busy host.
        // Routed input and hold/release timing are covered by the neighboring tests.
        for (var i = 0; i < 5; i++)
        {
            pet.BeginCompanionPress();
            Assert.Equal(PetPointerPhase.Pending, pet.PointerPhase);
            Assert.Equal("idle", pet.ActiveAnimation);
            Assert.True(pet.ReleaseCompanionPress(clicked: true));
            await Until(() => pet.ActiveAnimation == "click");
            Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
            pet.AdvanceCompanion(.15);
            Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        }
        await Until(() => pet.ActiveAnimation == "idle");
    }

    [AvaloniaFact]
    public async Task ReleaseBeforeHoldThresholdOnlyClicksAndThresholdStartsPickup()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        pet.BeginCompanionPress(); pet.AdvanceCompanion(PetPose.LiftDelay - .001);
        Assert.Equal(PetPointerPhase.Pending, pet.PointerPhase);
        Assert.Equal("idle", pet.ActiveAnimation);
        Assert.True(pet.ReleaseCompanionPress(true));
        await Until(() => pet.ActiveAnimation == "click");
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        await Until(() => pet.ActiveAnimation == "idle");
        pet.BeginCompanionPress(); pet.AdvanceCompanion(PetPose.LiftDelay);
        Assert.Equal(PetPointerPhase.Pickup, pet.PointerPhase);
        Assert.Equal("pickup", pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData("capture")] [InlineData("hide")] [InlineData("change")]
    public async Task CancellingBeforeTheHoldThresholdDoesNotStartPickupOrClick(string reason)
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        IPointer? pointer = null;
        pet.PetView.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
        var point = pet.PetView.TranslatePoint(new(96, 85), pet)!.Value;
        pet.MouseDown(point, MouseButton.Left);
        Assert.Equal(PetPointerPhase.Pending, pet.PointerPhase);
        if (reason == "capture") { Assert.NotNull(pointer); pointer.Capture(null); }
        else if (reason == "hide") await scope.Runtime.HidePet();
        else await scope.Select(original: false);
        pet.AdvanceCompanion(.25);
        pet.MouseUp(point, MouseButton.Left);
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        // A newly selected custom pet may enter its own hover clip under the
        // stationary pointer. The cancelled press must not trigger any press/release clip.
        Assert.DoesNotContain(pet.ActiveAnimation, new[] { "click", "pickup", "held", "land", "pointerDown", "pointerUp" });
        if (reason == "change") await Until(() => pet.ActiveAnimation == "idle");
        Assert.Equal("idle", pet.ActiveAnimation);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
    }

    [AvaloniaFact]
    public async Task HoldReleaseStartsAssignedLandImmediatelyWithoutMovingCanvasOrClicking()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        pet.BeginCompanionPress(); pet.AdvanceCompanion(PetPose.LiftDelay);
        Assert.Equal("pickup", pet.ActiveAnimation);
        Assert.True(pet.ReleaseCompanionPress(true));
        Assert.Equal("land", pet.ActiveAnimation);
        Assert.Equal(PetPointerPhase.Recovering, pet.PointerPhase);
        for (var i = 0; i < 65; i++)
        {
            pet.AdvanceCompanion(.01);
            Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
            Assert.Equal(Matrix.Identity, pet.PetView.RenderTransform?.Value ?? Matrix.Identity);
        }
        await Until(() => pet.PointerPhase == PetPointerPhase.None);
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaFact]
    public async Task FastDragStartsPickupImmediatelyAndHidingCancelsLanding()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        Point Point() => pet.PetView.TranslatePoint(new(96, 85), pet)!.Value;
        pet.MouseDown(Point(), MouseButton.Left);
        Assert.Equal(PetPointerPhase.Pending, pet.PointerPhase);
        pet.MouseMove(Point() + new Vector(30, 0));
        Assert.Equal(PetPointerPhase.Pickup, pet.PointerPhase);
        await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        Assert.Equal(PetPointerPhase.Held, pet.PointerPhase);
        pet.MouseUp(Point(), MouseButton.Left);
        pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22);
        await Until(() => pet.PointerPhase == PetPointerPhase.None);
        Assert.Equal("idle", pet.ActiveAnimation);
        pet.MouseDown(Point(), MouseButton.Left); pet.AdvanceCompanion(PetPose.LiftDelay);
        pet.MouseUp(Point(), MouseButton.Left);
        Assert.Equal(PetPointerPhase.Recovering, pet.PointerPhase);
        await scope.Runtime.HidePet();
        pet.AdvanceCompanion(.22); await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(PetPointerPhase.None, pet.PointerPhase);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = true });
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaFact]
    public async Task RepressingDuringLandingRetainsDeferredStretchWithoutClicking()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        pet.BeginCompanionPress(); await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        pet.ReleaseCompanionPress(true); pet.AdvanceCompanion(.2);
        pet.BeginCompanionPress(); await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        pet.ReleaseCompanionPress(false);
        pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22);
        await Until(() => pet.ActiveAnimation == "stretch");
        await Until(() => pet.ActiveAnimation == "walk");
        Assert.True(pet.IsRoaming);
    }

    [AvaloniaFact]
    public async Task ReminderDoesNotReplaceHeldArtAndCancelledReminderDoesNotReplayAfterLanding()
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: true); var pet = scope.Pet;
        pet.BeginCompanionPress(); await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        await scope.Runtime.ShowReminder();
        Assert.Equal(PetNotice.Invitation, scope.Runtime.Reminder.Notice);
        Assert.Equal("held", pet.ActiveAnimation);
        scope.Runtime.StartBreak();
        Assert.Equal("held", pet.ActiveAnimation);
        scope.Runtime.Stop(); pet.ReleaseCompanionPress(false);
        pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22);
        await Until(() => pet.PointerPhase == PetPointerPhase.None);
        Assert.Equal("idle", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task CaptureLossHideAndPetChangeCancelHeldPoseAndDoNotClickOnLaterRelease(bool pointerArt)
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: pointerArt); var pet = scope.Pet;
        IPointer? pointer = null;
        pet.PetView.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
        Point Point() => pet.PetView.TranslatePoint(new(96, 85), pet)!.Value;
        pet.MouseDown(Point(), MouseButton.Left); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.NotNull(pointer); pointer.Capture(null);
        pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.MouseUp(Point(), MouseButton.Left); Assert.Equal("idle", pet.ActiveAnimation);

        pet.MouseDown(Point(), MouseButton.Left); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        await scope.Runtime.HidePet();
        Assert.Null(pointer.Captured); Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings with { ShowPet = true });
        pet.MouseUp(Point(), MouseButton.Left); Assert.Equal("idle", pet.ActiveAnimation);

        pet.MouseDown(Point(), MouseButton.Left); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        await scope.Select(original: false);
        Assert.Null(pointer.Captured); Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.MouseUp(Point(), MouseButton.Left); Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaFact]
    public async Task PressingAgainAfterReleaseKeepsTheCanvasFixed()
    {
        using var scope = new Scope(); await scope.Select(original: true); var pet = scope.Pet;
        pet.BeginCompanionPress(); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        pet.ReleaseCompanionPress(false); pet.AdvanceCompanion(.12);
        var landingPose = pet.PetView.Pose;
        pet.BeginCompanionPress();
        Assert.Equal(landingPose, pet.PetView.Pose);
        pet.AdvanceCompanion(.001);
        Assert.InRange(Math.Abs(pet.PetView.Pose.ScaleY - landingPose.ScaleY), 0, .001);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.ReleaseCompanionPress(true); pet.AdvanceCompanion(.22); pet.AdvanceCompanion(.22);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
    }

    [AvaloniaFact]
    public async Task ThirdSnoozeSulkAndStretchThenWalkStopSafelyOnCompletionAndCancel()
    {
        using var scope = new Scope(); await scope.Select(original: true); var runtime = scope.Runtime; var pet = scope.Pet;
        for (var index = 1; index <= 4; index++)
        {
            await runtime.ShowReminder(); runtime.SnoozeBreak();
            Assert.Equal(index == 3 ? "sulk" : "attention", pet.ActiveAnimation);
            await Until(() => pet.ActiveAnimation == "idle");
        }
        await runtime.ShowReminder(); runtime.StartBreak();
        Assert.Equal("stretch", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
        Assert.Equal(0, runtime.Reminder.ConsecutiveSnoozes);
        await Until(() => pet.IsRoaming && pet.ActiveAnimation == "walk");
        var saved = (runtime.Settings.PetX, runtime.Settings.PetY);
        pet.AdvanceCompanion(.1); Assert.Equal(saved, (runtime.Settings.PetX, runtime.Settings.PetY));
        runtime.CompleteBreak(); Assert.False(pet.IsRoaming);
        await Until(() => pet.ActiveAnimation == "idle");
        await runtime.ShowReminder(); runtime.StartBreak(); runtime.Stop();
        await Until(() => pet.ActiveAnimation == "idle"); Assert.False(pet.IsRoaming);
        await Task.Delay(650, TestContext.Current.CancellationToken);
        Assert.Equal("idle", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
    }

    [AvaloniaFact]
    public async Task DragDuringStretchResumesStretchBeforeWalking()
    {
        using var scope = new Scope(); await scope.Select(original: true); var pet = scope.Pet;
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        Assert.Equal("stretch", pet.ActiveAnimation);
        pet.BeginCompanionPress();
        await Until(() => pet.ActiveAnimation == "idle");
        Assert.False(pet.IsRoaming);
        pet.ReleaseCompanionPress(false);
        Assert.Equal("stretch", pet.ActiveAnimation); Assert.False(pet.IsRoaming);
        await Until(() => pet.IsRoaming && pet.ActiveAnimation == "walk");
    }

    [AvaloniaFact]
    public async Task HidingAndChangingPetCancelPendingBehaviorsAndCustomPetsKeepFiveActions()
    {
        using var scope = new Scope(); await scope.Select(original: true); var pet = scope.Pet;
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await scope.Runtime.HidePet(); Assert.False(pet.IsVisible); Assert.False(pet.IsRoaming);
        await scope.Select(original: false);
        Assert.False(pet.HasOriginalBehavior);
        pet.BeginCompanionPress(); pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        pet.ReleaseCompanionPress(true); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        Assert.Equal(new[] { "idle", "attention", "stretch", "celebrate", "click", "hover", "pointerDown", "pointerUp" }, CustomPetDraft.Actions);
        scope.Runtime.Stop(); await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await Until(() => pet.ActiveAnimation == "idle"); Assert.False(pet.IsRoaming);
        Assert.Equal("idle", pet.ActiveAnimation);
        for (var i = 0; i < 1000; i++) pet.AdvanceCompanion(.1);
        Assert.Equal("idle", pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task PointerInteractionsKeepCanvasAtIdentity(bool pointerArt)
    {
        using var scope = new Scope(); await scope.Select(original: true, pointerArt: pointerArt);
        var pet = scope.Pet;
        void FixedCanvas()
        {
            Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
            Assert.Equal(Matrix.Identity, pet.PetView.RenderTransform?.Value ?? Matrix.Identity);
        }
        FixedCanvas();
        pet.BeginCompanionPress();
        foreach (var seconds in new[] { .1, .12, .2, .2 }) { pet.AdvanceCompanion(seconds); FixedCanvas(); }
        if (pointerArt) await Until(() => pet.PointerPhase == PetPointerPhase.Held);
        FixedCanvas();
        pet.ReleaseCompanionPress(false); FixedCanvas();
        if (pointerArt) { Assert.Equal("land", pet.ActiveAnimation); Assert.Equal(PetPointerPhase.Recovering, pet.PointerPhase); }
        for (var i = 0; i < 65; i++) { pet.AdvanceCompanion(.01); FixedCanvas(); }
        await Until(() => pet.ActiveAnimation == "idle"); FixedCanvas();
    }

    private static async Task Until(Func<bool> ready)
    {
        for (var i = 0; i < 150; i++)
        {
            Dispatcher.UIThread.RunJobs(); if (ready()) return;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        Assert.True(ready(), "Companion did not reach the expected playback state.");
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
        public async Task Select(bool original, bool pointerArt = false)
        {
            var document = new PixelDocument(4, 4) { Name = "Interaction fixture" };
            Array.Fill(document.Layers[0].Frames[0], 0xFFFFFFFFu);
            var package = Runtime.Library.Save(document);
            var keys = original ? OriginalCompanion.RequiredClips.Concat(pointerArt ? OriginalCompanion.PointerClips : []) : CustomPetDraft.Actions;
            var manifest = package.Manifest with
            {
                BehaviorProfile = original ? OriginalCompanion.Profile : null,
                Animations = keys.ToDictionary(key => key, key =>
                    new AnimationDefinition(Enumerable.Repeat(0, key == "stretch" ? 12 : 2).ToArray(), 20, Loop: key is "idle" or "walk" or "held"))
            };
            AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false });
            Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public async Task SelectBuiltIn(string id)
        {
            await Runtime.Start(true, true); Runtime.Stop();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = id, ShowPet = true, ReminderSoundsEnabled = false });
            Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
