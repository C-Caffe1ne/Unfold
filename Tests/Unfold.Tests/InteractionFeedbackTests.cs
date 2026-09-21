using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class InteractionFeedbackTests
{
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    [AvaloniaFact]
    public void SliderHighlightsOnlyItsThumbAndPreservesTrackClickDragAndKeyboardInput()
    {
        var slider = new Slider
        {
            Width = 280, Minimum = 50, Maximum = 150, Value = 100, SmallChange = 5,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        slider.Classes.Add("thumb-hover-slider");
        var window = new Window { Width = 400, Height = 140, Content = slider };
        try
        {
            window.Show(); Layout(window);
            var thumb = slider.GetVisualDescendants().OfType<Thumb>().Single();
            var container = slider.GetVisualDescendants().OfType<Grid>().Single(item => item.Name == "SliderContainer");
            var trackButtons = slider.GetVisualDescendants().OfType<RepeatButton>().ToArray();
            var tracks = slider.GetVisualDescendants().OfType<Border>().Where(item => item.Name == "TrackBackground").ToArray();
            Assert.Equal(2, trackButtons.Length); Assert.Equal(2, tracks.Length);
            var thumbColor = ColorOf(thumb.Background);
            var thumbBounds = thumb.Bounds;
            var trackColors = trackButtons.Select(button => ColorOf(button.Background)).ToArray();
            var trackBounds = tracks.Select(track => track.Bounds).ToArray();
            var containerColor = ColorOf(container.Background);
            void AssertTracksUnchanged()
            {
                Assert.Equal(containerColor, ColorOf(container.Background));
                Assert.Equal(trackColors, trackButtons.Select(button => ColorOf(button.Background)).ToArray());
                Assert.Equal(trackBounds, tracks.Select(track => track.Bounds).ToArray());
                Assert.Equal(thumbBounds, thumb.Bounds);
            }
            // Hover the control's empty padding and the visible track on either side.
            foreach (var offset in new[] { new Point(20, 1), new Point(20, slider.Bounds.Height / 2),
                new Point(slider.Bounds.Width - 20, slider.Bounds.Height / 2) })
            {
                window.MouseMove(slider.TranslatePoint(offset, window)!.Value); Layout(window);
                Assert.True(slider.IsPointerOver); Assert.False(thumb.IsPointerOver);
                Assert.Equal(thumbColor, ColorOf(thumb.Background)); AssertTracksUnchanged();
            }
            var thumbPoint = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            window.MouseMove(thumbPoint); Layout(window);
            Assert.True(thumb.IsPointerOver);
            Assert.NotEqual(thumbColor, ColorOf(thumb.Background)); AssertTracksUnchanged();

            window.MouseDown(thumbPoint, MouseButton.Left);
            window.MouseMove(thumbPoint + new Vector(40, 0));
            window.MouseUp(thumbPoint + new Vector(40, 0), MouseButton.Left); Layout(window);
            Assert.True(slider.Value > 100);
            var afterDrag = slider.Value;
            var trackPoint = slider.TranslatePoint(new Point(20, slider.Bounds.Height / 2), window)!.Value;
            window.MouseDown(trackPoint, MouseButton.Left); window.MouseUp(trackPoint, MouseButton.Left); Layout(window);
            Assert.True(slider.Value < afterDrag);

            slider.Focus(); Layout(window); Assert.True(slider.IsFocused);
            var beforeKey = slider.Value;
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Layout(window);
            Assert.Equal(beforeKey + slider.SmallChange, slider.Value);
        }
        finally { window.Close(); }
    }
}
