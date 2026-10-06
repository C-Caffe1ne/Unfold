using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Images decoded/uploaded once per clip. A timer runs only while attached.</summary>
public sealed class AnimationView : Control, IDisposable
{
    private IReadOnlyList<AnimationFrame> frames = [];
    private Bitmap[] bitmaps = [];
    private GlbAnimationFrames? live;
    private PixelImage? liveImage;
    private PixelImage? liveBackImage;
    private bool renderingLive;
    private int liveGeneration;
    private int livePixelSize = 192;
    private TopLevel? renderTopLevel;
    internal int RenderedPixelSize => liveImage?.Width ?? 0;
    internal int TargetPixelSize => livePixelSize;
    public event Action<Exception>? PlaybackFailed;
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
        pose = value; facingLeft = live is null && mirror;
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
        SizeChanged += (_, _) => { UpdatePoseTransform(); RefreshLiveResolution(); };
        LayoutUpdated += (_, _) => RefreshLiveResolution();
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        timer.Tick += (_, _) => Advance();
        AttachedToVisualTree += (_, _) =>
        {
            renderTopLevel = TopLevel.GetTopLevel(this);
            if (renderTopLevel is not null) renderTopLevel.ScalingChanged += OnScalingChanged;
            RefreshLiveResolution();
            if (running && NeedsTimer) { elapsed.Start(); timer.Start(); }
        };
        DetachedFromVisualTree += (_, _) => { DetachTopLevel(); liveGeneration++; liveBackImage = null; elapsed.Stop(); timer.Stop(); };
    }
    public void SetFrames(IReadOnlyList<AnimationFrame> clip, bool repeat, bool pixel = true, bool alignCompanion = false, bool pingPong = false)
    {
        if (disposed) return;
        timer.Stop(); liveGeneration++; foreach (var bitmap in bitmaps) bitmap.Dispose();
        live = clip as GlbAnimationFrames; liveImage = liveBackImage = null;
        if (live is not null)
        {
            frames = clip;
            var first = live[0].Image;
            // Playback owns both buffers; never recycle pixels returned by the
            // public immutable frame API or shared with another view.
            liveImage = new(first.Width, first.Height, first.Pixels.ToArray());
            bitmaps = [Ui.Bitmap(liveImage)]; loop = repeat;
            frameOffsets = []; totalMs = live.DurationSeconds * 1000; index = 0; completed = false; facingLeft = false;
            RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
            timer.Interval = live.FrameDuration;
            if (running) { elapsed.Restart(); if (HasTopLevel() && NeedsTimer) timer.Start(); } else elapsed.Reset();
            UpdatePoseTransform(); RefreshLiveResolution(); InvalidateVisual(); return;
        }
        frames = pingPong && clip.Count > 2 ? clip.Concat(clip.Skip(1).Take(clip.Count - 2).Reverse()).ToArray() : clip; bitmaps = frames.Select(f => Ui.Bitmap(f.Image)).ToArray(); loop = repeat;
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
        else { liveGeneration++; liveBackImage = null; elapsed.Stop(); timer.Stop(); }
    }
    private void Advance()
    {
        if (live is not null) { _ = AdvanceLive(); return; }
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
    private async Task AdvanceLive(bool refreshOnly = false)
    {
        if (renderingLive || live is not { } source || !HasTopLevel() || (!refreshOnly && (completed || !running))) return;
        var ms = elapsed.Elapsed.TotalMilliseconds; var ended = !refreshOnly && !loop && ms >= totalMs;
        var next = refreshOnly ? index : ended ? source.Count - 1 : Math.Min(source.Count - 1, (int)((ms % totalMs) / source.FrameDuration.TotalMilliseconds));
        if (next == index && !ended && RenderedPixelSize == livePixelSize) return;
        var request = liveGeneration; var pixels = livePixelSize; renderingLive = true; var failed = false;
        try
        {
            var image = liveBackImage is { } back && back.Width == pixels
                ? back : new PixelImage(pixels, pixels, new uint[pixels * pixels]);
            liveBackImage = null;
            // A completed one-shot keeps the exact end pose when resized or
            // refreshed; indexed frames continue to exclude the loop boundary.
            var terminal = ended || (!loop && completed);
            await Task.Run(() =>
            {
                if (terminal) source.RenderTerminalFrameInto(pixels, image.Pixels);
                else source.RenderFrameInto(next, pixels, image.Pixels);
            });
            if (disposed || request != liveGeneration) return;
            if (bitmaps.Length == 1 && bitmaps[0] is WriteableBitmap bitmap && bitmap.PixelSize.Width == pixels && bitmap.PixelSize.Height == pixels)
                Ui.WriteBitmap(bitmap, image);
            else
            {
                var replacement = Ui.Bitmap(image);
                foreach (var old in bitmaps) old.Dispose();
                bitmaps = [replacement];
            }
            liveBackImage = liveImage?.Width == pixels ? liveImage : null;
            liveImage = image; index = next; InvalidateVisual();
            if (ended) { timer.Stop(); elapsed.Stop(); completed = true; Completed?.Invoke(); }
        }
        catch (Exception error)
        {
            failed = true;
            if (!disposed && request == liveGeneration)
            { timer.Stop(); elapsed.Stop(); completed = true; AppPaths.Log(error); PlaybackFailed?.Invoke(error); }
        }
        finally
        {
            renderingLive = false;
            // A resize/clip swap during rendering must also refresh a paused or
            // completed view, without advancing time or firing completion again.
            if (!failed && !disposed && live is not null && HasTopLevel() && RenderedPixelSize != livePixelSize)
                _ = AdvanceLive(refreshOnly: true);
        }
    }
    internal static int PixelSizeFor(double logicalSize, double renderScaling)
    {
        var pixels = logicalSize * renderScaling;
        if (!double.IsFinite(pixels) || pixels <= 0) return 192;
        return (int)Math.Clamp(Math.Ceiling(pixels / 32) * 32, 64, GlbAnimationFrames.MaxFrameSize);
    }
    private void RefreshLiveResolution()
    {
        if (disposed || live is null || !HasTopLevel()) return;
        var top = TopLevel.GetTopLevel(this)!;
        // Include Viewbox/ancestor scaling; ignore the pet's own transient squash
        // so a click does not allocate a different resolution on every pose tick.
        var matrix = this.GetVisualParent()?.TransformToVisual(top) ?? Matrix.Identity;
        var sx = Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
        var sy = Math.Sqrt(matrix.M21 * matrix.M21 + matrix.M22 * matrix.M22);
        var size = PixelSizeFor(Math.Min(Bounds.Width * sx, Bounds.Height * sy), top.RenderScaling);
        if (size != livePixelSize) { livePixelSize = size; liveGeneration++; }
        if (RenderedPixelSize != livePixelSize) _ = AdvanceLive(refreshOnly: true);
    }
    private void OnScalingChanged(object? sender, EventArgs e) => RefreshLiveResolution();
    private void DetachTopLevel()
    {
        if (renderTopLevel is not null) renderTopLevel.ScalingChanged -= OnScalingChanged;
        renderTopLevel = null;
    }
    private void UpdatePoseTransform()
    {
        // Keep recorded frame geometry stable when changing the static pose or
        // walking direction; pointer events never animate this transform.
        var sx = pose.ScaleX * (facingLeft ? -1 : 1);
        var anchorX = Bounds.Width / 2;
        var anchorY = Bounds.Height * .90;
        RenderTransform = new MatrixTransform(new Matrix(sx, 0, 0, pose.ScaleY,
            anchorX * (1 - sx), anchorY * (1 - pose.ScaleY) - pose.Lift * Bounds.Height));
    }
    private Rect ImageRect()
    {
        if (frames.Count == 0) return default;
        var image = liveImage ?? frames[index].Image; var scale = Math.Min(Bounds.Width / image.Width, Bounds.Height / image.Height);
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
    public bool OpaqueAt(Point point) => OpaqueAt(point, includeEdgeTolerance: true);
    public bool OpaqueAt(Point point, bool includeEdgeTolerance)
    {
        if (frames.Count == 0) return false;
        // Pointer coordinates are already converted into this visual's local space
        // by Avalonia, including its scale, lift and facing direction.
        var rect = ImageRect(); if (!rect.Contains(point) || rect.Width <= 0) return false;
        var image = liveImage ?? frames[index].Image;
        var x = (int)((point.X - rect.X) / rect.Width * image.Width); var y = (int)((point.Y - rect.Y) / rect.Height * image.Height);
        var radius = includeEdgeTolerance ? Math.Max(1, (int)Math.Ceiling(2 * image.Width / rect.Width)) : 0;
        for (var py = Math.Max(0, y - radius); py <= Math.Min(image.Height - 1, y + radius); py++)
            for (var px = Math.Max(0, x - radius); px <= Math.Min(image.Width - 1, x + radius); px++)
                if (image.Pixels[py * image.Width + px] >> 24 >= 26) return true;
        return false;
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (bitmaps.Length > 0) context.DrawImage(bitmaps[live is null ? index : 0], ImageRect());
    }
    public void Dispose() { DetachTopLevel(); disposed = true; liveGeneration++; live = null; liveImage = liveBackImage = null; running = false; timer.Stop(); foreach (var bitmap in bitmaps) bitmap.Dispose(); bitmaps = []; frames = []; }
}
