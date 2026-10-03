using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Unfold.Desktop;

public static partial class DesignSystem
{
    private static void AddDirectionStyles(Styles styles)
    {
        AddDirection(s => s.OfType<ComboBox>().Template().OfType<PathIcon>().Name("DropDownGlyph"), IconDirection.Down);
        foreach (var (part, direction) in new[]
        {
            ("PART_IncreaseButton", IconDirection.Up), ("PART_DecreaseButton", IconDirection.Down)
        })
            AddDirection(s => s.OfType<ButtonSpinner>().Template().OfType<RepeatButton>().Name(part)
                .Child().OfType<PathIcon>(), direction);

        foreach (var (orientation, up, down) in new[]
        {
            (":vertical", IconDirection.Up, IconDirection.Down),
            (":horizontal", IconDirection.Left, IconDirection.Right)
        })
        {
            AddDirection(s => s.OfType<ScrollBar>().Class(orientation).Template()
                .OfType<RepeatButton>().Name("PART_LineUpButton").Child().OfType<PathIcon>(), up);
            AddDirection(s => s.OfType<ScrollBar>().Class(orientation).Template()
                .OfType<RepeatButton>().Name("PART_LineDownButton").Child().OfType<PathIcon>(), down);
        }

        void AddDirection(Func<Selector?, Selector> selector, IconDirection direction) => styles.Add(new Style(selector)
        {
            Setters =
            {
                // Replace only the glyph. Retain Fluent's button, layout slot and input states.
                // The transparent 24px SVG viewport is centered on the existing glyph's slot.
                // PathIcon clips by default; its old 8/12px slot must not crop the new SVG.
                new Setter(Visual.ClipToBoundsProperty, false),
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<PathIcon>((_, _) =>
                    new DirectionIcon(direction) { HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center }))
            }
        });
    }
}
