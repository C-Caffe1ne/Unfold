using Avalonia.Controls;
using Avalonia.Platform;

namespace Unfold.Desktop;

internal static class BrandTrayIcon
{
    internal static Uri AssetUri(bool windows) => new(windows
        ? "avares://Unfold/Assets/Icons/Brand/unfold.ico"
        : "avares://Unfold/Assets/Icons/Brand/unfold-64.png");

    internal static TrayIcon Create()
    {
        // Embed the approved brand assets so tray icons also work from an installed app.
        // Windows keeps the ICO's native sizes; macOS uses a high-resolution PNG.
        using var asset = AssetLoader.Open(AssetUri(OperatingSystem.IsWindows()));
        var tray = new TrayIcon { Icon = new WindowIcon(asset), ToolTipText = "Unfold", IsVisible = true };
        MacOSProperties.SetIsTemplateIcon(tray, false);
        return tray;
    }
}
