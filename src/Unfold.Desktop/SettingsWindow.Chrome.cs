using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private void ConfigureNativeWindowControls()
    {
        WindowDecorations = WindowDecorations.Full;
        // AppKit keeps its standard buttons in an extended title bar. In Avalonia 12,
        // extending on Windows instead opts into Avalonia-drawn caption controls.
        ExtendClientAreaToDecorationsHint = OperatingSystem.IsMacOS();
        ExtendClientAreaTitleBarHeightHint = -1;
    }

    private Grid BuildNativeWindowContent(Border content)
    {
        var titleBar = new Border { Name = "SettingsNativeTitleBar", Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Top };
        // Route the extended area to the backend's native drag and title-bar double-click
        // handling. Do not capture the pointer or update window coordinates ourselves.
        WindowDecorationProperties.SetElementRole(titleBar, WindowDecorationsElementRole.TitleBar);
        void UpdateInsets()
        {
            var inset = IsExtendedIntoWindowDecorations ? WindowDecorationMargin : default;
            content.Margin = inset;
            titleBar.Height = inset.Top;
            titleBar.IsVisible = inset.Top > 0;
        }
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowDecorationMarginProperty || args.Property == IsExtendedIntoWindowDecorationsProperty)
                UpdateInsets();
        };
        Opened += (_, _) => UpdateInsets();
        UpdateInsets();
        return new Grid { Children = { content, titleBar } };
    }
}
