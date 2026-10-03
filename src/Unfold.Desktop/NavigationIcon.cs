namespace Unfold.Desktop;

/// <summary>Original Figma SVGs, tinted by the button's inherited theme foreground.</summary>
internal sealed class NavigationIcon(string assetName) : SvgIcon($"Navigation/{assetName}", assetName == "exit" ? 20 : 24);
