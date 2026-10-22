using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Unfold.Desktop;

public static partial class DesignSystem
{
    /// <summary>Track thickness a scrollbar takes; the rounded thumb is drawn thinner inside it.</summary>
    public const double ScrollBarSize = 12, ScrollThumbSize = 6, ScrollThumbHoverSize = 8, ScrollThumbMinLength = 32;

    private static void InstallScrollBarResources(Application app)
    {
        // Fluent's template sizes the bar and its parts from this key.
        app.Resources["ScrollBarSize"] = ScrollBarSize;
        app.Resources["ScrollBarThumbFillPointerOver"] = Muted;
        app.Resources["ScrollBarThumbFillPressed"] = Muted;
        app.Resources["ScrollBarThumbFillDisabled"] = OutlineSubtle;
    }

    private static void AddScrollBarStyles(Styles styles)
    {
        foreach (var (orientation, vertical) in new[] { (":vertical", true), (":horizontal", false) })
        {
            // No line arrows or track fill: the thumb spans the whole length. The wheel, keyboard,
            // track clicks and the context menu still scroll by line and page.
            foreach (var name in new[] { "PART_LineUpButton", "PART_LineDownButton" })
                styles.Add(new Style(s => s.OfType<ScrollBar>().Class(orientation).Template().OfType<RepeatButton>().Name(name))
                { Setters = { new Setter(Visual.IsVisibleProperty, false) } });
            styles.Add(new Style(s => s.OfType<ScrollBar>().Class(orientation).Template().OfType<Rectangle>().Name("TrackRect"))
            { Setters = { new Setter(Visual.IsVisibleProperty, false) } });
            // Auto-hiding bars keep the same thumb instead of Fluent's 2px collapsed line.
            foreach (var (state, size) in new[] { ("", ScrollThumbSize), (":pointerover", ScrollThumbHoverSize) })
                styles.Add(new Style(s =>
                {
                    var bar = s.OfType<ScrollBar>().Class(orientation);
                    return (state.Length == 0 ? bar : bar.Class(state)).Template().OfType<Thumb>();
                }) { Setters =
                {
                    new Setter(vertical ? Layoutable.WidthProperty : Layoutable.HeightProperty, size),
                    new Setter(vertical ? Layoutable.MinHeightProperty : Layoutable.MinWidthProperty, ScrollThumbMinLength),
                    new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(size / 2)),
                    new Setter(TemplatedControl.BackgroundProperty, OutlineStrong),
                    new Setter(Visual.RenderTransformProperty, null)
                }});
        }
    }
}
