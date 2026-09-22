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
        Assert.Equal(OriginalCompanion.RequiredClips.Order(), report.Clips.Select(clip => clip.Key).Order());
        Assert.True(report.Clips.Sum(clip => clip.DecodedBytes) < 32 * 1024 * 1024);
        Assert.All(report.Clips, clip =>
        {
            Assert.Equal(256, clip.Width); Assert.Equal(256, clip.Height);
            Assert.Equal(clip.Frames, clip.TransparentFrames); Assert.Equal(0, clip.EmptyFrames);
            Assert.InRange(clip.DurationMs, 500, 10000);
        });
        foreach (var key in OriginalCompanion.RequiredClips)
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

    [AvaloniaFact]
    public async Task PointerDownSquashesReleaseBouncesAndDragDoesNotReact()
    {
        using var scope = new Scope(); await scope.Select(original: true); var pet = scope.Pet;
        var point = new Point(96, 85);
        pet.MouseDown(point, MouseButton.Left); pet.AdvanceCompanion(.1);
        Assert.True(pet.PetView.Pose.ScaleY < 1);
        pet.MouseUp(point, MouseButton.Left); pet.AdvanceCompanion(.12);
        Assert.Equal("click", pet.ActiveAnimation); Assert.True(pet.PetView.Pose.Lift > 0);
        pet.AdvanceCompanion(.2); pet.AdvanceCompanion(.2);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        await Until(() => pet.ActiveAnimation == "idle");
        pet.MouseDown(point, MouseButton.Left); pet.MouseMove(point + new Vector(20, 0));
        pet.MouseUp(point + new Vector(20, 0), MouseButton.Left);
        Assert.Equal("idle", pet.ActiveAnimation); Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
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
        pet.BeginCompanionPress(); pet.AdvanceCompanion(.1);
        Assert.Equal(PetPose.Neutral, pet.PetView.Pose);
        Assert.Equal(new[] { "idle", "attention", "stretch", "celebrate", "click" }, CustomPetDraft.Actions);
        scope.Runtime.Stop(); await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await Until(() => pet.ActiveAnimation == "idle"); Assert.False(pet.IsRoaming);
        Assert.Equal("idle", pet.ActiveAnimation);
        for (var i = 0; i < 1000; i++) pet.AdvanceCompanion(.1);
        Assert.Equal("idle", pet.ActiveAnimation);
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
        public async Task Select(bool original)
        {
            var document = new PixelDocument(4, 4) { Name = "Interaction fixture" };
            Array.Fill(document.Layers[0].Frames[0], 0xFFFFFFFFu);
            var package = Runtime.Library.Save(document);
            var keys = original ? OriginalCompanion.RequiredClips : CustomPetDraft.Actions;
            var manifest = package.Manifest with
            {
                BehaviorProfile = original ? OriginalCompanion.Profile : null,
                Animations = keys.ToDictionary(key => key, key =>
                    new AnimationDefinition(Enumerable.Repeat(0, key == "stretch" ? 12 : 2).ToArray(), 20, Loop: key is "idle" or "walk"))
            };
            AtomicFile.Write(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false });
            Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
