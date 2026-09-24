using Avalonia;

namespace Unfold.Desktop;

internal readonly record struct PetPose(double ScaleX, double ScaleY, double Lift)
{
    public const double LiftDelay = .22;
    public const double BounceDuration = .64;
    public static PetPose Neutral => new(1, 1, 0);
    public static PetPose Press(double seconds) => Between(Neutral, new(1.12, .82, 0), Math.Clamp(seconds / .10, 0, 1));
    // A quick click keeps its original squash. Continuing to hold lifts the pet
    // inside the existing canvas; no native window geometry changes are needed.
    public static PetPose Hold(double seconds, PetPose start)
    {
        var squashed = Press(.10);
        if (seconds <= .10) return Between(start, squashed, seconds / .10);
        return Between(squashed, new(.96, 1.02, .06), (seconds - LiftDelay) / .18);
    }
    public static PetPose Land(double seconds, PetPose start)
    {
        var contact = new PetPose(1.10, .90, 0);
        var rebound = new PetPose(.98, 1.02, .012);
        if (seconds < .16) return Between(start, contact, seconds / .16);
        if (seconds < .28) return Between(contact, rebound, (seconds - .16) / .12);
        return Between(rebound, Neutral, (seconds - .28) / .16);
    }
    public static PetPose Pickup(double seconds, PetPose start) => Between(start, new(1, 1, .06), seconds / .28);
    public static PetPose BounceOnce(double seconds, PetPose start)
    {
        var firstContact = new PetPose(1.10, .88, 0);
        var apex = new PetPose(.98, 1.02, .10);
        var finalContact = new PetPose(1.05, .94, 0);
        if (seconds < .16) return Between(start, firstContact, seconds / .16);
        if (seconds < .34) return Between(firstContact, apex, (seconds - .16) / .18);
        if (seconds < .52) return Between(apex, finalContact, (seconds - .34) / .18);
        return Between(finalContact, Neutral, (seconds - .52) / .12);
    }
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
