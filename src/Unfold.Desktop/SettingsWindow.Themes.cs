using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private Button BuildThemeButton()
    {
        var button = Ui.Action("");
        button.Name = "SettingsTheme";
        button.Width = button.Height = 46;
        button.Padding = new(9);
        button.Content = new NavigationIcon("theme");
        AutomationProperties.SetName(button, "테마");
        ToolTip.SetTip(button, "테마"); ToolTip.SetShowDelay(button, 500);
        var error = Ui.Caption(""); error.Name = "ThemeSaveError"; error.Foreground = DesignSystem.Error; error.IsVisible = false;
        var content = Ui.Column(Ui.Text("테마", DesignSystem.Section), error);
        content.Name = "ThemeChoices"; content.Width = 232; content.Spacing = 8;
        var flyout = new Flyout { Content = content, Placement = PlacementMode.Right };
        flyout.FlyoutPresenterClasses.Add("theme-picker");
        var choices = new List<(Button Button, TextBlock Check, ThemePalette Palette)>();
        void RefreshChoices()
        {
            foreach (var (choice, check, palette) in choices)
            {
                var selected = runtime.Settings.Theme == palette.Id;
                choice.Classes.Set("primary", selected);
                check.IsVisible = selected;
                AutomationProperties.SetName(choice, palette.Name + (selected ? ", 선택됨" : ""));
            }
        }
        foreach (var palette in DesignSystem.Themes)
        {
            var choice = Ui.Action(""); choice.Name = "Theme" + palette.Id; choice.Height = 44;
            choice.HorizontalAlignment = HorizontalAlignment.Stretch;
            choice.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var row = new Grid { ColumnDefinitions = new("24,12,*,20") };
            row.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new(11),
                Background = Brush.Parse(palette.Secondary), BorderBrush = Brush.Parse(palette.Line), BorderThickness = new(1),
                Child = new Border { Margin = new(5), CornerRadius = new(6), Background = Brush.Parse(palette.Accent) } });
            var label = new TextBlock { Text = palette.Name, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 2); row.Children.Add(label);
            var check = new TextBlock { Text = "✓", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(check, 3); row.Children.Add(check);
            choice.Content = row; choices.Add((choice, check, palette)); content.Children.Insert(content.Children.Count - 1, choice);
            choice.Click += (_, _) =>
            {
                try
                {
                    runtime.SetTheme(palette.Id);
                    error.IsVisible = false; RefreshChoices(); flyout.Hide();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    AppPaths.Log(ex); error.Text = "테마를 저장하지 못했어요. 다시 선택해 주세요."; error.IsVisible = true;
                }
            };
        }
        flyout.Opening += (_, _) => { error.IsVisible = false; RefreshChoices(); };
        button.Flyout = flyout;
        RefreshChoices();
        return button;
    }
}
