using Unfold.Desktop;

namespace Unfold.Tests;

public sealed class AppReleaseTests
{
    [Theory]
    [InlineData("1.0.3-beta", "Beta v1.0.3")]
    [InlineData("1.0.3-beta.1", "Beta v1.0.3")]
    [InlineData("1.0.4-beta", "Beta v1.0.4")]
    [InlineData("1.1.0-beta", "Beta v1.1.0")]
    [InlineData("1.0.3", "v1.0.3")]
    [InlineData("1.0.3-rc.1", "v1.0.3-rc.1")]
    public void DisplayVersionPreservesThePublicBetaLabel(string version, string display)
    {
        Assert.Equal(display, AppRelease.FormatDisplayVersion(version));
    }
}
