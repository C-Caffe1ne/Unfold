using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Unfold.Desktop;

namespace Unfold.Tests;

public class MacPetWindowTests
{
    [Theory]
    [InlineData(1u << 7)] // Avalonia's initial full-screen primary window.
    [InlineData((1u << 1) | (1u << 9) | (1u << 16))]
    [InlineData(1u << 17)]
    public void OverlayReplacesConflictingSpaceAndApplicationRoles(uint initial)
    {
        const uint retained = (1u << 6) | (1u << 12); // Cycle/tiling preferences.
        var behavior = MacPetWindow.CollectionBehavior(initial | retained, true);
        Assert.Equal((nuint)(retained | (1u << 0) | (1u << 8) | (1u << 18)), behavior);
        Assert.Equal(behavior, MacPetWindow.CollectionBehavior(behavior, true));
    }

    [Fact]
    public void OlderMacsDoNotReceiveTheMacOS13ApplicationRole()
    {
        var behavior = MacPetWindow.CollectionBehavior((1u << 18) | (1u << 7), false);
        Assert.Equal((nuint)((1u << 0) | (1u << 8)), behavior);
    }

    [AvaloniaFact]
    public void HeadlessHandlesAreNeverSentToObjectiveCOrMadeVisible()
    {
        var window = new Window();
        try
        {
            Assert.False(MacPetWindow.Apply(window));
            Assert.False(window.IsVisible);
            window.Show();
            Assert.False(MacPetWindow.Apply(window));
            window.Hide();
            Assert.False(MacPetWindow.Apply(window));
            Assert.False(window.IsVisible);
        }
        finally { window.Close(); }
    }
}
