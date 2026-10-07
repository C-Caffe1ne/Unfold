using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AnimationMemoryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(AnimationView view, string name) =>
        (T)typeof(AnimationView).GetField(name, Private)!.GetValue(view)!;
    private static Bitmap[] Bitmaps(AnimationView view) => Field<Bitmap[]>(view, "bitmaps");
    private static void Advance(AnimationView view) => typeof(AnimationView).GetMethod("Advance", Private)!.Invoke(view, null);
    private static void Complete(AnimationView view)
    {
        // Move the existing one-shot boundary before the observed elapsed time;
        // exercise Advance's terminal path without a long wall-clock wait.
        typeof(AnimationView).GetField("totalMs", Private)!.SetValue(view, double.Epsilon);
        Advance(view);
    }
    private static uint[] Pixels(Bitmap bitmap)
    {
        var pixels = new uint[bitmap.PixelSize.Width * bitmap.PixelSize.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), handle.AddrOfPinnedObject(), pixels.Length * 4, bitmap.PixelSize.Width * 4); }
        finally { handle.Free(); }
        return pixels;
    }
    private static AnimationFrame Frame(uint color, TimeSpan? duration = null) =>
        new(new PixelImage(2, 2, Enumerable.Repeat(color, 4).ToArray()), duration ?? TimeSpan.FromSeconds(1));

    [AvaloniaFact]
    public void NativeBitmapCountDoesNotGrowWithDecodedFrameCount()
    {
        using var view = new AnimationView();
        var clip = Enumerable.Range(0, 512).Select(i => Frame(0xFF000000u | (uint)i)).ToArray();
        view.SetFrames(clip, true);
        Assert.Equal(clip[0].Image.Pixels, Pixels(Assert.Single(Bitmaps(view))));
        Assert.Equal(512, Field<IReadOnlyList<AnimationFrame>>(view, "frames").Count);
        view.SetFrames([], false);
        Assert.Empty(Bitmaps(view));
    }

    [AvaloniaFact]
    public async Task ReusedBitmapShowsChangedTransparencyAndOneShotLastFrame()
    {
        using var view = new AnimationView { Width = 64, Height = 64 };
        var window = new Window { Width = 64, Height = 64, Content = view };
        var clip = new[]
        {
            Frame(0xFFFF0000u, TimeSpan.FromMilliseconds(20)),
            new AnimationFrame(new PixelImage(2, 2, [0u, 0xFF00FF00u, 0xFF00FF00u, 0u]), TimeSpan.FromDays(1)),
            Frame(0xFF0000FFu)
        };
        var sourcePixels = clip.Select(frame => frame.Image.Pixels.ToArray()).ToArray();
        var completed = 0; view.Completed += () => completed++;
        try
        {
            window.Show(); window.UpdateLayout(); view.SetFrames(clip, false);
            var bitmap = Assert.Single(Bitmaps(view));
            Field<DispatcherTimer>(view, "timer").Stop();
            var elapsed = Field<Stopwatch>(view, "elapsed");
            while (elapsed.Elapsed.TotalMilliseconds < 25)
                await Task.Delay(5, TestContext.Current.CancellationToken);
            Advance(view);
            Assert.Equal(1, Field<int>(view, "index"));
            Assert.Same(bitmap, Assert.Single(Bitmaps(view)));
            Assert.Equal(clip[1].Image.Pixels, Pixels(bitmap));
            Assert.False(view.OpaqueAt(new Point(8, 8), includeEdgeTolerance: false));
            Assert.True(view.OpaqueAt(new Point(48, 8), includeEdgeTolerance: false));
            Complete(view);
            Assert.Same(bitmap, Assert.Single(Bitmaps(view)));
            Assert.Equal(clip[^1].Image.Pixels, Pixels(bitmap));
            Assert.Equal(1, completed);
            Advance(view); view.SetRunning(false); view.SetRunning(true);
            Assert.Equal(1, completed);
            Assert.False(Field<DispatcherTimer>(view, "timer").IsEnabled);
            for (var i = 0; i < clip.Length; i++) Assert.Equal(sourcePixels[i], clip[i].Image.Pixels);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PingPongKeepsFrameOrderAndUsesOneBitmapForTheEndingPose()
    {
        using var view = new AnimationView();
        var clip = new[] { Frame(0xFFFF0000u), Frame(0xFF00FF00u), Frame(0xFF0000FFu) };
        view.SetFrames(clip, false, pingPong: true);
        var expanded = Field<IReadOnlyList<AnimationFrame>>(view, "frames");
        Assert.Equal(new[] { clip[0], clip[1], clip[2], clip[1] }, expanded);
        var bitmap = Assert.Single(Bitmaps(view));
        Complete(view);
        Assert.Same(bitmap, Assert.Single(Bitmaps(view)));
        Assert.Equal(clip[1].Image.Pixels, Pixels(bitmap));
    }

    [AvaloniaFact]
    public void ViewsOwnTheirBitmapsAndReplaceOnlyForChangedFrameGeometry()
    {
        using var first = new AnimationView(); using var second = new AnimationView();
        var clip = new[]
        {
            Frame(0xFFFF0000u),
            new AnimationFrame(new PixelImage(4, 2, Enumerable.Repeat(0xFF0000FFu, 8).ToArray()), TimeSpan.FromSeconds(1))
        };
        first.SetFrames(clip, false); second.SetFrames(clip, false);
        var old = Assert.Single(Bitmaps(first)); var other = Assert.Single(Bitmaps(second));
        Assert.NotSame(old, other);
        Complete(first);
        var resized = Assert.Single(Bitmaps(first));
        Assert.NotSame(old, resized); Assert.Equal(new PixelSize(4, 2), resized.PixelSize);
        Assert.Equal(clip[1].Image.Pixels, Pixels(resized));
        Assert.Same(other, Assert.Single(Bitmaps(second)));
        Assert.Equal(clip[0].Image.Pixels, Pixels(other));
        first.SetFrames([], false); Assert.Empty(Bitmaps(first));
        Assert.Equal(clip[0].Image.Pixels, Pixels(other));
        first.Dispose(); first.SetFrames(clip, false); Assert.Empty(Bitmaps(first));
    }
}
