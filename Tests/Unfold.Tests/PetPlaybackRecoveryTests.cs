using System.Reflection;
using System.Text.Json;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class PetPlaybackRecoveryTests
{
    [AvaloniaFact]
    public async Task RepairedGifRetriesAndAFailedReactionReleasesRandomIdleBehavior()
    {
        using var scope = new Scope(); await scope.Select(); var pet = scope.Pet;
        scope.Damage("click");
        var failed = scope.Runtime.Clip("click");
        await Assert.ThrowsAsync<InvalidDataException>(() => failed);
        await pet.React("click");
        Assert.Equal("idle", pet.ActiveAnimation);
        // This must exercise the eligibility gate, not just the animation label.
        for (var i = 0; i < 200 && pet.ActiveAnimation == "idle"; i++) pet.AdvanceCompanion(.25);
        Assert.Contains(pet.ActiveAnimation, new[] { "sleep", "look", "yawn" });
        await Until(() => pet.ActiveAnimation == "idle");

        scope.Repair("click");
        var retry = scope.Runtime.Clip("click"); Assert.NotSame(failed, retry);
        Assert.Equal(4, (await retry).Count);
        Assert.Same(retry, scope.Runtime.Clip("click"));
        var reaction = pet.React("click"); Assert.Equal("click", pet.ActiveAnimation);
        await reaction;
        Assert.Equal("idle", pet.ActiveAnimation); Assert.True(pet.PetView.Repeats);
    }

    [AvaloniaTheory]
    [InlineData(false)] [InlineData(true)]
    public async Task InFlightLoadsAreSharedButFailedOrCancelledLoadsCanBeRetried(bool cancelled)
    {
        using var scope = new Scope(); await scope.Select();
        var pending = scope.Defer("click");
        Assert.Same(pending.Task, scope.Runtime.Clip("click"));
        Assert.Same(pending.Task, scope.Runtime.Clip("click"));
        if (cancelled) pending.SetCanceled(); else pending.SetException(new IOException("Read failed."));
        var retry = scope.Runtime.Clip("click");
        Assert.NotSame(pending.Task, retry); Assert.Equal(4, (await retry).Count);
        Assert.Same(retry, scope.Runtime.Clip("click"));
    }

    [AvaloniaTheory]
    [InlineData("stretch")] [InlineData("walk")]
    public async Task BrokenBreakMotionFallsBackWithoutCancellingTheBreakOrLooping(string key)
    {
        using var scope = new Scope(); await scope.Select(); scope.Damage(key);
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await Until(() => scope.Pet.ActiveAnimation == "idle");
        Assert.Equal(PetNotice.Resting, scope.Runtime.Reminder.Notice);
        Assert.False(scope.Pet.IsRoaming); Assert.True(scope.Pet.PetView.Repeats);
        scope.Repair(key); scope.Runtime.Stop();
        await scope.Runtime.ShowReminder(); scope.Runtime.StartBreak();
        await Until(() => scope.Pet.ActiveAnimation == "walk" && scope.Pet.IsRoaming);
    }

    [AvaloniaFact]
    public async Task EvenABrokenIdleHasABoundedFallbackAndCanRecoverOnTheNextReaction()
    {
        using var scope = new Scope(); await scope.Select();
        scope.Damage("click"); scope.Damage("idle"); scope.Cache.Remove(scope.Key("idle"));
        var reaction = scope.Pet.React("click"); await Until(() => reaction.IsCompleted); await reaction;
        Assert.Equal("idle", scope.Pet.ActiveAnimation);
        Assert.False(scope.Pet.PetView.OpaqueAt(new(96, 96)));
        scope.Repair("click"); scope.Repair("idle");
        await scope.Pet.React("click");
        Assert.Equal("idle", scope.Pet.ActiveAnimation); Assert.True(scope.Pet.PetView.Repeats);
        Assert.False(scope.Runtime.Clip("idle").IsFaulted);
    }

    [AvaloniaFact]
    public async Task LateFailureDoesNotReplaceANewerReactionOrItsCacheEntry()
    {
        using var scope = new Scope(); await scope.Select();
        var first = scope.Defer("click"); var older = scope.Pet.React("click");
        var second = scope.Defer("click"); var newer = scope.Pet.React("click");
        first.SetException(new IOException("Old decode failed.")); await older;
        Assert.Equal("click", scope.Pet.ActiveAnimation);
        Assert.Same(second.Task, scope.Runtime.Clip("click"));
        second.SetResult(scope.Runtime.Selected!.LoadAnimation("click")); await newer;
        Assert.Equal("idle", scope.Pet.ActiveAnimation);
    }

    [AvaloniaFact]
    public async Task LateIdleFallbackDoesNotReplaceANewerReaction()
    {
        using var scope = new Scope(); await scope.Select(); scope.Damage("click");
        var idle = scope.Defer("idle"); var older = scope.Pet.React("click");
        await Until(() => scope.Cache.TryGetValue(scope.Key("click"), out var load) && load.IsFaulted &&
            !(bool)typeof(PetWindow).GetField("reacting", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scope.Pet)!);
        var look = scope.Defer("look"); var newer = scope.Pet.React("look");
        idle.SetResult(scope.Runtime.Selected!.LoadAnimation("idle")); await older;
        Assert.Equal("look", scope.Pet.ActiveAnimation);
        look.SetResult(scope.Runtime.Selected!.LoadAnimation("look")); await newer;
        Assert.Equal("idle", scope.Pet.ActiveAnimation);
    }

    [AvaloniaTheory]
    [InlineData("hide")] [InlineData("close")] [InlineData("change")]
    public async Task LateFailureCannotResurrectHiddenClosedOrReplacedPets(string action)
    {
        using var scope = new Scope(); await scope.Select(); var pet = scope.Pet;
        var pending = scope.Defer("click"); var reaction = pet.React("click");
        switch (action)
        {
            case "hide": await scope.Runtime.HidePet(); break;
            case "close": scope.Runtime.Dispose(); break;
            case "change": await scope.Select(); break;
        }
        var selected = scope.Runtime.Settings.SelectedCharacterId;
        var animation = pet.ActiveAnimation; var visible = pet.IsVisible;
        pending.SetException(new IOException("Old pet load failed.")); await reaction;
        Assert.Equal(selected, scope.Runtime.Settings.SelectedCharacterId);
        Assert.Equal(animation, pet.ActiveAnimation); Assert.Equal(visible, pet.IsVisible);
        Assert.Equal(action == "change", pet.IsVisible);
    }

    private static async Task Until(Func<bool> predicate)
    {
        for (var i = 0; i < 200 && !predicate(); i++)
        { Dispatcher.UIThread.RunJobs(); await Task.Delay(20, TestContext.Current.CancellationToken); }
        Assert.True(predicate(), "Playback recovery did not complete.");
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private static readonly byte[] Gif = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "pet-motion.gif"));
        public AppRuntime Runtime { get; }
        public PetWindow Pet => Runtime.ActivePet!;
        public Dictionary<string, Task<IReadOnlyList<AnimationFrame>>> Cache =>
            (Dictionary<string, Task<IReadOnlyList<AnimationFrame>>>)typeof(AppRuntime)
                .GetField("clips", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Runtime)!;
        public string Key(string key) => $"{Runtime.Selected!.DirectoryPath}:{key}";
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime);
        }
        public async Task Select()
        {
            var document = new PixelDocument(4, 4) { Name = "Recovery fixture" };
            Array.Fill(document.Layers[0].Frames[0], 0xFFFFFFFFu);
            var package = Runtime.Library.Save(document);
            foreach (var key in OriginalCompanion.RequiredClips) File.WriteAllBytes(Path.Combine(package.DirectoryPath, key + ".gif"), Gif);
            var manifest = package.Manifest with { BehaviorProfile = OriginalCompanion.Profile,
                Animations = OriginalCompanion.RequiredClips.ToDictionary(key => key,
                    key => new AnimationDefinition(Gif: key + ".gif", Loop: key is "idle" or "walk")) };
            File.WriteAllBytes(Path.Combine(package.DirectoryPath, "character.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, CharacterLibrary.JsonOptions));
            await Runtime.Reload();
            await Runtime.UpdateSettings(Runtime.Settings with { SelectedCharacterId = package.Manifest.Id, ShowPet = true, ReminderSoundsEnabled = false });
            Dispatcher.UIThread.RunJobs(); Pet.UpdateLayout();
        }
        public void Damage(string key) => File.WriteAllText(Path.Combine(Runtime.Selected!.DirectoryPath, key + ".gif"), "damaged");
        public void Repair(string key) => File.WriteAllBytes(Path.Combine(Runtime.Selected!.DirectoryPath, key + ".gif"), Gif);
        public TaskCompletionSource<IReadOnlyList<AnimationFrame>> Defer(string key)
        {
            var pending = new TaskCompletionSource<IReadOnlyList<AnimationFrame>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Cache[Key(key)] = pending.Task; return pending;
        }
        public void Dispose()
        {
            Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
