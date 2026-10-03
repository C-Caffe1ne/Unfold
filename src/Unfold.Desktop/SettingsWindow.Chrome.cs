using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private IPointer? windowMovePointer;
    private PixelPoint windowMoveStart, windowPositionStart;
    private Border? windowDragRegion;

    private Control BuildWindowControls()
    {
        windowDragRegion = new Border { Name = "SettingsWindowDragRegion", Background = Brushes.Transparent };
        WindowDecorationProperties.SetElementRole(windowDragRegion, WindowDecorationsElementRole.User);
        windowDragRegion.PointerPressed += (_, args) =>
        {
            if (windowMovePointer is not null || !args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (args.ClickCount == 2) { ToggleWindowMaximized(); args.Handled = true; return; }
            if (OperatingSystem.IsMacOS())
            {
                if (WindowState != WindowState.Normal) return;
                // As in the account window, managed dragging also works for macOS
                // windows without a native title bar.
                windowMoveStart = WindowPointerScreenPosition(args); windowPositionStart = Position;
                windowMovePointer = args.Pointer; args.Pointer.Capture(windowDragRegion);
            }
            else BeginMoveDrag(args);
            args.Handled = true;
        };
        windowDragRegion.PointerMoved += (_, args) =>
        {
            if (windowMovePointer != args.Pointer) return;
            var delta = WindowPointerScreenPosition(args) - windowMoveStart;
            Position = new PixelPoint(windowPositionStart.X + delta.X, windowPositionStart.Y + delta.Y);
            args.Handled = true;
        };
        windowDragRegion.PointerReleased += (_, args) =>
        {
            if (windowMovePointer != args.Pointer || args.InitialPressMouseButton != MouseButton.Left) return;
            EndWindowMove(); args.Handled = true;
        };
        windowDragRegion.PointerCaptureLost += (_, _) => EndWindowMove();
        Deactivated += (_, _) => EndWindowMove();

        var close = WindowButton("SettingsWindowClose", "닫기", "#FF5F57", "M1,1 L7,7 M7,1 L1,7", Close);
        var minimize = WindowButton("SettingsWindowMinimize", "최소화", "#FEBC2E", "M1,4 L7,4", () => WindowState = WindowState.Minimized);
        var maximize = WindowButton("SettingsWindowMaximize", "최대화", "#28C840", "M1,4 L1,1 L4,1 M4,7 L7,7 L7,4", ToggleWindowMaximized);
        void RefreshWindowControls()
        {
            minimize.IsEnabled = CanMinimize;
            maximize.IsEnabled = CanResize && CanMaximize;
            var label = WindowState is WindowState.Maximized or WindowState.FullScreen ? "복원" : "최대화";
            AutomationProperties.SetName(maximize, label); ToolTip.SetTip(maximize, label);
        }
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty || args.Property == CanMinimizeProperty ||
                args.Property == CanMaximizeProperty || args.Property == CanResizeProperty)
            { EndWindowMove(); RefreshWindowControls(); }
        };
        RefreshWindowControls();
        var buttons = new StackPanel { Name = "SettingsWindowControls", Orientation = Orientation.Horizontal,
            Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left };
        buttons.Children.Add(close); buttons.Children.Add(minimize); buttons.Children.Add(maximize);
        var bar = new Grid { Name = "SettingsWindowControlBar", Height = 24 };
        bar.Children.Add(windowDragRegion); bar.Children.Add(buttons);
        return bar;
    }

    private static Button WindowButton(string name, string label, string color, string glyph, Action action)
    {
        var face = new Grid { Width = 14, Height = 14, IsHitTestVisible = false };
        face.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Fill = Brush.Parse(color) });
        var path = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(glyph), Width = 8, Height = 8,
            Stroke = Brush.Parse("#66000000"), StrokeThickness = 1, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        face.Children.Add(path);
        var button = new Button { Name = name, Content = face, Width = 28, Height = 24, MinWidth = 0, MinHeight = 0,
            Padding = new(0), CornerRadius = new(12), Background = Brushes.Transparent, BorderThickness = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        WindowDecorationProperties.SetElementRole(button, WindowDecorationsElementRole.User);
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        button.Click += (_, _) => action();
        return button;
    }

    private void ToggleWindowMaximized() => WindowState = WindowState is WindowState.Maximized or WindowState.FullScreen
        ? WindowState.Normal : WindowState.Maximized;

    private PixelPoint WindowPointerScreenPosition(PointerEventArgs args)
    {
        var point = args.GetPosition(this);
        return new PixelPoint(Position.X + (int)Math.Round(point.X * DesktopScaling),
            Position.Y + (int)Math.Round(point.Y * DesktopScaling));
    }

    private void EndWindowMove()
    {
        var pointer = windowMovePointer; windowMovePointer = null;
        if (pointer is not null && pointer.Captured == windowDragRegion) pointer.Capture(null);
    }
}
