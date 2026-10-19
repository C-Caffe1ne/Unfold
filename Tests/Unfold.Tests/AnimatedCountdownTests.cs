using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public sealed class AnimatedCountdownTests
{
    [AvaloniaFact]
    public void MinuteBorrowAnimatesChangedDigitsAndPauseCancelsTheTransition()
    {
        using var countdown = new AnimatedCountdown { FontSize = 64, LineHeight = 74 };
        var window = new Window { Content = countdown, Width = 400, Height = 180 };
        window.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            countdown.UpdateTime(TimeSpan.FromMinutes(1), true);
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            Assert.Equal("00:59", countdown.Text);
            Assert.Equal(new[] { 1, 3, 4 }, countdown.AnimatedDigitIndices.Order().ToArray());
            Assert.True(countdown.HasDigitMotion);
            // A repeated runtime refresh must not restart or interrupt a digit transition.
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            Assert.True(countdown.HasDigitMotion);
            countdown.UpdateTime(TimeSpan.FromSeconds(59), false);
            Assert.False(countdown.HasDigitMotion); Assert.Empty(countdown.AnimatedDigitIndices);
            Assert.Equal(.55, countdown.Opacity); Assert.Equal("00:59", countdown.Text);
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            Assert.False(countdown.HasDigitMotion); Assert.Equal(1, countdown.Opacity);
            countdown.UpdateTime(TimeSpan.FromSeconds(58), true);
            Assert.Equal(new[] { 4 }, countdown.AnimatedDigitIndices.ToArray());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OsReduceMotionShowsChangedDigitsWithoutTheRoll()
    {
        var reduced = true;
        using var countdown = new AnimatedCountdown { FontSize = 64, LineHeight = 74, ReduceMotion = () => reduced };
        var window = new Window { Content = countdown, Width = 400, Height = 180 };
        window.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            countdown.UpdateTime(TimeSpan.FromMinutes(1), true);
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            Assert.Equal("00:59", countdown.Text);
            Assert.False(countdown.HasDigitMotion); Assert.Empty(countdown.AnimatedDigitIndices);
            reduced = false; // The preference is read on each change, not cached per control.
            countdown.UpdateTime(TimeSpan.FromSeconds(58), true);
            Assert.True(countdown.HasDigitMotion);
        }
        finally { window.Close(); }
    }

    [Fact]
    public void HostReduceMotionQueryCompletesWithoutThrowing()
    {
        // Exercises the real native signatures on each CI OS; the value is host-specific.
        _ = MotionPreference.QueryOs();
        _ = MotionPreference.QueryOs(); // Cached path.
    }

    [AvaloniaFact]
    public void ResetLongIntervalsAndHiddenControlsDoNotRollThroughIntermediateTimes()
    {
        using var countdown = new AnimatedCountdown { FontSize = 64, LineHeight = 74 };
        var window = new Window { Content = countdown, Width = 500, Height = 180 };
        window.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            countdown.UpdateTime(TimeSpan.FromMinutes(60), true);
            window.UpdateLayout(); var twoDigitWidth = countdown.DesiredSize.Width;
            countdown.UpdateTime(TimeSpan.FromMinutes(240), false);
            window.UpdateLayout();
            Assert.Equal("240:00", countdown.Text);
            Assert.True(countdown.DesiredSize.Width > twoDigitWidth);
            Assert.True(countdown.HasDigitMotion); Assert.Equal(1, countdown.MotionDirection);
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            countdown.UpdateTime(TimeSpan.FromSeconds(58), true);
            Assert.True(countdown.HasDigitMotion);
            window.Content = null;
            Assert.False(countdown.HasDigitMotion);
            countdown.UpdateTime(TimeSpan.FromSeconds(57), true);
            Assert.Equal("00:57", countdown.Text); Assert.False(countdown.HasDigitMotion);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DisabledMotionAndDisposalStopTheFrameTimer()
    {
        using var countdown = new AnimatedCountdown { FontSize = 64, LineHeight = 74 };
        var window = new Window { Content = countdown, Width = 400, Height = 180 };
        window.Show(); Dispatcher.UIThread.RunJobs();
        try
        {
            countdown.UpdateTime(TimeSpan.FromSeconds(59), true);
            countdown.UpdateTime(TimeSpan.FromSeconds(58), true);
            Assert.True(countdown.HasDigitMotion);
            countdown.AnimationsEnabled = false;
            countdown.UpdateTime(TimeSpan.FromSeconds(57), true);
            Assert.False(countdown.HasDigitMotion); Assert.Equal("00:57", countdown.Text);
            countdown.AnimationsEnabled = true;
            countdown.UpdateTime(TimeSpan.FromSeconds(56), true);
            Assert.True(countdown.HasDigitMotion);
            countdown.Dispose(); Assert.False(countdown.HasDigitMotion);
            countdown.UpdateTime(TimeSpan.FromSeconds(55), true);
            Assert.Equal("00:56", countdown.Text);
        }
        finally { window.Close(); }
    }
}
