using Avalonia;

namespace Unfold.Desktop;

/// <summary>
/// Drag-follow and throw physics for the pet window in physical desktop pixels.
/// Constants are logical pixels per second and scale with the desktop scaling.
/// </summary>
internal sealed class PetThrowMotion
{
    internal const double SpringK = 200, SpringC = 30; // ζ ≈ 1.06: overdamped, no overshoot past the pointer.
    internal const double Gravity = 1400, Restitution = .78, GroundFriction = 2.5;
    internal const double DeadZone = 500, MaxSpeed = 3600, RestVy = 40, RestVx = 15, MaxStep = .05;
    private static readonly TimeSpan ReleaseWindow = TimeSpan.FromMilliseconds(150), TrailKeep = TimeSpan.FromMilliseconds(200),
        MinSpan = TimeSpan.FromMilliseconds(20);
    private readonly List<(TimeSpan Time, Point Pointer)> trail = [];
    private Point exact;
    private Vector velocity;
    internal PixelPoint Target { get; private set; }
    internal Vector Velocity => velocity;
    internal bool IsFlying { get; private set; }

    internal void Begin(PixelPoint window, Point pointer, TimeSpan now)
    {
        exact = new(window.X, window.Y); velocity = default; Target = window; IsFlying = false;
        trail.Clear(); trail.Add((now, pointer));
    }

    internal void Track(PixelPoint target, Point pointer, TimeSpan now)
    {
        Target = target; trail.Add((now, pointer));
        while (trail.Count > 2 && now - trail[0].Time > TrailKeep) trail.RemoveAt(0);
    }

    internal PixelPoint Follow(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return Round(exact);
        seconds = Math.Min(seconds, MaxStep);
        var offset = new Vector(Target.X - exact.X, Target.Y - exact.Y);
        velocity += (offset * SpringK - velocity * SpringC) * seconds;
        exact += velocity * seconds;
        if (offset.Length < .5 && velocity.Length < 1) { exact = new(Target.X, Target.Y); velocity = default; }
        return Round(exact);
    }

    /// <summary>Starts a throw when the pointer was still moving fast at release; otherwise the pet stays at the target.</summary>
    internal bool Release(TimeSpan now, double scale)
    {
        if (trail.Count < 2 || now - trail[^1].Time > ReleaseWindow) return Settle();
        var first = trail.First(sample => trail[^1].Time - sample.Time <= ReleaseWindow);
        var span = trail[^1].Time - first.Time;
        if (span < MinSpan) return Settle();
        var raw = (Vector)(trail[^1].Pointer - first.Pointer) / span.TotalSeconds;
        var speed = raw.Length / scale;
        if (!double.IsFinite(speed) || speed < DeadZone) return Settle();
        // Soft cap: any force stays distinguishable but never exceeds MaxSpeed.
        var capped = MaxSpeed * (1 - Math.Exp(-speed / MaxSpeed));
        // The flight starts from where the spring has carried the window, not from the pointer.
        velocity = raw / raw.Length * capped * scale;
        IsFlying = true;
        return true;
    }

    /// <summary>Stops at the drag target, as a gentle release does.</summary>
    internal bool Settle()
    {
        exact = new(Target.X, Target.Y); velocity = default; IsFlying = false;
        return false;
    }

    /// <summary>Advances a throw inside <paramref name="bounds"/>, whose Right/Bottom are the largest window X/Y.</summary>
    internal (PixelPoint Position, bool Landed) Fly(PixelRect bounds, double seconds, double scale)
    {
        if (!IsFlying) return (Round(exact), true);
        seconds = double.IsFinite(seconds) ? Math.Clamp(seconds, 0, MaxStep) : 0;
        var vx = velocity.X; var vy = velocity.Y + Gravity * scale * seconds;
        var x = exact.X + vx * seconds; var y = exact.Y + vy * seconds;
        if (x < bounds.X) { x = bounds.X; vx = -vx * Restitution; }
        if (x > bounds.Right) { x = bounds.Right; vx = -vx * Restitution; }
        if (y < bounds.Y) { y = bounds.Y; vy = -vy * Restitution; }
        if (y >= bounds.Bottom)
        {
            y = bounds.Bottom;
            vy = vy > RestVy * scale ? -vy * Restitution : 0;
            if (vy == 0)
            {
                vx *= Math.Exp(-GroundFriction * seconds);
                if (Math.Abs(vx) < RestVx * scale) { vx = 0; IsFlying = false; }
            }
        }
        exact = new(x, y); velocity = new(vx, vy);
        return (Round(exact), !IsFlying);
    }

    internal void Cancel() { IsFlying = false; velocity = default; trail.Clear(); }

    private static PixelPoint Round(Point point) => new((int)Math.Round(point.X), (int)Math.Round(point.Y));
}
