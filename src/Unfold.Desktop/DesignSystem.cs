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
public static partial class DesignSystem
{
    // Compatibility alias kept while callers move to the semantic outline roles.
    public static IBrush Outline => OutlineSubtle;
    public const double Caption = 12, Body = 14, Section = 18, Title = 24;
    public const double Space = 8, Gap = 12, Inset = 20;
    public const double SettingsContentWidth = 760, SettingsControlHeight = 40, SettingsChoiceWidth = 200,
        SettingsNumberWidth = 160, SettingsRowGap = 16, SettingsActionWidth = 80,
        SettingsPreviewWidth = 80, SettingsImportWidth = 104, SettingsResetWidth = 64;
    public const double FocusRingWidth = 2, FocusRingOffset = 2;
    public const double HomeTimerHeight = 196, HomeControlHeight = 40, HomeChoiceWidth = 200,
        HomeNumberWidth = 160, HomeActionWidth = 80, HomePetScaleWidth = 280;
    public const double ReviewContentWidth = 760, ReviewControlHeight = 40, ReviewDateHeight = 44;
    public const double PetContentWidth = 760, PetControlHeight = 40, PetChoiceWidth = 200,
        PetPreviewOptionWidth = 148, PetPreviewWidth = 520, PetActionWidth = 128, PetActionHeight = 148;
    public const double SpeechBubbleWidth = 320, SpeechAdvanceHeight = 96, SpeechInvitationHeight = 159,
        SpeechRestingHeight = 196, SpeechCompletedHeight = 113, SpeechControlHeight = 40,
        SpeechTimerSize = 40, SpeechGap = 6, PetBaseSize = 192, PetBubbleGap = 12;
    public static readonly Thickness BorderSubtle = new(1), BorderStrong = new(1);
    public static readonly CornerRadius ControlRadius = new(12), CardRadius = new(24), FrameRadius = new(32);

    public static void Install(Application app)
    {
        var fluent = new FluentTheme();
        ApplyFluentPalette(app, fluent, Themes.Single(item => item.Id == CurrentTheme));
        app.Styles.Add(fluent);
        var styles = new Styles();
        styles.Add(new Style(s => s.OfType<FlyoutPresenter>().Class("theme-picker")) { Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, Surface), new Setter(TemplatedControl.ForegroundProperty, Cream),
            new Setter(TemplatedControl.BorderBrushProperty, OutlineStrong), new Setter(TemplatedControl.BorderThicknessProperty, BorderStrong),
            new Setter(TemplatedControl.CornerRadiusProperty, CardRadius), new Setter(TemplatedControl.PaddingProperty, new Thickness(16))
        }});
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
        { new Setter(TemplatedControl.BackgroundProperty, Accent), new Setter(TemplatedControl.ForegroundProperty, Ink) }});
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
        AddButtonState(styles, "primary", ":pressed", Accent, Ink);
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
                new Setter(TemplatedControl.FontSizeProperty, Body), new Setter(TemplatedControl.ForegroundProperty, Cream),
                new Setter(Layoutable.MinHeightProperty, 38d),
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
                var input = s.OfType<ComboBox>().Not(selector => selector.Class("settings-choice"))
                    .Not(selector => selector.Class("pet-choice")).Not(selector => selector.Class("pet-preview-choice"))
                    .Not(selector => selector.Class("home-choice"));
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
            new Setter(TextBox.SelectionBrushProperty, Accent), new Setter(TextBox.SelectionForegroundBrushProperty, Ink),
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
            { new Setter(ContentPresenter.BackgroundProperty, Accent), new Setter(ContentPresenter.ForegroundProperty, Ink) }});
        }
        styles.Add(new Style(s => s.OfType<ListBox>()) { Setters =
        { new Setter(TemplatedControl.BackgroundProperty, Surface), new Setter(TemplatedControl.CornerRadiusProperty, ControlRadius) }});
        styles.Add(new Style(s => s.OfType<ListBoxItem>()) { Setters =
        { new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 10)), new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)) }});
        AddChoiceStyles(styles, "settings-choice", SettingsChoiceWidth);
        AddChoiceStyles(styles, "home-choice", HomeChoiceWidth);
        AddChoiceStyles(styles, "pet-choice", PetChoiceWidth);
        AddChoiceStyles(styles, "pet-preview-choice", PetPreviewOptionWidth);
        styles.Add(new Style(s => s.OfType<Button>().Class("settings-reset")) { Setters =
        { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent), new Setter(TemplatedControl.ForegroundProperty, Muted) }});
        AddPetCardStyles(styles);
        app.Styles.Add(styles);
    }

    private static void AddChoiceStyles(Styles styles, string className, double popupWidth)
    {
        styles.Add(new Style(s => s.OfType<ComboBox>().Class(className)) { Setters =
        {
            new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 0)),
            new Setter(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center)
        }});
        foreach (var state in new[] { "", ":pointerover", ":pressed", ":focus", ":focus-visible", ":focus-within", ":disabled" })
        {
            Selector Choice(Selector? selector)
            {
                var choice = selector.OfType<ComboBox>().Class(className);
                return state.Length == 0 ? choice : choice.Class(state);
            }
            styles.Add(new Style(s => Choice(s).Template().OfType<Border>().Name("Background")) { Setters =
            {
                new Setter(Border.BackgroundProperty, Shell), new Setter(Border.BorderBrushProperty, OutlineStrong),
                new Setter(Border.BorderThicknessProperty, BorderStrong), new Setter(Border.CornerRadiusProperty, ControlRadius)
            }});
            foreach (var name in new[] { "HighlightBackground", "DropDownOverlay" })
                styles.Add(new Style(s => Choice(s).Template().OfType<Border>().Name(name)) { Setters =
                { new Setter(Visual.IsVisibleProperty, false) }});
        }
        styles.Add(new Style(s => s.OfType<ComboBox>().Class(className).Template().OfType<Popup>().Name("PART_Popup")) { Setters =
        { new Setter(Layoutable.WidthProperty, popupWidth) }});
        styles.Add(new Style(s => s.OfType<ComboBox>().Class(className).Template().OfType<Border>().Name("PopupBorder")) { Setters =
        {
            new Setter(Border.BackgroundProperty, Surface), new Setter(Border.BorderBrushProperty, OutlineStrong),
            new Setter(Border.BorderThicknessProperty, BorderStrong), new Setter(Border.PaddingProperty, new Thickness(4)),
            new Setter(Border.CornerRadiusProperty, ControlRadius)
        }});
        styles.Add(new Style(s => s.OfType<ComboBox>().Class(className).Template().OfType<ItemsPresenter>().Name("PART_ItemsPresenter")) { Setters =
        { new Setter(Layoutable.MarginProperty, new Thickness(0)) }});
        styles.Add(new Style(s => s.OfType<ComboBoxItem>().Class(className + "-item")) { Setters =
        {
            new Setter(Layoutable.MinHeightProperty, 36d), new Setter(Layoutable.MarginProperty, new Thickness(0)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 8)), new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(8))
        }});
        foreach (var state in new[] { ":pointerover", ":selected" })
            styles.Add(new Style(s => s.OfType<ComboBoxItem>().Class(className + "-item").Class(state)
                .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter")) { Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, state == ":selected" ? Accent : Raised),
                new Setter(ContentPresenter.ForegroundProperty, state == ":selected" ? Ink : Cream)
            }});
    }

    private static void AddPetCardStyles(Styles styles)
    {
        styles.Add(new Style(s => s.OfType<Button>().Class("pet-action-preview")) { Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
            new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            new Setter(TemplatedControl.PaddingProperty, new Thickness(0)),
            new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(18)),
            new Setter(Layoutable.MinHeightProperty, 0d)
        }});
        foreach (var state in new[] { "", ":pointerover", ":pressed", ":disabled", ":focus-visible" })
            styles.Add(new Style(s =>
            {
                var button = s.OfType<Button>().Class("pet-action-preview");
                return (state.Length == 0 ? button : button.Class(state))
                    .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
            }) { Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent),
                new Setter(ContentPresenter.BorderBrushProperty, state == ":focus-visible" ? Cream : Brushes.Transparent),
                new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(1))
            }});
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
