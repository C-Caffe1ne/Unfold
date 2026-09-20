using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class ThemeTests
{
    [Fact]
    public void MissingLegacyAndUnknownThemesUseOatWithoutLosingSettings()
    {
        using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "settings.json");
        Assert.Equal(AppTheme.OatLatte, AppSettings.Load(path).Theme);
        foreach (var json in new[] { "{\"intervalMinutes\":37}", "{\"theme\":99,\"intervalMinutes\":37}" })
        {
            File.WriteAllText(path, json);
            var value = AppSettings.Load(path);
            Assert.Equal(AppTheme.OatLatte, value.Theme); Assert.Equal(37, value.IntervalMinutes);
        }
        foreach (var theme in Enum.GetValues<AppTheme>())
        {
            new AppSettings { Theme = theme, IdleMinutes = 9 }.Save(path);
            Assert.Equal(theme, AppSettings.Load(path).Theme); Assert.Equal(9, AppSettings.Load(path).IdleMinutes);
        }
        Assert.Throws<InvalidDataException>(() => new AppSettings { Theme = (AppTheme)99 }.Save(path));
    }

    [AvaloniaFact]
    public void ThemeChoicesUpdateExistingViewsPersistAndPreserveDraftsAndClock()
    {
        using var scope = new Scope(); var window = scope.Window;
        var themeButton = Find<Button>(window, "SettingsTheme");
        var quit = Find<Button>(window, "SettingsQuit");
        window.Width = 860; window.Height = 680; Layout(window);
        var themeY = themeButton.TranslatePoint(default, window)!.Value.Y;
        Assert.True(themeY + themeButton.Bounds.Height < quit.TranslatePoint(default, window)!.Value.Y);
        Assert.True(quit.TranslatePoint(default, window)!.Value.Y + quit.Bounds.Height < window.ClientSize.Height);
        var flyout = (Flyout)themeButton.Flyout!;
        var point = themeButton.TranslatePoint(new Point(23, 23), window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Layout(window);
        Assert.True(flyout.IsOpen);
        // Dismissing the picker without choosing must not persist a different theme.
        flyout.Hide(); Assert.Equal(AppTheme.OatLatte, scope.Runtime.Settings.Theme);
        Press(Find<Button>(window, "TimerToggle"));
        var interval = Find<NumericUpDown>(window, "ReminderInterval"); interval.Value = 37;
        Press(Find<Button>(window, "SettingsNavSettings"));
        var idle = Find<NumericUpDown>(window, "ReminderIdle"); idle.Value = 19;
        var host = Find<ContentControl>(window, "SettingsPageHost"); var page = host.Content;
        var surface = Find<Border>(window, "SettingsTimerSettingsCard").Background;
        foreach (var palette in DesignSystem.Themes)
        {
            Choose(window, palette.Id);
            Assert.Same(page, host.Content); Assert.Same(surface, Find<Border>(window, "SettingsTimerSettingsCard").Background);
            Assert.Equal(Color.Parse(palette.Surface), ((ISolidColorBrush)surface!).Color);
            Assert.Equal(Color.Parse(palette.Canvas), ((ISolidColorBrush)window.Background!).Color);
            Assert.Equal(palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(palette.Id, scope.Runtime.Settings.Theme);
            // The default choice does not need to write a new file.
            Assert.Equal(palette.Id, AppSettings.Load(scope.Path).Theme);
            Assert.True(scope.Runtime.Clock.Paused); Assert.Equal(19, idle.Value);
            Assert.Equal(37, interval.Value); Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        }
        Press(Find<Button>(window, "SavePreferences"));
        Assert.Equal(AppTheme.Plum, AppSettings.Load(scope.Path).Theme); Assert.Equal(19, AppSettings.Load(scope.Path).IdleMinutes);
        Press(Find<Button>(window, "SettingsNavPacks"));
        var background = Find<ComboBox>(window, "PackBackground"); background.SelectedIndex = 1;
        var preview = Find<Border>(window, "PackPreviewSurface");
        Assert.DoesNotContain(preview.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains(".unfoldpet") == true);
        foreach (var palette in DesignSystem.Themes)
        {
            Choose(window, palette.Id);
            Assert.Equal(Brushes.WhiteSmoke, preview.Background);
        }
        background.SelectedIndex = 0;
        Assert.Same(DesignSystem.Surface, preview.Background);
        Find<TabControl>(window, "PetManagementTabs").SelectedIndex = 1; Layout(window);
        var name = Find<TextBox>(window, "CustomPetName"); name.Text = "작성 중인 펫";
        Choose(window, AppTheme.Sage); Assert.Equal("작성 중인 펫", name.Text);
        Assert.Same(name, Find<TextBox>(window, "CustomPetName"));
        using var restarted = new AppRuntime(new ClassicDesktopStyleApplicationLifetime());
        Assert.Equal(AppTheme.Sage, restarted.Settings.Theme); Assert.Equal(AppTheme.Sage, DesignSystem.CurrentTheme);
    }

    [AvaloniaFact]
    public void FailedThemeSaveKeepsOldColorsAndAllowsRetry()
    {
        using var scope = new Scope(); Directory.CreateDirectory(scope.Path);
        Choose(scope.Window, AppTheme.Plum);
        Assert.Equal(AppTheme.OatLatte, scope.Runtime.Settings.Theme); Assert.Equal(AppTheme.OatLatte, DesignSystem.CurrentTheme);
        var flyout = (Flyout)Find<Button>(scope.Window, "SettingsTheme").Flyout!;
        Assert.True(Find<TextBlock>((Control)flyout.Content!, "ThemeSaveError").IsVisible);
        Assert.True(flyout.IsOpen);
        Directory.Delete(scope.Path);
        Choose(scope.Window, AppTheme.Plum);
        Assert.Equal(AppTheme.Plum, AppSettings.Load(scope.Path).Theme); Assert.False(flyout.IsOpen);
    }

    [Fact]
    public void AllPalettesKeepTextAndPrimaryActionsReadable()
    {
        foreach (var palette in DesignSystem.Themes)
        {
            foreach (var foreground in new[] { palette.Text, palette.Muted, palette.Warning, palette.Error, palette.Success })
                foreach (var background in new[] { palette.Shell, palette.Surface, palette.Raised })
                    Assert.True(Contrast(foreground, background) >= 4.5, $"{palette.Name}: {foreground} / {background}");
            Assert.True(Contrast(palette.OnAccent, palette.Accent) >= 4.5);
            Assert.True(Contrast(palette.OnAccent, palette.AccentHover) >= 4.5);
        }
    }

    [AvaloniaFact]
    public void StoppedTimerColorStaysDistinctFromTheErrorColor()
    {
        foreach (var palette in DesignSystem.Themes)
        {
            // Stopping the timer is a deliberate action, so it must not read as the error state.
            Assert.NotEqual(palette.Error, palette.Stopped);
            DesignSystem.ApplyTheme(palette.Id);
            Assert.Equal(Color.Parse(palette.Stopped), DesignSystem.Stopped.Color);
            Assert.NotEqual(DesignSystem.Error.Color, DesignSystem.Stopped.Color);
            if (palette.IsDark) continue;
            // The light themes carry a darkened variant so the 13px badge text stays readable.
            foreach (var background in new[] { palette.Shell, palette.Surface })
                Assert.True(Contrast(palette.Stopped, background) >= 4.5,
                    $"{palette.Name}: {palette.Stopped} / {background} = {Contrast(palette.Stopped, background):0.00}");
        }
        DesignSystem.ApplyTheme(AppTheme.OatLatte);
    }

    private static double Contrast(string foreground, string background)
    {
        static double L(string value)
        {
            var c = Color.Parse(value);
            static double Channel(byte b) { var v = b / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        }
        var a = L(foreground); var b = L(background); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static T Find<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Press(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }
    private static void Choose(Window window, AppTheme theme)
    {
        var button = Find<Button>(window, "SettingsTheme"); var flyout = (Flyout)button.Flyout!;
        if (!flyout.IsOpen) flyout.ShowAt(button);
        Layout(window); Press(Find<Button>((Control)flyout.Content!, "Theme" + theme)); Layout(window);
    }
    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Path => System.IO.Path.Combine(temp.Path, "settings.json");
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Layout(Window);
        }
        public void Dispose()
        {
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
            DesignSystem.ApplyTheme(AppTheme.OatLatte);
        }
    }
}
