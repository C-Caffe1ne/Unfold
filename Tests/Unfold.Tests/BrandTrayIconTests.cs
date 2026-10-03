using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using SkiaSharp;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class BrandTrayIconTests
{
    [AvaloniaTheory]
    [InlineData(false, "f887ddf10c2d7fda49064c1db00aa29749fd21c62c48a3b2eba8903db2f1ebd9")]
    [InlineData(true, "54f8749829ebd075b5eb6f1f8ece19e7efe485441504345c3f9c07309137e01d")]
    public void PackagedTrayAssetsMatchTheApprovedBrandFiles(bool windows, string expectedHash)
    {
        using var asset = AssetLoader.Open(BrandTrayIcon.AssetUri(windows));
        using var buffer = new MemoryStream(); asset.CopyTo(buffer);
        var bytes = buffer.ToArray();
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        if (windows)
        {
            Assert.Equal(1, BitConverter.ToUInt16(bytes, 2));
            Assert.Equal(7, BitConverter.ToUInt16(bytes, 4));
        }
        else
        {
            using var image = SKBitmap.Decode(bytes);
            Assert.NotNull(image); Assert.Equal(64, image.Width); Assert.Equal(64, image.Height);
        }
    }

    [AvaloniaFact]
    public async Task RuntimeStartupRegistersAVisibleNonTemplateTrayWithTheExistingMenu()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            using var lifetime = new ClassicDesktopStyleApplicationLifetime();
            using var runtime = new AppRuntime(lifetime) { AccountServiceFactory = () => new FakeAccountService() };
            await runtime.Start(false);
            var tray = Assert.Single(TrayIcon.GetIcons(Application.Current!)!);
            Assert.True(tray.IsVisible); Assert.NotNull(tray.Menu);
            Assert.False(MacOSProperties.GetIsTemplateIcon(tray));
            Assert.IsType<WindowIcon>(tray.Icon);
        }
        finally { Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); }
    }

}
