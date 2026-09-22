using Avalonia;

namespace Unfold.Desktop;

internal readonly record struct PetPose(double ScaleX, double ScaleY, double Lift)
{
    public static PetPose Neutral => new(1, 1, 0);
    public static PetPose Press(double seconds) => Between(Neutral, new(1.12, .82, 0), Math.Clamp(seconds / .10, 0, 1));
    public static PetPose Release(double seconds, PetPose start)
    {
        if (seconds < .12) return Between(start, new(.94, 1.06, .07), seconds / .12);
        if (seconds < .26) return Between(new(.94, 1.06, .07), new(1.06, .94, 0), (seconds - .12) / .14);
        return Between(new(1.06, .94, 0), Neutral, Math.Clamp((seconds - .26) / .18, 0, 1));
    }
    private static PetPose Between(PetPose from, PetPose to, double t)
    {
        t = Math.Clamp(t, 0, 1); t = t * t * (3 - 2 * t);
        return new(from.ScaleX + (to.ScaleX - from.ScaleX) * t,
            from.ScaleY + (to.ScaleY - from.ScaleY) * t, from.Lift + (to.Lift - from.Lift) * t);
    }
}

/// <summary>Physical-pixel movement bounded by the whole pet-and-bubble window.</summary>
internal sealed class PetWanderMotion(Random random)
{
    private Point? exact, target;
    private PixelPoint? previous;
    public bool FacingLeft { get; private set; }
    public void Reset() { exact = target = null; previous = null; FacingLeft = false; }
    public PixelPoint Step(PixelPoint position, Size windowSize, PixelRect work, double scale, double seconds)
    {
        var maxX = Math.Max(work.X, work.Right - (int)Math.Ceiling(windowSize.Width * scale));
        var maxY = Math.Max(work.Y, work.Bottom - (int)Math.Ceiling(windowSize.Height * scale));
        var clamped = new PixelPoint(Math.Clamp(position.X, work.X, maxX), Math.Clamp(position.Y, work.Y, maxY));
        if (!double.IsFinite(seconds) || seconds <= 0 || seconds > .25) return clamped;
        if (previous != position || exact is null) exact = new Point(clamped.X, clamped.Y);
        if (target is { } oldTarget) target = new Point(Math.Clamp(oldTarget.X, work.X, maxX), Math.Clamp(oldTarget.Y, work.Y, maxY));
        if (target is null || ((Vector)(target.Value - exact.Value)).Length < 2)
            target = new Point(work.X + random.NextDouble() * (maxX - work.X), work.Y + random.NextDouble() * (maxY - work.Y));
        var delta = (Vector)(target.Value - exact.Value);
        if (Math.Abs(delta.X) > 1) FacingLeft = delta.X < 0;
        if (delta.Length > 0) exact += delta / delta.Length * Math.Min(delta.Length, seconds * 28 * scale);
        var next = new PixelPoint(Math.Clamp((int)Math.Round(exact.Value.X), work.X, maxX),
            Math.Clamp((int)Math.Round(exact.Value.Y), work.Y, maxY));
        previous = next; return next;
    }
}
