using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class ThemeControlColorTests
{
    [AvaloniaFact]
    public void ExistingFluentTextAndGlyphsFollowConsecutiveLightAndDarkThemes()
    {
        var check = new CheckBox { Content = "알림 효과음 사용", IsChecked = true };
        var choice = new ComboBox { ItemsSource = new[] { "반복" }, SelectedIndex = 0 };
        var bar = new ScrollBar { Orientation = Orientation.Vertical, Maximum = 100, Value = 50,
            ViewportSize = 10, AllowAutoHide = false, Width = 16, Height = 180 };
        var window = Page(Ui.Column(check, choice, bar));
        try
        {
            window.Show(); Layout(window);
            foreach (var theme in new[] { AppTheme.Plum, AppTheme.OatLatte, AppTheme.Sage, AppTheme.Plum,
                         AppTheme.MidnightBlue, AppTheme.OatLatte })
            {
                DesignSystem.ApplyTheme(theme); Layout(window);
                var palette = DesignSystem.Themes.Single(p => p.Id == theme);
                AssertColor(palette.Text, check.Foreground);
                AssertColor(palette.Text, check.GetVisualDescendants().OfType<TextBlock>().Single().Foreground);
                AssertColor(palette.Muted, choice.GetVisualDescendants().OfType<DirectionIcon>().Single().Foreground);
                foreach (var icon in bar.GetVisualDescendants().OfType<DirectionIcon>())
                    AssertColor(palette.Text, icon.Foreground);
            }
        }
        finally { window.Close(); DesignSystem.ApplyTheme(AppSettings.DefaultTheme); }
    }

    [AvaloniaFact]
    public void OpenAndReopenedTooltipsUseCurrentThemeInsteadOfButtonSelectionColors()
    {
        var button = Ui.Primary(Ui.Action("저장")); ToolTip.SetTip(button, "변경한 설정 저장");
        var window = Page(button);
        try
        {
            window.Show(); Layout(window); ToolTip.SetIsOpen(button, true); Layout(window);
            var tooltip = Assert.IsType<ToolTip>(button.GetValue(ToolTipDiagnostics.ToolTipProperty));
            foreach (var theme in new[] { AppTheme.Plum, AppTheme.OatLatte, AppTheme.Sage, AppTheme.MidnightBlue, AppTheme.Plum })
            {
                DesignSystem.ApplyTheme(theme); Layout(window);
                var palette = DesignSystem.Themes.Single(p => p.Id == theme);
                AssertColor(palette.Surface, tooltip.Background);
                AssertColor(palette.Text, tooltip.Foreground);
                AssertColor(palette.Line, tooltip.BorderBrush);
                AssertColor(palette.Text, tooltip.GetVisualDescendants().OfType<TextBlock>().Single().Foreground);
                Assert.Equal(DesignSystem.AppFont, tooltip.FontFamily);
                AssertColor(palette.OnAccent, button.Foreground);
            }
            ToolTip.SetIsOpen(button, false); ToolTip.SetIsOpen(button, true); Layout(window);
            tooltip = Assert.IsType<ToolTip>(button.GetValue(ToolTipDiagnostics.ToolTipProperty));
            AssertColor(DesignSystem.Themes.Single(p => p.Id == AppTheme.Plum).Surface, tooltip.Background);
        }
        finally { ToolTip.SetIsOpen(button, false); window.Close(); DesignSystem.ApplyTheme(AppSettings.DefaultTheme); }
    }

    [AvaloniaFact]
    public void PlaceholdersReadOnlyValuesAndDisabledInputTextUseSemanticThemeColors()
    {
        var name = new TextBox { PlaceholderText = "펫 이름" };
        var number = new NumericUpDown { Value = 5, IsReadOnly = true };
        var choice = new ComboBox { ItemsSource = new[] { "반복" }, SelectedIndex = 0 };
        var window = Page(Ui.Column(name, number, choice));
        try
        {
            window.Show(); Layout(window);
            foreach (var palette in DesignSystem.Themes)
            {
                DesignSystem.ApplyTheme(palette.Id);
                name.IsEnabled = number.IsEnabled = choice.IsEnabled = true; Layout(window);
                AssertColor(palette.Muted, name.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "PART_Placeholder").Foreground);
                AssertColor(palette.Text, number.GetVisualDescendants().OfType<TextBox>().Single().Foreground);
                name.Text = "모찌"; name.IsEnabled = number.IsEnabled = choice.IsEnabled = false; Layout(window);
                AssertColor(palette.DisabledText, name.Foreground);
                AssertColor(palette.DisabledText, number.GetVisualDescendants().OfType<TextBox>().Single().Foreground);
                AssertColor(palette.DisabledText, choice.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "반복").Foreground);
                AssertColor(palette.DisabledText, choice.GetVisualDescendants().OfType<DirectionIcon>().Single().Foreground);
                name.Text = "";
            }
        }
        finally { window.Close(); DesignSystem.ApplyTheme(AppSettings.DefaultTheme); }
    }

    private static Window Page(Control content) => new() { Width = 460, Height = 400, Content = content, Classes = { "unfold-page" } };
    private static void AssertColor(string expected, IBrush? actual) => Assert.Equal(Color.Parse(expected), Assert.IsAssignableFrom<ISolidColorBrush>(actual).Color);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
