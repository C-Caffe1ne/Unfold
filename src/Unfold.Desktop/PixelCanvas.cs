using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Unfold.Core;
using PixelPoint = Unfold.Core.PixelPoint;

namespace Unfold.Desktop;

public sealed class PixelCanvas : Control, IDisposable
{
    public EditorSession Session { get; }
    private int zoom = 8;
    public int Zoom { get => zoom; set { zoom = Math.Clamp(value, 1, 24); Refresh(); } }
    public bool Grid { get; set; } = true;
    public bool OnionSkin { get; set; }
    private WriteableBitmap? bitmap, onion;
    private uint[]? uploaded, uploadedOnion;
    public PixelCanvas(EditorSession session)
    {
        Session = session; Focusable = true; Cursor = new Cursor(StandardCursorType.Cross);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        Session.Changed += Refresh; Refresh();
    }
    public void Refresh()
    {
        var doc = Session.Document;
        Width = doc.Width * zoom; Height = doc.Height * zoom;
        var pixels = Session.Composite(Session.Frame);
        if (!ReferenceEquals(uploaded, pixels)) { Upload(ref bitmap, pixels, doc.Width, doc.Height); uploaded = pixels; }
        if (OnionSkin && Session.Frame > 0)
        {
            var previous = Session.Composite(Session.Frame - 1);
            if (!ReferenceEquals(uploadedOnion, previous)) { Upload(ref onion, previous, doc.Width, doc.Height); uploadedOnion = previous; }
        }
        InvalidateVisual();
    }
    private static unsafe void Upload(ref WriteableBitmap? image, uint[] pixels, int width, int height)
    {
        if (image is null || image.PixelSize.Width != width || image.PixelSize.Height != height)
        { image?.Dispose(); image = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul); }
        using var locked = image.Lock();
        for (var y = 0; y < height; y++) pixels.AsSpan(y * width, width).CopyTo(new Span<uint>((byte*)locked.Address + y * locked.RowBytes, width));
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var doc = Session.Document; var dark = Brush.Parse("#353B47"); var light = Brush.Parse("#454D5A");
        var rect = new Rect(Bounds.Size);
        using (context.PushClip(rect))
        {
            context.FillRectangle(dark, rect);
            for (var y = 0; y < doc.Height; y += 4) for (var x = 0; x < doc.Width; x += 4)
                if ((x / 4 + y / 4) % 2 == 0) context.FillRectangle(light, new Rect(x * zoom, y * zoom, 4 * zoom, 4 * zoom));
            if (OnionSkin && Session.Frame > 0 && onion is not null)
                using (context.PushOpacity(0.25)) context.DrawImage(onion, rect);
            if (bitmap is not null) context.DrawImage(bitmap, rect);
            if (Grid && zoom >= 6)
            {
                var pen = new Pen(Brush.Parse("#33000000"), 1);
                for (var x = 0; x <= doc.Width; x++) context.DrawLine(pen, new Point(x * zoom, 0), new Point(x * zoom, Height));
                for (var y = 0; y <= doc.Height; y++) context.DrawLine(pen, new Point(0, y * zoom), new Point(Width, y * zoom));
            }
        }
    }
    private PixelPoint Pixel(PointerEventArgs e) { var p = e.GetPosition(this); return new((int)Math.Floor(p.X / zoom), (int)Math.Floor(p.Y / zoom)); }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); e.Pointer.Capture(this); Session.BeginStroke(Pixel(e)); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (e.Pointer.Captured == this) Session.ContinueStroke(Pixel(e)); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); Session.EndStroke(); e.Pointer.Capture(null); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); Session.EndStroke(); }
    public void Dispose() { Session.EndStroke(); Session.Changed -= Refresh; bitmap?.Dispose(); onion?.Dispose(); }
}
