using Avalonia;
using Unfold.Core;
using Unfold.Desktop;
using PixelPoint = Avalonia.PixelPoint;

namespace Unfold.Tests;

public sealed class PetDockPlacementTests
{
    [Theory]
    [InlineData(1920, 1080, 96)]
    [InlineData(1920, 1080, 192)]
    [InlineData(2560, 1440, 288)]
    [InlineData(3008, 1692, 192)]
    [InlineData(3840, 2160, 288)]
    public void MacDockDropHoverAndWalkingKeepThePetAtItsScreenAnchor(int width, int height, double petSize)
    {
        // A 4K panel can expose several logical desktop sizes. macOS positions
        // use DesktopScaling=1 even when Retina rendering uses twice the pixels.
        foreach (var origin in new[] { new PixelPoint(0, 0), new PixelPoint(-width, -100) })
        foreach (var dock in new[] { "bottom", "left", "right", "hidden" })
        {
            var bounds = new PixelRect(origin.X, origin.Y, width, height);
            var work = dock switch
            {
                "bottom" => new PixelRect(origin.X, origin.Y + 25, width, height - 115),
                "left" => new PixelRect(origin.X + 90, origin.Y + 25, width - 90, height - 25),
                "right" => new PixelRect(origin.X, origin.Y + 25, width - 90, height - 25),
                _ => new PixelRect(origin.X, origin.Y + 25, width, height - 29)
            };
            var area = PetWindow.PlacementArea(bounds, work, macOS: true);
            var anchor = new PixelPoint(dock == "left" ? bounds.X : dock == "right" ? bounds.Right - (int)petSize : bounds.X + width / 2,
                bounds.Bottom - (int)petSize);
            var collapsed = PetBubbleLayout.Create(BubbleDirection.Top, false, petSize: petSize);
            Assert.Equal(anchor, collapsed.Position(anchor, 1, area));
            foreach (var preferred in Enum.GetValues<BubbleDirection>())
            {
                var hover = PetBubbleLayout.CreateHover(preferred, anchor, 1, area, petSize);
                AssertAnchoredAndVisible(hover, anchor, 1, area);
            }
            var motion = new PetWanderMotion(new Random(42));
            var next = motion.Step(anchor, new Size(petSize, petSize), area, 1, .04);
            Assert.InRange(Math.Abs(next.X - anchor.X), 0, 2);
            Assert.InRange(Math.Abs(next.Y - anchor.Y), 0, 2);
        }
    }

    [Theory]
    [InlineData(1, 96)]
    [InlineData(1, 192)]
    [InlineData(1, 288)]
    [InlineData(1.5, 192)]
    [InlineData(2, 288)]
    public void ReminderLayoutsReflowAtEdgesWithoutMovingThePet(double scale, double petSize)
    {
        var area = new PixelRect(-2560, 25, 2560, 1415);
        foreach (var direction in Enum.GetValues<BubbleDirection>())
        foreach (var x in new[] { area.X, area.X + 1000, area.Right - (int)Math.Ceiling(petSize * scale) })
        foreach (var y in new[] { area.Y, area.Y + 500, area.Bottom - (int)Math.Ceiling(petSize * scale) })
        foreach (var height in new[] { DesignSystem.SpeechInvitationHeight, DesignSystem.SpeechRestingHeight, DesignSystem.SpeechCompletedHeight })
        {
            var anchor = new PixelPoint(x, y);
            var layout = PetBubbleLayout.CreateExpanded(direction, anchor, scale, area, petSize, height, DesignSystem.SpeechBubbleWidth);
            AssertAnchoredAndVisible(layout, anchor, scale, area);
            var bubble = new Rect(layout.Bubble, new Size(DesignSystem.SpeechBubbleWidth, height));
            Assert.False(bubble.Intersects(new Rect(layout.Pet, new Size(petSize, petSize))));
        }
    }

    [Fact]
    public void MacAllowsDockOverlapAndStillExcludesTheMenuBar()
    {
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var work = new PixelRect(0, 25, 1920, 965);
        Assert.Equal(new PixelRect(0, 25, 1920, 1055), PetWindow.PlacementArea(bounds, work, macOS: true));
        Assert.Equal(work, PetWindow.PlacementArea(bounds, work, macOS: false));
    }

    private static void AssertAnchoredAndVisible(PetBubbleLayout layout, PixelPoint anchor, double scale, PixelRect area)
    {
        var position = layout.Position(anchor, scale, area);
        Assert.Equal(anchor, new PixelPoint(position.X + (int)Math.Round(layout.Pet.X * scale),
            position.Y + (int)Math.Round(layout.Pet.Y * scale)));
        Assert.InRange(position.X, area.X, area.Right - (int)Math.Ceiling(layout.Size.Width * scale));
        Assert.InRange(position.Y, area.Y, area.Bottom - (int)Math.Ceiling(layout.Size.Height * scale));
    }
}
