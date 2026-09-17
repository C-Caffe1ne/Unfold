using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AnimationLifecycleTests
{
    private static AnimationFrame[] Clip(int count = 2, TimeSpan? duration = null) =>
        Enumerable.Range(0, count)
            .Select(_ => new AnimationFrame(new PixelImage(2, 2, [0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu, 0xFFFFFFFFu]), duration ?? TimeSpan.FromMilliseconds(50)))
            .ToArray();

    private static bool TimerEnabled(AnimationView view) =>
        ((DispatcherTimer)typeof(AnimationView).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!).IsEnabled;

    // PetWindow.PetView and AnimationView.Repeats are internal to Unfold.Desktop; the pack
    // diagnostic reads both directly. The tests reach them the same way this file already
    // reaches the playback timer.
    private static AnimationView PetView(PetWindow pet) =>
        (AnimationView)typeof(PetWindow).GetProperty("PetView", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pet)!;

    private static bool Repeats(AnimationView view) =>
        (bool)typeof(AnimationView).GetProperty("Repeats", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;

    private sealed class AnimationHost(Window window, AnimationView view) : IDisposable
    {
        public void Dispose() { view.Dispose(); window.Close(); }
    }

    private static IDisposable Host(AnimationView view)
    {
        var window = new Window { Width = 64, Height = 64, Content = view };
        window.Show(); Dispatcher.UIThread.RunJobs();
        return new AnimationHost(window, view);
    }

    // Regression for: SetFrames used to call timer.Start() unconditionally, so a
    // React()/SetCharacter() continuation that resolves after HidePet() (SetRunning(false))
    // resurrected playback on a view nobody could see.
    [AvaloniaFact]
    public void PausedViewIgnoresClipSwapAndStaysStopped()
    {
        var view = new AnimationView();
        using var host = Host(view);
        view.SetFrames(Clip(), true);
        Assert.True(TimerEnabled(view));

        view.SetRunning(false);
        Assert.False(TimerEnabled(view));

        // Simulates a pending reaction's clip finishing load after the pet was hidden.
        view.SetFrames(Clip(), true);
        Assert.False(TimerEnabled(view));
    }

    // Regression for: ClosePet() did not invalidate in-flight generations, so a
    // React()/SetCharacter() continuation resolving after Close() could call back into an
    // already-disposed AnimationView and resurrect it. The view itself is now the last line
    // of defense: once disposed, further calls are no-ops regardless of caller-side races.
    [AvaloniaFact]
    public void DisposedViewIgnoresStaleCallbacksAndStaysEmpty()
    {
        var view = new AnimationView();
        using var host = Host(view);
        view.SetFrames(Clip(), true);
        Assert.True(view.OpaqueAt(new Point(32, 32)));

        view.Dispose();
        Assert.False(TimerEnabled(view));
        Assert.False(view.OpaqueAt(new Point(32, 32)));

        // Simulates a stale React()/SetCharacter() callback arriving after ClosePet().
        view.SetFrames(Clip(), true);
        view.SetRunning(true);
        Assert.False(TimerEnabled(view));
        Assert.False(view.OpaqueAt(new Point(32, 32)));
    }

    // Guards the fix against over-suppressing playback: a visible, running view must still
    // animate and fire Completed normally.
    [AvaloniaFact]
    public async Task VisibleRunningAnimationAdvancesAndCompletes()
    {
        var view = new AnimationView();
        using var host = Host(view);
        var completed = false; view.Completed += () => completed = true;
        view.SetFrames(Clip(2, TimeSpan.FromMilliseconds(20)), false);

        for (var i = 0; i < 50 && !completed; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }
        Assert.True(completed);
    }

    [AvaloniaFact]
    public async Task SingleFrameOneShotCompletesOnceAndDoesNotRestartAfterResume()
    {
        var view = new AnimationView(); using var host = Host(view);
        var completed = 0; view.Completed += () => completed++;
        view.SetFrames(Clip(1, TimeSpan.FromMilliseconds(20)), false);
        for (var i = 0; i < 50 && completed == 0; i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken); Dispatcher.UIThread.RunJobs();
        }
        Assert.Equal(1, completed); Assert.False(TimerEnabled(view));
        view.SetRunning(false); view.SetRunning(true); Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, completed); Assert.False(TimerEnabled(view));
        view.SetFrames(Clip(), true); Assert.True(TimerEnabled(view));
    }

    // Regression for: PetPackDiagnostics read the pet's playback mode with
    // `runtime.ActivePet.Content is AnimationView`, but PetWindow.Content became the layout
    // Canvas that also carries the speech bubble and tail. The cast silently produced null,
    // so `--review-pet-pack` threw "The desktop pet did not enter the requested reaction."
    // on its very first clip and exited 1 for every pack. The surface is now published as
    // PetWindow.PetView, and that contract must keep pointing at the view the pet really
    // renders — both for the loop-mode checks and for the per-clip render capture.
    [AvaloniaFact]
    public void PetAnimationSurfaceIsPublishedAsAContractAndTracksTheLiveClipMode()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
        using var lifetime = new ClassicDesktopStyleApplicationLifetime();
        using var runtime = new AppRuntime(lifetime);
        var pet = new PetWindow(runtime);
        try
        {
            pet.Show(); Dispatcher.UIThread.RunJobs(); pet.UpdateLayout();

            var live = pet.GetVisualDescendants().OfType<AnimationView>().Single();
            Assert.Same(live, PetView(pet));
            Assert.IsNotType<AnimationView>(pet.Content);

            // The diagnostic renders this surface per clip, so it has to be laid out at the
            // pet's own size and actually draw the character.
            Assert.Equal(new Size(192, 192), PetView(pet).Bounds.Size);
            live.SetFrames(Clip(), true);
            Assert.True(PetView(pet).OpaqueAt(new Point(96, 96)));

            // A one-shot reaction, then the restored idle loop: the two states the review
            // asserts between captures.
            Assert.True(Repeats(PetView(pet)));
            live.SetFrames(Clip(2, TimeSpan.FromMilliseconds(20)), false);
            Assert.False(Repeats(PetView(pet)));
            live.SetFrames(Clip(), true);
            Assert.True(Repeats(PetView(pet)));
        }
        finally { pet.ClosePet(); Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }
}
