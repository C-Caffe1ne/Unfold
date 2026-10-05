using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Unfold.Desktop;

public static partial class Ui
{
    /// <summary>A collapsible app card with native expand/collapse and keyboard semantics.</summary>
    public static Expander Disclosure(string heading, Control content) => new()
    {
        Header = heading, Content = content, Background = DesignSystem.Surface,
        Foreground = DesignSystem.Cream, CornerRadius = DesignSystem.CardRadius,
        HorizontalAlignment = HorizontalAlignment.Stretch, IsTabStop = false,
        Theme = new ControlTheme { TargetType = typeof(Expander) },
        Template = new FuncControlTemplate<Expander>((expander, scope) =>
        {
            var toggle = new ToggleButton
            {
                Name = "ExpanderHeader", HorizontalAlignment = HorizontalAlignment.Stretch,
                Foreground = DesignSystem.Cream, MinHeight = DesignSystem.PetControlHeight,
                Theme = new ControlTheme { TargetType = typeof(ToggleButton) },
                Template = new FuncControlTemplate<ToggleButton>((button, _) =>
                {
                    var label = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center,
                        FontSize = DesignSystem.Section, FontWeight = FontWeight.SemiBold };
                    label.Bind(ContentPresenter.ContentProperty, new Binding(nameof(Expander.Header)) { Source = expander });
                    var up = new DirectionIcon(IconDirection.Up); var down = new DirectionIcon(IconDirection.Down);
                    up.Bind(Visual.IsVisibleProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = button });
                    down.Bind(Visual.IsVisibleProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = button, Converter = BoolConverters.Not });
                    var icons = new Grid { VerticalAlignment = VerticalAlignment.Center, Children = { up, down } };
                    var row = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = { label } };
                    Grid.SetColumn(icons, 2); row.Children.Add(icons);
                    return new Border { Name = "DisclosureHeaderSurface", CornerRadius = DesignSystem.CardRadius,
                        BorderBrush = Brushes.Transparent, BorderThickness = new(DesignSystem.FocusRingWidth),
                        Padding = new(16 - DesignSystem.FocusRingWidth), Background = Brushes.Transparent, Child = row };
                })
            };
            scope.Register("ExpanderHeader", toggle);
            AutomationProperties.SetName(toggle, heading);
            toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(nameof(Expander.IsExpanded)) { Source = expander, Mode = BindingMode.TwoWay });
            toggle.Styles.Add(new Style(s => s.OfType<ToggleButton>().Class(":pointerover").Template().OfType<Border>().Name("DisclosureHeaderSurface"))
                { Setters = { new Setter(Border.BackgroundProperty, DesignSystem.Hover) } });
            toggle.Styles.Add(new Style(s => s.OfType<ToggleButton>().Class(":focus-visible").Template().OfType<Border>().Name("DisclosureHeaderSurface"))
                { Setters = { new Setter(Border.BorderBrushProperty, DesignSystem.FocusRing) } });
            var body = new ContentPresenter { Name = "PART_ContentPresenter", Margin = new(16, 0, 16, 16),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            scope.Register("PART_ContentPresenter", body);
            body.Bind(ContentPresenter.ContentProperty, new Binding(nameof(Expander.Content)) { Source = expander });
            body.Bind(Visual.IsVisibleProperty, new Binding(nameof(Expander.IsExpanded)) { Source = expander });
            var layout = Column(toggle, body); layout.Spacing = 0;
            var card = Card(layout, padding: 0); card.Name = "DisclosureCard";
            return card;
        })
    };
}
