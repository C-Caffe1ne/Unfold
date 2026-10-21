using Avalonia;
using Unfold.Desktop;

namespace Unfold.Tests;

public sealed class PetThrowMotionTests
{
    private static readonly PixelRect Bounds = new(0, 0, 1000, 600); // Window top-left range; Bottom is the floor.

    private static PetThrowMotion Drag(Vector perStep, int steps = 6, double scale = 1)
    {
        var motion = new PetThrowMotion();
        var pointer = new Point(500, 300);
        motion.Begin(new(400, 200), pointer, TimeSpan.Zero);
        for (var i = 1; i <= steps; i++)
        {
            pointer += perStep;
            motion.Track(new(400 + (int)(perStep.X * i), 200 + (int)(perStep.Y * i)), pointer, TimeSpan.FromMilliseconds(16 * i));
        }
        return motion;
    }

    [Fact]
    public void SlowReleaseStaysWhereTheUserPutThePet()
    {
        var motion = Drag(new(4, 0)); // 250 px/s
        Assert.False(motion.Release(TimeSpan.FromMilliseconds(100), 1));
        Assert.Equal(new PixelPoint(424, 200), motion.Target);
    }

    [Fact]
    public void PausingBeforeReleaseDropsInsteadOfThrowing()
    {
        var motion = Drag(new(40, 0));
        Assert.False(motion.Release(TimeSpan.FromMilliseconds(96 + 200), 1));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FastReleaseThrowsWithASoftSpeedCapAndLandsOnTheFloor(double scale)
    {
        var motion = Drag(new Vector(120, -40) * scale);
        Assert.True(motion.Release(TimeSpan.FromMilliseconds(100), scale));
        Assert.InRange(motion.Velocity.Length, 500 * scale, 3600 * scale);
        var position = motion.Target; var landed = false; var bounced = false;
        for (var tick = 0; tick < 2000 && !landed; tick++)
        {
            (position, landed) = motion.Fly(Bounds, .016, scale);
            Assert.InRange(position.X, Bounds.X, Bounds.Right); Assert.InRange(position.Y, Bounds.Y, Bounds.Bottom);
            if (position.X == Bounds.Right) bounced = true;
        }
        Assert.True(landed); Assert.Equal(Bounds.Bottom, position.Y);
        Assert.True(bounced); // 2,700+ px/s to the right reaches the wall first.
        Assert.False(motion.IsFlying);
    }

    [Fact]
    public void LongFrameGapsAreClampedSoThePetCannotTunnelThroughTheFloor()
    {
        var motion = Drag(new(0, 60));
        Assert.True(motion.Release(TimeSpan.FromMilliseconds(100), 1));
        var (position, _) = motion.Fly(Bounds, 5, 1);
        Assert.InRange(position.Y, Bounds.Y, Bounds.Bottom);
    }

    [Fact]
    public void SpringFollowsWithoutOvershootAndSettlesOnTheTarget()
    {
        var motion = new PetThrowMotion();
        motion.Begin(new(0, 0), new(0, 0), TimeSpan.Zero);
        motion.Track(new(100, 0), new(100, 0), TimeSpan.FromMilliseconds(16));
        var position = new PixelPoint();
        for (var tick = 0; tick < 120; tick++)
        {
            position = motion.Follow(.016);
            Assert.InRange(position.X, 0, 100);
        }
        Assert.Equal(new PixelPoint(100, 0), position);
    }
}
