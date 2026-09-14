using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class SettingsDashboardTests
{
    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
    private static void Click(Window window, string name) => Find<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void DashboardKeepsThePetAndTimerVisibleWhileOnlyDetailsScroll()
    {
        using var scope = new Scope(); var window = scope.Window;
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680), new Size(1440, 960) })
        {
            window.Width = size.Width; window.Height = size.Height;
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var name in new[] { "SettingsCompanionCard", "SettingsTimerCard", "TimerToggle", "TimerStop", "TimerReset", "SettingsQuit", "LaunchAtLogin" })
            {
                var control = Find<Control>(window, name);
                var origin = control.TranslatePoint(default, window)!.Value;
                Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
                Assert.True(origin.X >= 0 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1, name);
                Assert.True(origin.Y >= 0 && origin.Y + control.Bounds.Height <= window.ClientSize.Height + 1, name);
            }
            var hero = Find<Border>(window, "SettingsCompanionCard"); var timer = Find<Border>(window, "SettingsTimerCard");
            Assert.True(hero.Bounds.Bottom < timer.Bounds.Top);
            var scroll = Find<ScrollViewer>(window, "SettingsDetailsScroll");
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
            // Scroll to the final card without moving the timer out of reach.
            scroll.ScrollToEnd(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var review = Find<Button>(window, "SettingsOpenReview");
            var reviewPosition = review.TranslatePoint(default, window)!.Value;
            Assert.True(reviewPosition.Y + review.Bounds.Height <= window.ClientSize.Height + 1);
        }
    }

    [AvaloniaFact]
    public void ResizingPreservesDraftsAndApplyStillPersistsBothSettings()
    {
        using var scope = new Scope(); var window = scope.Window;
        var idle = Find<NumericUpDown>(window, "ReminderIdle"); var routine = Find<ComboBox>(window, "RoutinePicker");
        idle.Value = 12; routine.SelectedItem = scope.Runtime.Routines.Single(item => item.Id == "look-away");
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, idle.Value); Assert.Equal("look-away", Assert.IsType<BreakRoutine>(routine.SelectedItem).Id);
        Assert.Equal(5, scope.Runtime.Settings.IdleMinutes); Assert.Equal(BreakRoutines.DefaultId, scope.Runtime.Settings.BreakRoutineId);
        Click(window, "ApplyReminderSettings"); Dispatcher.UIThread.RunJobs();
        var loaded = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(12, loaded.IdleMinutes); Assert.Equal("look-away", loaded.BreakRoutineId);
    }

    [AvaloniaFact]
    public void SidebarWorksWithTheKeyboardAndReturnsFocusToTheTimer()
    {
        using var scope = new Scope(); var window = scope.Window;
        var routines = Find<Button>(window, "SettingsNavRoutines");
        Assert.Equal("내 루틴 · 업무 프로필 열기", AutomationProperties.GetName(routines));
        Assert.NotNull(ToolTip.GetTip(routines));
        routines.Focus();
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " "); Dispatcher.UIThread.RunJobs();
        var dialog = Assert.Single(window.OwnedWindows); Assert.IsType<PersonalizationWindow>(dialog);
        dialog.Close(); Dispatcher.UIThread.RunJobs();
        Assert.True(routines.IsEnabled);
        Click(window, "SettingsNavTimer"); Dispatcher.UIThread.RunJobs();
        Assert.True(Find<Button>(window, "TimerToggle").IsFocused);
    }

    private sealed class Scope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        public string Root => temp.Path;
        public AppRuntime Runtime { get; }
        public SettingsWindow Window { get; }
        public Scope()
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Root);
            Runtime = new(lifetime); Window = new(Runtime); Window.Show(); Dispatcher.UIThread.RunJobs();
        }
        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Window.HideToTray(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
