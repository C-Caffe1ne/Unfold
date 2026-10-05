using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Unfold.Desktop;

/// <summary>A short text transition that never changes bubble or pet layout.</summary>
internal sealed class AnimatedTimeText : TextBlock
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(180);
    private readonly DispatcherTimer frames = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch elapsed = new();
    private readonly TranslateTransform offset = new();
    internal bool HasMotion => frames.IsEnabled;
    internal bool AnimationsEnabled { get; set; } = true;

    public AnimatedTimeText()
    {
        RenderTransform = offset;
        frames.Tick += (_, _) =>
        {
            if (!IsEffectivelyVisible || elapsed.Elapsed >= Duration) { StopMotion(); return; }
            var progress = elapsed.Elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
            var remaining = Math.Pow(1 - progress, 3);
            offset.Y = 3 * remaining; Opacity = 1 - .45 * remaining;
        };
    }

    internal void UpdateValue(string value, bool animate = true)
    {
        if (Text == value)
        {
            if (!animate || !AnimationsEnabled) StopMotion();
            return;
        }
        var transition = animate && AnimationsEnabled && !string.IsNullOrEmpty(Text) &&
            IsEffectivelyVisible && VisualRoot is not null;
        StopMotion(); Text = value;
        if (!transition) return;
        offset.Y = 3; Opacity = .55; elapsed.Restart(); frames.Start();
    }

    internal void StopMotion()
    {
        frames.Stop(); elapsed.Reset(); offset.Y = 0; Opacity = 1;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        StopMotion(); base.OnDetachedFromVisualTree(args);
    }
}
