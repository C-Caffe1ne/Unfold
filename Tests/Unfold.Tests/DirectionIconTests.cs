using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class DirectionIconTests
{
    [AvaloniaFact]
    public void TemplateGlyphsPreserveInputAndDoNotReplaceSliderTrackButtons()
    {
        var number = new NumericUpDown { Value = 5, Minimum = 1, Maximum = 60, Width = 160, Height = 40 };
        var choice = new ComboBox { ItemsSource = new[] { "왼쪽", "오른쪽" }, SelectedIndex = 0, Width = 200, Height = 40 };
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50, Width = 200 };
        var window = new Window { Width = 360, Height = 320, Content = Ui.Column(number, choice, slider) };
        try
        {
            window.Show(); Layout(window);
            var increase = number.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_IncreaseButton");
            var decrease = number.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_DecreaseButton");
            AssertGlyph(increase, IconDirection.Up);
            AssertGlyph(decrease, IconDirection.Down);
            AssertGlyph(choice, IconDirection.Down);
            Assert.Empty(slider.GetVisualDescendants().OfType<DirectionIcon>());

            Click(increase); Assert.Equal(6, number.Value);
            Click(decrease); Assert.Equal(5, number.Value);
            var location = choice.TranslatePoint(new Point(choice.Bounds.Width - 16, choice.Bounds.Height / 2), window)!.Value;
            window.MouseDown(location, MouseButton.Left); window.MouseUp(location, MouseButton.Left); Layout(window);
            Assert.True(choice.IsDropDownOpen);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, ""); Layout(window);
            Assert.False(choice.IsDropDownOpen);

            number.IsEnabled = false; choice.IsEnabled = false; Layout(window);
            var disabled = Assert.IsAssignableFrom<ISolidColorBrush>(DesignSystem.DisabledText).Color;
            Assert.Equal(disabled, Assert.IsAssignableFrom<ISolidColorBrush>(AssertGlyph(increase, IconDirection.Up).Foreground).Color);
            AssertGlyph(choice, IconDirection.Down);
        }
        finally { window.Close(); }

        void Click(Control control)
        {
            var location = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseDown(location, MouseButton.Left); window.MouseUp(location, MouseButton.Left); Layout(window);
        }
    }

    [AvaloniaFact]
    public void ScrollBarUsesMatchingDirectionsWhenOrientationChanges()
    {
        var bar = new ScrollBar { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, Value = 50,
            ViewportSize = 10, AllowAutoHide = false, Width = 16, Height = 180 };
        var window = new Window { Width = 300, Height = 260, Content = bar };
        try
        {
            window.Show(); Layout(window); Check(IconDirection.Up, IconDirection.Down);
            bar.Orientation = Orientation.Horizontal; bar.Width = 180; bar.Height = 16; Layout(window);
            Check(IconDirection.Left, IconDirection.Right);
        }
        finally { window.Close(); }

        void Check(IconDirection first, IconDirection second)
        {
            var before = bar.Value;
            var up = bar.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_LineUpButton");
            var down = bar.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_LineDownButton");
            AssertGlyph(up, first); AssertGlyph(down, second);
            up.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.True(bar.Value < before);
            down.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(before, bar.Value);
        }
    }

    private static DirectionIcon AssertGlyph(Control parent, IconDirection direction)
    {
        var icon = Assert.Single(parent.GetVisualDescendants().OfType<DirectionIcon>());
        Assert.Equal(direction, icon.Direction);
        Assert.Equal(new Size(24, 24), icon.Bounds.Size);
        Assert.False(icon.IsHitTestVisible);
        var slot = Assert.IsType<PathIcon>(icon.GetVisualParent());
        Assert.False(slot.ClipToBounds);
        var origin = icon.TranslatePoint(default, slot)!.Value;
        Assert.InRange(Math.Abs(origin.X + 12 - slot.Bounds.Width / 2), 0, .5);
        Assert.InRange(Math.Abs(origin.Y + 12 - slot.Bounds.Height / 2), 0, .5);
        return icon;
    }

    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
