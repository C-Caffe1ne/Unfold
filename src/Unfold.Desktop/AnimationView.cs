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
    private Point[] frameOffsets = [];
    private PetPose pose = PetPose.Neutral;
    private bool facingLeft;
    internal PetPose Pose => pose;
    internal void SetPose(PetPose value, bool mirror = false)
    {
        if (pose == value && facingLeft == mirror) return;
        pose = value; facingLeft = mirror;
        UpdatePoseTransform();
    }
    // Whether playback is allowed to run: false while hidden (SetRunning(false))
    // or after Dispose(). A clip swap while paused must not override this, or a
    // stale async continuation (React/SetCharacter resolving after hide/close)
    // would resurrect the timer on a view nobody can see.
    private bool running = true;
    private bool disposed;
    private bool completed;
    public event Action? Completed;
    internal bool Repeats => loop;
    private bool NeedsTimer => frames.Count > 0 && !completed && (!loop || frames.Count > 1);
    public AnimationView()
    {
        RenderTransformOrigin = RelativePoint.TopLeft;
        SizeChanged += (_, _) => UpdatePoseTransform();
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) => Advance();
        AttachedToVisualTree += (_, _) => { if (running && NeedsTimer) { elapsed.Start(); timer.Start(); } };
        DetachedFromVisualTree += (_, _) => { elapsed.Stop(); timer.Stop(); };
    }
    public void SetFrames(IReadOnlyList<AnimationFrame> clip, bool repeat, bool pixel = true, bool alignCompanion = false)
    {
        if (disposed) return;
        timer.Stop(); foreach (var bitmap in bitmaps) bitmap.Dispose();
        frames = clip; bitmaps = frames.Select(f => Ui.Bitmap(f.Image)).ToArray(); loop = repeat;
        frameOffsets = alignCompanion ? frames.Select(frame => CompanionOffset(frame.Image)).ToArray() : [];
        totalMs = frames.Sum(f => f.Duration.TotalMilliseconds); index = 0; completed = false;
        RenderOptions.SetBitmapInterpolationMode(this, pixel ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        timer.Interval = frames.Count > 0 ? frames[0].Duration : TimeSpan.FromMilliseconds(100);
        if (running) { elapsed.Restart(); if (HasTopLevel() && NeedsTimer) timer.Start(); } else elapsed.Reset();
        InvalidateVisual();
    }
    private bool HasTopLevel() => TopLevel.GetTopLevel(this) is not null;
    public void SetRunning(bool value)
    {
        if (disposed) return;
        running = value;
        if (running && NeedsTimer && HasTopLevel()) { elapsed.Start(); timer.Start(); }
        else { elapsed.Stop(); timer.Stop(); }
    }
    private void Advance()
    {
        if (frames.Count == 0 || totalMs <= 0) return;
        var ms = elapsed.Elapsed.TotalMilliseconds;
        if (!loop && ms >= totalMs)
        {
            index = frames.Count - 1; timer.Stop(); elapsed.Stop(); InvalidateVisual();
            if (!completed) { completed = true; Completed?.Invoke(); }
            return;
        }
        ms %= totalMs; var next = 0;
        while (next < frames.Count - 1 && ms >= frames[next].Duration.TotalMilliseconds) ms -= frames[next++].Duration.TotalMilliseconds;
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(5, frames[next].Duration.TotalMilliseconds - ms));
        if (next != index) { index = next; InvalidateVisual(); }
    }
    private void UpdatePoseTransform()
    {
        // Keep the recorded image geometry fixed. Let the compositor move the visual
        // and invalidate its old and new extents, including during a press/release.
        var sx = pose.ScaleX * (facingLeft ? -1 : 1);
        var anchorX = Bounds.Width / 2;
        var anchorY = Bounds.Height * .90;
        RenderTransform = new MatrixTransform(new Matrix(sx, 0, 0, pose.ScaleY,
            anchorX * (1 - sx), anchorY * (1 - pose.ScaleY) - pose.Lift * Bounds.Height));
    }
    private Rect ImageRect()
    {
        if (frames.Count == 0) return default;
        var image = frames[index].Image; var scale = Math.Min(Bounds.Width / image.Width, Bounds.Height / image.Height);
        var offset = frameOffsets.Length > index ? frameOffsets[index] : default;
        var x = (Bounds.Width - image.Width * scale) / 2 + offset.X * scale;
        var y = (Bounds.Height - image.Height * scale) / 2 + offset.Y * scale;
        return new(x, y, image.Width * scale, image.Height * scale);
    }
    // Stabilize generated poses at rendering time; preserve atlas pixels and custom GIF geometry.
    private static Point CompanionOffset(PixelImage image)
    {
        var left = image.Width; var right = -1; var bottom = -1;
        for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                if (image.Pixels[y * image.Width + x] >> 24 >= 26)
                { left = Math.Min(left, x); right = Math.Max(right, x); bottom = y; }
        return bottom < 0 ? default : new Point((image.Width - left - right - 1) / 2d, image.Height * .90 - bottom - 1);
    }
    public bool OpaqueAt(Point point)
    {
        if (frames.Count == 0) return false;
        // Pointer coordinates are already converted into this visual's local space
        // by Avalonia, including its scale, lift and facing direction.
        var rect = ImageRect(); if (!rect.Contains(point) || rect.Width <= 0) return false;
        var image = frames[index].Image;
        var x = (int)((point.X - rect.X) / rect.Width * image.Width); var y = (int)((point.Y - rect.Y) / rect.Height * image.Height);
        var radius = Math.Max(1, (int)Math.Ceiling(2 * image.Width / rect.Width));
        for (var py = Math.Max(0, y - radius); py <= Math.Min(image.Height - 1, y + radius); py++)
            for (var px = Math.Max(0, x - radius); px <= Math.Min(image.Width - 1, x + radius); px++)
                if (image.Pixels[py * image.Width + px] >> 24 >= 26) return true;
        return false;
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (bitmaps.Length > 0) context.DrawImage(bitmaps[index], ImageRect());
    }
    public void Dispose() { disposed = true; running = false; timer.Stop(); foreach (var bitmap in bitmaps) bitmap.Dispose(); bitmaps = []; frames = []; }
}
