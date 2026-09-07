using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Images decoded/uploaded once per clip. A timer runs only while attached.</summary>
public sealed class AnimationView : Control, IDisposable
{
    private IReadOnlyList<AnimationFrame> frames = [];
    private Bitmap[] bitmaps = [];
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch elapsed = new();
    private double totalMs;
    private bool loop;
    private int index;
    public event Action? Completed;
    public AnimationView()
    {
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) => Advance();
        AttachedToVisualTree += (_, _) => { if (frames.Count > 1) { elapsed.Start(); timer.Start(); } };
        DetachedFromVisualTree += (_, _) => { elapsed.Stop(); timer.Stop(); };
    }
    public void SetFrames(IReadOnlyList<AnimationFrame> clip, bool repeat, bool pixel = true)
    {
        timer.Stop(); foreach (var bitmap in bitmaps) bitmap.Dispose();
        frames = clip; bitmaps = frames.Select(f => Ui.Bitmap(f.Image)).ToArray(); loop = repeat;
        totalMs = frames.Sum(f => f.Duration.TotalMilliseconds); index = 0; elapsed.Restart();
        RenderOptions.SetBitmapInterpolationMode(this, pixel ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        timer.Interval = frames.Count > 0 ? frames[0].Duration : TimeSpan.FromMilliseconds(100);
        if (HasTopLevel() && frames.Count > 1) timer.Start();
        InvalidateVisual();
    }
    private bool HasTopLevel() => TopLevel.GetTopLevel(this) is not null;
    public void SetRunning(bool running)
    {
        if (running) { elapsed.Start(); if (frames.Count > 1) timer.Start(); }
        else { elapsed.Stop(); timer.Stop(); }
    }
    private void Advance()
    {
        if (frames.Count == 0 || totalMs <= 0) return;
        var ms = elapsed.Elapsed.TotalMilliseconds;
        if (!loop && ms >= totalMs) { index = frames.Count - 1; timer.Stop(); InvalidateVisual(); Completed?.Invoke(); return; }
        ms %= totalMs; var next = 0;
        while (next < frames.Count - 1 && ms >= frames[next].Duration.TotalMilliseconds) ms -= frames[next++].Duration.TotalMilliseconds;
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(5, frames[next].Duration.TotalMilliseconds - ms));
        if (next != index) { index = next; InvalidateVisual(); }
    }
    private Rect ImageRect()
    {
        if (frames.Count == 0) return default;
        var image = frames[index].Image; var scale = Math.Min(Bounds.Width / image.Width, Bounds.Height / image.Height);
        return new((Bounds.Width - image.Width * scale) / 2, (Bounds.Height - image.Height * scale) / 2, image.Width * scale, image.Height * scale);
    }
    public bool OpaqueAt(Point point)
    {
        if (frames.Count == 0) return false;
        var rect = ImageRect(); if (!rect.Contains(point) || rect.Width <= 0) return false;
        var image = frames[index].Image;
        var x = (int)((point.X - rect.X) / rect.Width * image.Width); var y = (int)((point.Y - rect.Y) / rect.Height * image.Height);
        var radius = Math.Max(1, (int)Math.Ceiling(2 * image.Width / rect.Width));
        for (var py = Math.Max(0, y - radius); py <= Math.Min(image.Height - 1, y + radius); py++)
            for (var px = Math.Max(0, x - radius); px <= Math.Min(image.Width - 1, x + radius); px++)
                if (image.Pixels[py * image.Width + px] >> 24 >= 26) return true;
        return false;
    }
    public override void Render(DrawingContext context) { base.Render(context); if (bitmaps.Length > 0) context.DrawImage(bitmaps[index], ImageRect()); }
    public void Dispose() { timer.Stop(); foreach (var bitmap in bitmaps) bitmap.Dispose(); bitmaps = []; frames = []; }
}
