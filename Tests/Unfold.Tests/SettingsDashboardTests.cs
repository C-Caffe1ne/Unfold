using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
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
    private static void AssertNoTabPageHeader(Window window)
    {
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name == "PageHeader");
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text =>
            (text.Text ?? "").StartsWith("UNFOLD /", StringComparison.Ordinal) || text.Text is
                "잠깐의 여유를 만들어 보세요." or "알림과 타이머를 설정하세요." or
                "나를 위해 만든 여유." or "새로운 친구를 만나 보세요." or
                "나만의 펫을 만들어 보세요." or "나의 페이스대로");
    }

    [AvaloniaFact]
    public void DashboardKeepsThePetAndTimerVisibleWhileOnlyDetailsScroll()
    {
        using var scope = new Scope(); var window = scope.Window;
        AssertNoTabPageHeader(window);
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680), new Size(1440, 960) })
        {
            window.Width = size.Width; window.Height = size.Height;
            Find<ScrollViewer>(window, "SettingsDetailsScroll").ScrollToHome();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var name in new[] { "SettingsCompanionCard", "SettingsTimerCard", "SettingsHomeTimingCard", "ReminderInterval", "BreakDurationMinutes", "TimerToggle", "TimerStop", "SettingsQuit", "LaunchAtLogin" })
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
    public async Task ResizingPreservesTimerDraftsAndLegacyRoutineData()
    {
        using var scope = new Scope(); var window = scope.Window;
        var legacyRoutine = new BreakRoutine("legacy-routine", "기존 루틴", [new("숨을 고르세요.", 30)]);
        var legacyProfile = new WorkProfile("legacy-profile", "기존 프로필", 45, 3, legacyRoutine.Id);
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings.SaveRoutine(legacyRoutine).SaveProfile(legacyProfile));
        Dispatcher.UIThread.RunJobs();
        var breakDuration = Find<NumericUpDown>(window, "BreakDurationMinutes"); breakDuration.Value = 3;
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        Assert.Equal(3, breakDuration.Value);
        Click(window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs();
        Click(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs();
        var idle = Find<NumericUpDown>(window, "ReminderIdle"); idle.Value = 12;
        var snooze = Find<NumericUpDown>(window, "SnoozeMinutes"); snooze.Value = 9;
        window.Width = window.MinWidth; window.Height = window.MinHeight; Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, idle.Value); Assert.Equal(9, snooze.Value);
        window.Width = 1120; window.Height = 800; Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, idle.Value); Assert.Equal(9, snooze.Value);
        Assert.Equal(5, scope.Runtime.Settings.IdleMinutes);
        Click(window, "SavePreferences"); Dispatcher.UIThread.RunJobs();
        var loaded = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(12, loaded.IdleMinutes); Assert.Equal(9, loaded.SnoozeMinutes); Assert.Equal(3, loaded.BreakDurationMinutes);
        Assert.Equal(legacyRoutine.Id, loaded.BreakRoutineId);
        Assert.Contains(loaded.AdditionalRoutines, routine => routine.Id == legacyRoutine.Id);
        Assert.Contains(loaded.WorkProfiles, profile => profile.Id == legacyProfile.Id);
    }

    [AvaloniaFact]
    public void PreferencesActionsStayBelowTheScrollableBodyAtBothWindowSizes()
    {
        using var scope = new Scope(); var window = scope.Window;
        Click(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs();
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680) })
        {
            window.Width = size.Width; window.Height = size.Height; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var scroll = Find<ScrollViewer>(window, "SettingsPreferencesScroll");
            var save = Find<Button>(window, "SavePreferences"); var cancel = Find<Button>(window, "CancelPreferences");
            var saveOrigin = save.TranslatePoint(default, window)!.Value;
            var cancelOrigin = cancel.TranslatePoint(default, window)!.Value;
            var bodyOrigin = scroll.TranslatePoint(default, window)!.Value;
            Assert.True(saveOrigin.Y >= bodyOrigin.Y + scroll.Bounds.Height);
            Assert.True(saveOrigin.Y + save.Bounds.Height <= window.ClientSize.Height);
            Assert.True(cancelOrigin.X + cancel.Bounds.Width < saveOrigin.X);
            Assert.Equal(cancelOrigin.Y, saveOrigin.Y);
            scroll.ScrollToEnd(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(saveOrigin, save.TranslatePoint(default, window)!.Value);
            Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 1);
        }
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Content as string == "적용");
    }

    [AvaloniaFact]
    public void HomeTimingApplyStaysAtTheCardTopRight()
    {
        using var scope = new Scope(); var window = scope.Window;
        window.UpdateLayout();
        var card = Find<Border>(window, "SettingsHomeTimingCard");
        var apply = Find<Button>(window, "ApplyHomeTimingSettings");
        var cardPosition = card.TranslatePoint(default, window)!.Value;
        var applyPosition = apply.TranslatePoint(default, window)!.Value;
        Assert.True(applyPosition.X > cardPosition.X + card.Bounds.Width / 2);
        Assert.True(applyPosition.Y < cardPosition.Y + 60);
        Assert.True(applyPosition.X + apply.Bounds.Width <= cardPosition.X + card.Bounds.Width);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "스트레칭 알림 간격 (분)");
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "휴식 시간 (분)");
    }

    [AvaloniaFact]
    public void SidebarUsesInWindowTabsAndReturnsFocusToTheTimer()
    {
        using var scope = new Scope(); var window = scope.Window;
        var navigation = window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Name?.StartsWith("SettingsNav", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(4, navigation.Length);
        foreach (var removed in new[] { "SettingsNavRoutines", "SettingsEditRoutine", "ApplyRoutineSettings", "SettingsOpenLibrary" })
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control => control.Name == removed);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TabControl>(), control => control.Name == "PersonalizationTabs");

        Click(window, "SettingsNavReview"); Dispatcher.UIThread.RunJobs();
        Assert.Empty(window.OwnedWindows);
        Assert.Equal("ReviewStatus", Find<TextBlock>(window, "ReviewStatus").Name);
        AssertNoTabPageHeader(window);

        var settings = Find<Button>(window, "SettingsNavSettings");
        Assert.Equal("설정 탭", AutomationProperties.GetName(settings));
        Click(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        Assert.Empty(window.OwnedWindows); Assert.Contains("primary", settings.Classes);
        Assert.Equal("SettingsPreferencesPage", Find<Grid>(window, "SettingsPreferencesPage").Name);
        AssertNoTabPageHeader(window);
        var notification = Find<Border>(window, "SettingsNotificationCard");
        Assert.Contains(notification.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "스트레칭 알림");
        Assert.Contains(notification.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "완료 알림");
        var timerSettings = Find<Border>(window, "SettingsTimerSettingsCard");
        foreach (var label in new[] { "자리 비움 시간 (분)", "다시 알림 시간 (분)" })
            Assert.Contains(timerSettings.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == label);
        Assert.DoesNotContain(timerSettings.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "스트레칭 알림 간격 (분)");

        Click(window, "SettingsNavTimer"); Dispatcher.UIThread.RunJobs();
        AssertNoTabPageHeader(window);
        Assert.True(Find<Button>(window, "TimerToggle").IsFocused);
    }

    [AvaloniaFact]
    public void PetPagePreservesDraftAcrossTabsAndSidebarNavigation()
    {
        using var scope = new Scope(); var window = scope.Window;
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => button.Name == "SettingsInstallPack");
        Assert.Equal(200, Find<ComboBox>(window, "CharacterPicker").Bounds.Width);
        var nav = Find<Button>(window, "SettingsNavPacks");
        Assert.Equal("펫 추가 탭", AutomationProperties.GetName(nav));
        Click(window, "SettingsNavPacks"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        Assert.Empty(window.OwnedWindows); Assert.Contains("primary", nav.Classes);
        AssertNoTabPageHeader(window);
        var tabs = Find<TabControl>(window, "PetManagementTabs");
        Assert.Equal(new[] { "펫 팩 열기", "펫 팩 만들기" }, tabs.Items.OfType<TabItem>().Select(item => item.Header));
        Assert.NotNull(Find<Button>(window, "OpenPetPack"));
        tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        AssertNoTabPageHeader(window);
        Find<TextBox>(window, "CustomPetName").Text = "새 친구";
        tabs.SelectedIndex = 0; Dispatcher.UIThread.RunJobs();
        tabs.SelectedIndex = 1; Dispatcher.UIThread.RunJobs();
        Assert.Equal("새 친구", Find<TextBox>(window, "CustomPetName").Text);
        Click(window, "SettingsNavTimer"); Dispatcher.UIThread.RunJobs();
        Click(window, "SettingsNavPacks"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, tabs.SelectedIndex); Assert.Equal("새 친구", Find<TextBox>(window, "CustomPetName").Text);
        window.HideToTray(); window.Show(); Dispatcher.UIThread.RunJobs();
        Assert.Equal("새 친구", Find<TextBox>(window, "CustomPetName").Text);
        window.Width = 860; window.Height = 680; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        var create = Find<Button>(window, "CreateCustomPetPack");
        var origin = create.TranslatePoint(default, window)!.Value;
        Assert.True(origin.Y >= 0 && origin.Y + create.Bounds.Height <= window.ClientSize.Height);
        Assert.Empty(window.OwnedWindows);
    }

    [AvaloniaFact]
    public void SpeechPreferencesApplyWhileWorkingWithoutResettingTheTimer()
    {
        using var scope = new Scope(); var window = scope.Window;
        scope.Runtime.Clock.Start(TimeSpan.Zero);
        scope.Runtime.Clock.Tick(TimeSpan.FromSeconds(8), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        var remaining = scope.Runtime.Clock.Remaining;
        Click(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs();
        Find<ComboBox>(window, "BubbleDirection").SelectedItem = BubbleDirection.Right;
        Find<CheckBox>(window, "ReminderSoundsEnabled").IsChecked = false;
        Assert.Equal(BubbleDirection.Top, scope.Runtime.Settings.BubbleDirection);
        Find<NumericUpDown>(window, "SnoozeMinutes").Value = 12;
        Click(window, "SavePreferences"); Dispatcher.UIThread.RunJobs();
        var settings = AppSettings.Load(Path.Combine(scope.Root, "settings.json"));
        Assert.Equal(BubbleDirection.Right, settings.BubbleDirection); Assert.Equal(12, settings.SnoozeMinutes);
        Assert.False(settings.ReminderSoundsEnabled); Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
        Assert.False(scope.Runtime.Clock.Paused);
        Find<NumericUpDown>(window, "SnoozeMinutes").Value = 2.5m;
        Click(window, "SavePreferences"); Dispatcher.UIThread.RunJobs();
        Assert.Equal(12, scope.Runtime.Settings.SnoozeMinutes);
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
            Window.HideToTray(); Window.Dispose(); Runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
