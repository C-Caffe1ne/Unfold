using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class ReminderRecoveryTests
{
    [AvaloniaFact]
    public async Task BrokenIdleStillDeliversOneInvitationAndSupportsSnoozeStartAndCompletion()
    {
        using var scope = new Scope(); await scope.Load(); var runtime = scope.Runtime;
        await runtime.ShowReminder(); Layout(scope.Pet);
        var first = Assert.IsType<BreakSession>(runtime.Reminder.Session);
        Assert.Equal(PetNotice.Invitation, runtime.Reminder.Notice);
        Assert.True(scope.Pet.IsVisible); Assert.True(scope.Pet.PetView.OpaqueAt(new(96, 96)));
        Assert.Contains("휴식 대기 중", runtime.TrayStatus.Status);
        Assert.Equal(1, runtime.DueSoundRequests); Assert.False(runtime.Settings.ShowPet);
        var settings = AppSettings.Load(scope.SettingsPath);
        await runtime.ShowReminder();
        Assert.Same(first, runtime.Reminder.Session); Assert.Equal(1, runtime.DueSoundRequests);

        Click(scope.Pet, "PetBreakSnooze");
        Assert.Equal(BreakSessionState.Snoozed, first.State); Assert.Null(runtime.Reminder.Session);
        Assert.False(scope.Pet.IsVisible); Assert.Empty(runtime.BreakHistory.Completions);
        Assert.Equal(TimeSpan.FromMinutes(runtime.Settings.SnoozeMinutes), runtime.Clock.Remaining);

        await runtime.ShowReminder(); Layout(scope.Pet);
        Assert.NotSame(first, runtime.Reminder.Session); Assert.Equal(2, runtime.DueSoundRequests);
        Click(scope.Pet, "PetBreakStart"); Layout(scope.Pet);
        Assert.Equal(PetNotice.Resting, runtime.Reminder.Notice);
        Assert.True(scope.Pet.PetView.OpaqueAt(new(96, 96)));
        Click(scope.Pet, "PetBreakComplete");
        Assert.Equal(PetNotice.Completed, runtime.Reminder.Notice);
        Assert.Single(runtime.BreakHistory.Completions); Assert.Equal(1, runtime.CompletionSoundRequests);
        Assert.Equal(settings, AppSettings.Load(scope.SettingsPath));
    }

    [AvaloniaFact]
    public async Task RepairedIdleIsRetriedForTheNextInvitationWithoutChangingTheSelection()
    {
        using var scope = new Scope(); await scope.Load();
        await scope.Runtime.ShowReminder(); Layout(scope.Pet);
        Assert.Equal(1, scope.FrameCount);
        scope.Runtime.SnoozeBreak(); scope.Repair();
        await scope.Runtime.ShowReminder(); Layout(scope.Pet);
        Assert.Equal(4, scope.FrameCount);
        Assert.Equal(scope.CharacterId, scope.Runtime.Settings.SelectedCharacterId);
        Assert.Equal(scope.CharacterId, scope.Runtime.Reminder.Session!.CharacterId);
    }

    [AvaloniaFact]
    public async Task LateIdleFailureDoesNotReplaceAnAlreadyRecoveredPet()
    {
        using var scope = new Scope(); await scope.Load();
        var pending = scope.DeferIdle(); var opening = scope.Runtime.ShowReminder();
        scope.Repair(); scope.ForgetIdle();
        await scope.Runtime.FocusReminder(); Layout(scope.Pet);
        Assert.Equal(4, scope.FrameCount);
        pending.SetException(new IOException("An older read failed.")); await opening;
        Assert.Equal(4, scope.FrameCount);
        Assert.True(scope.Pet.IsVisible); Assert.Equal(1, scope.Runtime.DueSoundRequests);
    }

    [AvaloniaFact]
    public async Task AReactionReturningToBrokenIdleKeepsTheNoticeAndFallbackImage()
    {
        using var scope = new Scope(); await scope.Load(withClick: true);
        await scope.Runtime.ShowReminder(); Layout(scope.Pet);
        await scope.Pet.React("click"); Layout(scope.Pet);
        Assert.Equal(PetNotice.Invitation, scope.Runtime.Reminder.Notice);
        Assert.Equal("idle", scope.Pet.ActiveAnimation); Assert.Equal(1, scope.FrameCount);
        Assert.True(scope.Pet.PetView.OpaqueAt(new(96, 96)));
        Assert.Equal(1, scope.Runtime.DueSoundRequests);
        Click(scope.Pet, "PetBreakStart"); Layout(scope.Pet);
        Click(scope.Pet, "PetBreakComplete");
        Assert.Single(scope.Runtime.BreakHistory.Completions);
    }

    [AvaloniaTheory]
    [InlineData("stop")] [InlineData("hide")] [InlineData("dispose")]
    public async Task ADelayedReadFailureCannotResurrectACancelledOrHiddenNotice(string action)
    {
        using var scope = new Scope(); await scope.Load();
        var pending = scope.DeferIdle();
        var opening = scope.Runtime.ShowReminder();
        var session = Assert.IsType<BreakSession>(scope.Runtime.Reminder.Session);
        await scope.Runtime.ShowReminder();
        Assert.Same(session, scope.Runtime.Reminder.Session);
        Assert.Equal(1, scope.Runtime.DueSoundRequests);
        var pet = scope.Pet;
        switch (action)
        {
            case "stop": scope.Runtime.Stop(); break;
            case "hide": await scope.Runtime.HidePet(); break;
            case "dispose": scope.Runtime.Dispose(); break;
        }
        var sounds = scope.Runtime.DueSoundRequests;
        pending.SetException(new IOException("Deferred idle read failed.")); await opening;
        Assert.False(pet.IsVisible); Assert.Equal(sounds, scope.Runtime.DueSoundRequests);
        Assert.Empty(scope.Runtime.BreakHistory.Completions);
        if (action == "stop") Assert.False(scope.Runtime.Reminder.HasNotice);
        if (action == "hide")
        {
            Assert.Same(session, scope.Runtime.Reminder.Session);
            await scope.Runtime.FocusReminder(); Layout(scope.Pet);
            Assert.True(scope.Pet.IsVisible); Assert.Equal(sounds, scope.Runtime.DueSoundRequests);
        }
    }

    [AvaloniaFact]
    public async Task DisposedRuntimeRejectsLaterReminderRequests()
    {
        using var scope = new Scope(); await scope.Load();
        scope.Repair(); scope.Runtime.Dispose(); await scope.Runtime.ShowReminder();
        Assert.Null(scope.Runtime.ActivePet); Assert.Null(scope.Runtime.Reminder.Session);
        Assert.Equal(0, scope.Runtime.DueSoundRequests);
    }

    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Window window, string name) => window.GetVisualDescendants().OfType<Button>()
        .Single(button => button.Name == name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private string gifPath = "";
        public string CharacterId { get; private set; } = "";
        public string SettingsPath => Path.Combine(temp.Path, "settings.json");
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public int FrameCount => ((IReadOnlyList<AnimationFrame>)typeof(AnimationView)
            .GetField("frames", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Pet.PetView)!).Count;
        private Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> Cache =>
            (Dictionary<string, Task<IReadOnlyList<AnimationFrame>>>)typeof(AppRuntime)
                .GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Runtime)!;
        private string IdleKey => $"{Runtime.Selected!.DirectoryPath}:idle";
        public Scope() { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path); Runtime = new(lifetime); }
        public async Task Load(bool withClick = false)
        {
            var document = new PixelDocument(4, 4) { Name = "Reminder recovery fixture" };
            Array.Fill(document.Layers[0].Frames[0], 0xFFFFFFFFu);
            var package = Runtime.Library.Save(document); CharacterId = package.Manifest.Id;
            gifPath = Path.Combine(package.DirectoryPath, "idle.gif"); File.WriteAllText(gifPath, "damaged");
            var manifest = package.Manifest with { Animations = new() { ["idle"] = new(Gif: "idle.gif") } };
            if (withClick)
            {
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"), Path.Combine(package.DirectoryPath, "click.gif"));
                manifest.Animations["click"] = new(Gif: "click.gif", Loop: false);
            }
            File.WriteAllBytes(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = CharacterId, ShowPet = false,
                ReminderSoundsEnabled = true, ReminderVolumePercent = 0 });
            Runtime.Clock.Start(TimeSpan.Zero);
        }
        public void Repair() => File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"), gifPath, true);
        public void ForgetIdle() => Cache.Remove(IdleKey);
        public TaskCompletionSource<IReadOnlyList<AnimationFrame>> DeferIdle()
        {
            var pending = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Cache[IdleKey] = pending.Task;
            return pending;
        }
        public void Dispose() { Runtime.Dispose(); lifetime.Dispose(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose(); }
    }
}
