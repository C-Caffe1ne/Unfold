using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
}
