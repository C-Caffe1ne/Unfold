using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Unfold.Desktop;

/// <summary>Shared visual tokens and control states for Unfold's desktop UI.</summary>
public static class DesignSystem
{
    public static readonly IBrush Canvas = Brush.Parse("#141713"), Shell = Brush.Parse("#1D201D"),
        Surface = Brush.Parse("#2B2F2A"), Raised = Brush.Parse("#363C33"), Cream = Brush.Parse("#DFE5D1"),
        Ink = Brush.Parse("#252A23"), Muted = Brush.Parse("#B6BEB0"), Hover = Brush.Parse("#505A48"),
        AccentHover = Brush.Parse("#F0F3E9"), TextTertiary = Brush.Parse("#9CA798"),
        OutlineSubtle = Brush.Parse("#4F5B51"), OutlineStrong = Brush.Parse("#849187"),
        Error = Brush.Parse("#FFB4AB"), Warning = Brush.Parse("#F2CD7D"), Success = Brush.Parse("#9ED8AC"),
        DisabledFill = Brush.Parse("#292F29"), DisabledText = Brush.Parse("#929C91"), FocusRing = Brush.Parse("#8FD3FF");
    // Compatibility alias kept while callers move to the semantic outline roles.
    public static readonly IBrush Outline = OutlineSubtle;
    public const double Caption = 12, Body = 14, Section = 18, Title = 24;
    public const double Space = 8, Gap = 12, Inset = 20;
    public const double FocusRingWidth = 2, FocusRingOffset = 2;
    public static readonly Thickness BorderSubtle = new(1), BorderStrong = new(1);
    public static readonly CornerRadius ControlRadius = new(12), CardRadius = new(24), FrameRadius = new(32);

    public static void Install(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Dark] = new ColorPaletteResources
        {
            Accent = ColorOf(Cream), RegionColor = ColorOf(Shell),
            BaseHigh = ColorOf(Cream), BaseMediumHigh = ColorOf(Muted),
            AltHigh = ColorOf(Ink), ErrorText = ColorOf(Error)
        };
        app.Styles.Add(fluent);
        var styles = new Styles();
        styles.Add(new Style(s => s.OfType<Window>().Class("unfold-page")) { Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, Canvas), new Setter(TemplatedControl.ForegroundProperty, Cream),
            new Setter(TemplatedControl.FontSizeProperty, Body)
        }});
        styles.Add(new Style(s => s.OfType<Button>().Class("unfold-action")) { Setters =
        {
            new Setter(TemplatedControl.CornerRadiusProperty, ControlRadius),
            new Setter(TemplatedControl.BackgroundProperty, Raised), new Setter(TemplatedControl.ForegroundProperty, Cream),
            new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(2)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 8)),
            new Setter(Layoutable.MinHeightProperty, 38d), new Setter(TemplatedControl.FontSizeProperty, Body),
            new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
            new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
        }});
        styles.Add(new Style(s => s.OfType<Button>().Class("primary")) { Setters =
        { new Setter(TemplatedControl.BackgroundProperty, Cream), new Setter(TemplatedControl.ForegroundProperty, Ink) }});
        styles.Add(new Style(s => s.OfType<Button>().Class("quiet")) { Setters =
        { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent), new Setter(TemplatedControl.BorderBrushProperty, OutlineStrong) }});
        styles.Add(new Style(s => s.OfType<Button>().Class("danger")) { Setters =
        { new Setter(TemplatedControl.ForegroundProperty, Error) }});
        styles.Add(new Style(s => s.OfType<Button>().Class("compact")) { Setters =
        { new Setter(TemplatedControl.FontSizeProperty, Caption), new Setter(TemplatedControl.PaddingProperty, new Thickness(10, 8)) }});
        // Fluent paints interactive states on its presenter; style that same surface.
        AddButtonState(styles, null, ":pointerover", Hover, Cream);
        AddButtonState(styles, null, ":pressed", OutlineSubtle, Cream);
        AddButtonState(styles, "primary", ":pointerover", AccentHover, Ink);
        AddButtonState(styles, "primary", ":pressed", Muted, Ink);
        AddButtonState(styles, "danger", ":pointerover", Hover, Error);
        AddButtonState(styles, "danger", ":pressed", OutlineSubtle, Error);
        AddButtonState(styles, null, ":disabled", DisabledFill, DisabledText);
        styles.Add(new Style(s => s.OfType<Button>().Class("unfold-action").Class(":disabled")) { Setters =
        { new Setter(Visual.OpacityProperty, 1d) }});
        styles.Add(new Style(s => s.OfType<Button>().Class("unfold-action").Class(":focus-visible")) { Setters =
        { new Setter(TemplatedControl.BorderBrushProperty, Cream) }});
        styles.Add(new Style(s => s.OfType<Button>().Class("primary").Class(":focus-visible")) { Setters =
        { new Setter(TemplatedControl.BorderBrushProperty, Ink) }});
        foreach (var type in new[] { typeof(TextBox), typeof(NumericUpDown), typeof(ComboBox) })
        {
            styles.Add(new Style(s => s.Is(type)) { Setters =
            {
                new Setter(TemplatedControl.CornerRadiusProperty, ControlRadius),
                new Setter(TemplatedControl.FontSizeProperty, Body), new Setter(Layoutable.MinHeightProperty, 38d),
                new Setter(TemplatedControl.BackgroundProperty, Shell),
                new Setter(TemplatedControl.BorderBrushProperty, OutlineStrong),
                new Setter(TemplatedControl.BorderThicknessProperty, BorderStrong),
                new Setter(Control.FocusAdornerProperty, null)
            }});
            foreach (var state in new[] { ":pointerover", ":focus", ":focus-within", ":disabled" })
                styles.Add(new Style(s => s.Is(type).Class(state)) { Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Shell),
                    new Setter(TemplatedControl.BorderBrushProperty, OutlineStrong),
                    new Setter(TemplatedControl.BorderThicknessProperty, BorderStrong)
                }});
        }
        styles.Add(new Style(s => s.OfType<NumericUpDown>()) { Setters =
        {
            new Setter(NumericUpDown.TextAlignmentProperty, TextAlignment.Left),
            new Setter(NumericUpDown.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            new Setter(Visual.ClipToBoundsProperty, true)
        }});
        foreach (var state in new[] { "", ":pointerover", ":focus", ":focus-within", ":disabled" })
            styles.Add(new Style(s =>
            {
                var input = s.OfType<TextBox>();
                return (state.Length == 0 ? input : input.Class(state)).Template().OfType<Border>().Name("PART_BorderElement");
            }) { Setters =
            {
                new Setter(Border.BackgroundProperty, Shell),
                new Setter(Border.BorderBrushProperty, OutlineStrong),
                new Setter(Border.BorderThicknessProperty, BorderStrong)
            }});
        foreach (var state in new[] { "", ":pointerover", ":focus", ":focus-within", ":disabled" })
            styles.Add(new Style(s =>
            {
                var input = s.OfType<ComboBox>();
                return (state.Length == 0 ? input : input.Class(state)).Template().OfType<Border>();
            }) { Setters =
            {
                new Setter(Border.BackgroundProperty, Shell),
                new Setter(Border.BorderBrushProperty, OutlineStrong),
                new Setter(Border.BorderThicknessProperty, BorderStrong)
            }});
        // NumericUpDown supplies the visible outer border. Its inner TextBox stays transparent in every state.
        foreach (var state in new[] { "", ":pointerover", ":focus", ":focus-within", ":disabled" })
        {
            styles.Add(new Style(s =>
            {
                var selector = s.OfType<NumericUpDown>().Template().OfType<TextBox>().Name("PART_TextBox");
                return state.Length == 0 ? selector : selector.Class(state);
            }) { Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderBrushProperty, Brushes.Transparent),
                new Setter(TemplatedControl.BorderThicknessProperty, BorderStrong),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(0)),
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(Control.FocusAdornerProperty, null)
            }});
            styles.Add(new Style(s =>
            {
                var input = s.OfType<NumericUpDown>().Template().OfType<TextBox>().Name("PART_TextBox");
                return (state.Length == 0 ? input : input.Class(state)).Template().OfType<Border>().Name("PART_BorderElement");
            }) { Setters =
            {
                new Setter(Border.BackgroundProperty, Brushes.Transparent),
                new Setter(Border.BorderBrushProperty, Brushes.Transparent),
                new Setter(Border.BorderThicknessProperty, BorderStrong),
                new Setter(Border.CornerRadiusProperty, new CornerRadius(0))
            }});
        }
        foreach (var (name, radius) in new[]
        {
            ("PART_IncreaseButton", new CornerRadius(0)),
            ("PART_DecreaseButton", new CornerRadius(0, ControlRadius.TopRight, ControlRadius.BottomRight, 0))
        })
        {
            // Fluent places both arrows side by side; the decrease button alone touches the outer right edge.
            foreach (var state in new[] { "", ":pointerover", ":disabled" })
            {
                var foreground = state == ":disabled" ? DisabledText : Cream;
                styles.Add(new Style(s =>
                {
                    var button = s.OfType<RepeatButton>().Name(name);
                    return state.Length == 0 ? button : button.Class(state);
                }) { Setters =
                {
                    new Setter(TemplatedControl.BackgroundProperty, Shell),
                    new Setter(TemplatedControl.ForegroundProperty, foreground),
                    new Setter(TemplatedControl.CornerRadiusProperty, radius)
                }});
                styles.Add(new Style(s =>
                {
                    var button = s.OfType<RepeatButton>().Name(name);
                    return (state.Length == 0 ? button : button.Class(state))
                        .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
                }) { Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Shell),
                    new Setter(ContentPresenter.ForegroundProperty, foreground),
                    new Setter(ContentPresenter.CornerRadiusProperty, radius)
                }});
            }
        }
        styles.Add(new Style(s => s.OfType<TextBox>()) { Setters =
        {
            new Setter(TextBox.SelectionBrushProperty, Cream), new Setter(TextBox.SelectionForegroundBrushProperty, Ink),
            new Setter(TextBox.CaretBrushProperty, Cream)
        }});
        foreach (var state in new[] { ":checked", ":indeterminate" })
            styles.Add(new Style(s => s.OfType<CheckBox>().Class(state).Template().OfType<Avalonia.Controls.Shapes.Path>().Name("CheckGlyph"))
            { Setters = { new Setter(Avalonia.Controls.Shapes.Shape.FillProperty, Ink) } });
        foreach (var type in new[] { typeof(ListBoxItem), typeof(ComboBoxItem) })
        {
            styles.Add(new Style(s => s.Is(type).Class(":selected")) { Setters =
            { new Setter(TemplatedControl.ForegroundProperty, Ink) }});
            styles.Add(new Style(s => s.Is(type).Class(":selected").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")) { Setters =
            { new Setter(ContentPresenter.BackgroundProperty, Cream), new Setter(ContentPresenter.ForegroundProperty, Ink) }});
        }
        styles.Add(new Style(s => s.OfType<ListBox>()) { Setters =
        { new Setter(TemplatedControl.BackgroundProperty, Surface), new Setter(TemplatedControl.CornerRadiusProperty, ControlRadius) }});
        styles.Add(new Style(s => s.OfType<ListBoxItem>()) { Setters =
        { new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 10)), new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)) }});
        app.Styles.Add(styles);
    }

    private static Color ColorOf(IBrush brush) => ((ISolidColorBrush)brush).Color;

    private static void AddButtonState(Styles styles, string? variant, string state, IBrush fill, IBrush foreground)
    {
        styles.Add(new Style(s =>
        {
            var selector = s.OfType<Button>().Class("unfold-action");
            if (variant is not null) selector = selector.Class(variant);
            return selector.Class(state).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
        }) { Setters = { new Setter(ContentPresenter.BackgroundProperty, fill), new Setter(ContentPresenter.ForegroundProperty, foreground) }});
    }
}
