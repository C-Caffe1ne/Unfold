using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
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
            foreach (var name in new[] { "SettingsCompanionCard", "SettingsTimerCard", "SettingsHomeTimingCard", "ReminderInterval", "BreakDurationMinutes", "TimerToggle", "TimerStop", "SettingsQuit" })
            {
                var control = Find<Control>(window, name);
                var origin = control.TranslatePoint(default, window)!.Value;
                Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
                Assert.True(origin.X >= 0 && origin.X + control.Bounds.Width <= window.ClientSize.Width + 1, name);
                Assert.True(origin.Y >= 0 && origin.Y + control.Bounds.Height <= window.ClientSize.Height + 1, name);
            }
            var hero = Find<Border>(window, "SettingsCompanionCard"); var timer = Find<Border>(window, "SettingsTimerCard");
            Assert.Equal(196, timer.Bounds.Height);
            Assert.Equal(20, hero.Bounds.Top - timer.Bounds.Bottom);
            Assert.Equal(timer.TranslatePoint(default, window)!.Value.Y,
                Find<Border>(window, "SettingsHomeTimingCard").TranslatePoint(default, window)!.Value.Y);
            var main = Find<Grid>(window, "SettingsMain");
            Assert.Same(timer, main.Children[0]); Assert.Same(hero, main.Children[1]);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "TimerStateDetail");
            foreach (var name in new[] { "ReminderInterval", "BreakDurationMinutes" })
                Assert.Equal(new Size(160, 40), Find<NumericUpDown>(window, name).Bounds.Size);
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
    public void PetScaleSitsAboveThePickerAndPersistsWithoutLegendLabels()
    {
        using var scope = new Scope(); var window = scope.Window;
        var card = Find<Border>(window, "SettingsCompanionCard");
        var slider = Find<Slider>(window, "PetScale");
        var picker = Find<ComboBox>(window, "CharacterPicker");
        var value = Find<TextBlock>(window, "PetScaleValue");
        var label = Find<TextBlock>(window, "PetScaleLabel");
        window.UpdateLayout();
        var labelOrigin = label.TranslatePoint(default, card)!.Value;
        var sliderOrigin = slider.TranslatePoint(default, card)!.Value;
        var valueOrigin = value.TranslatePoint(default, card)!.Value;
        var pickerOrigin = picker.TranslatePoint(default, card)!.Value;
        Assert.True(labelOrigin.Y + label.Bounds.Height <= sliderOrigin.Y);
        Assert.True(sliderOrigin.Y + slider.Bounds.Height <= valueOrigin.Y);
        Assert.True(sliderOrigin.Y + slider.Bounds.Height < pickerOrigin.Y);
        Assert.Equal(50, slider.Minimum); Assert.Equal(150, slider.Maximum);
        Assert.Equal(10, slider.TickFrequency); Assert.True(slider.IsSnapToTickEnabled);
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<TextBlock>(), text => text.Text is "축소" or "기본" or "확대");

        var host = Find<Control>(window, "CompanionPreview");
        var stage = Find<Grid>(window, "CompanionPreviewStage");
        var previewScroll = Find<ScrollViewer>(window, "CompanionPreviewScroll");
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680) })
        {
            window.Width = size.Width; window.Height = size.Height;
            foreach (var scale in new[] { 50, 100, 150 })
            {
                slider.Value = scale; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var actualSize = DesignSystem.PetBaseSize * scale / 100d;
                Assert.Equal(actualSize, host.Width); Assert.Equal(actualSize, host.Height);
                Assert.Equal(actualSize, stage.Width); Assert.Equal(actualSize, stage.Height);
                Assert.Equal($"{scale}%", value.Text);
                Assert.True(previewScroll.Extent.Width <= previewScroll.Viewport.Width + 1);
                Assert.Equal(size == new Size(860, 680) && scale == 150,
                    previewScroll.Extent.Height > previewScroll.Viewport.Height + 1);
            }
        }
        Assert.Equal("150%", value.Text); Assert.Equal(150, scope.Runtime.Settings.PetScalePercent);
        Assert.Equal(150, AppSettings.Load(Path.Combine(scope.Root, "settings.json")).PetScalePercent);
        Assert.True(card.TranslatePoint(default, window)!.Value.X + card.Bounds.Width <= window.ClientSize.Width + 1);
    }

    [AvaloniaFact]
    public void LongPetNameAndSaveFailureKeepHomeActionsInsideTheirCards()
    {
        using var scope = new Scope(); var window = scope.Window;
        window.Width = 860; window.Height = 680;
        var name = Find<TextBlock>(window, "CompanionName");
        name.Text = "오래 함께할 아주 긴 이름을 가진 나만의 새로운 고양이 친구";
        var rest = Find<NumericUpDown>(window, "BreakDurationMinutes"); rest.Value = 4;
        // An actual failed save must keep the draft and the next action visible.
        var settingsFile = Path.Combine(scope.Root, "settings.json");
        if (File.Exists(settingsFile)) File.Delete(settingsFile);
        Directory.CreateDirectory(settingsFile);
        Click(window, "ApplyHomeTimingSettings"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        Assert.Contains("저장하지 못했어요", Find<TextBlock>(window, "HomeTimingStatus").Text);
        Assert.Equal(4, rest.Value); Assert.NotEqual(4, scope.Runtime.Settings.BreakDurationMinutes);
        Assert.True(Find<Button>(window, "ApplyHomeTimingSettings").IsEnabled);
        foreach (var pair in new[] { ("CharacterPicker", "SettingsCompanionCard"), ("PetScale", "SettingsCompanionCard"),
            ("HomeTimingStatus", "SettingsHomeTimingCard"), ("TimerStop", "SettingsTimerCard") })
        {
            var control = Find<Control>(window, pair.Item1); var card = Find<Border>(window, pair.Item2);
            var point = control.TranslatePoint(default, card)!.Value;
            Assert.True(point.X >= 0 && point.X + control.Bounds.Width <= card.Bounds.Width, pair.Item1);
            Assert.True(point.Y >= 0 && point.Y + control.Bounds.Height <= card.Bounds.Height, pair.Item1);
        }
        Assert.Equal(2, name.MaxLines);
        Assert.Equal(196, Find<Border>(window, "SettingsTimerCard").Bounds.Height);
    }

    [AvaloniaFact]
    public async Task HomeSeparatesAllSixTimerStateTitlesWithoutHelperCopy()
    {
        using var scope = new Scope(); var window = scope.Window;
        window.Width = 860; window.Height = 680;
        void AssertState(string title)
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.Equal(title, Find<TextBlock>(window, "TimerStateText").Text);
            var badge = Find<Border>(window, "TimerStateBadge"); var toggle = Find<Button>(window, "TimerToggle");
            Assert.True(badge.TranslatePoint(default, window)!.Value.X + badge.Bounds.Width < toggle.TranslatePoint(default, window)!.Value.X);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "TimerStateDetail");
        }
        AssertState("진행 중");
        Click(window, "TimerToggle"); AssertState("일시정지");
        Click(window, "TimerStop"); AssertState("중지됨");
        Click(window, "TimerToggle");
        scope.Runtime.Clock.Start(TimeSpan.Zero);
        scope.Runtime.Clock.Tick(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(5));
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        AssertState("자리 비움");
        scope.Runtime.Reminder.Invite(new(BreakRoutines.All[0], "default-cat"));
        await scope.Runtime.UpdateSettings(scope.Runtime.Settings);
        AssertState("휴식 대기 중");
        scope.Runtime.StartBreak(); AssertState("휴식 중");
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
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), control =>
            control.Name is "ShowPetOnDesktop" or "LaunchAtLogin");

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
        var appBehavior = Find<Border>(window, "SettingsAppBehaviorCard");
        Assert.Contains(appBehavior.GetVisualDescendants().OfType<CheckBox>(), checkbox => checkbox.Name == "ShowPetOnDesktop");
        Assert.Contains(appBehavior.GetVisualDescendants().OfType<CheckBox>(), checkbox => checkbox.Name == "LaunchAtLogin");
        Assert.NotNull(Find<Border>(window, "SettingsDebugToolsCard"));

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

    [AvaloniaFact]
    public void DebugPreviewButtonsAreOptInAndDoNotChangeLiveState()
    {
        using var scope = new Scope(); var window = scope.Window;
        Click(window, "SettingsNavSettings"); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        var toggle = Find<CheckBox>(window, "DebugToolsEnabled");
        var firstPreview = Find<Button>(window, "DebugPreviewAdvance");
        Assert.False(toggle.IsChecked); Assert.False(firstPreview.IsEffectivelyVisible);

        toggle.IsChecked = true; Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        Assert.True(firstPreview.IsEffectivelyVisible); Assert.True(Find<Button>(window, "SavePreferences").IsEnabled);
        Click(window, "SavePreferences"); Dispatcher.UIThread.RunJobs();
        Assert.True(scope.Runtime.Settings.DebugToolsEnabled);
        Assert.True(AppSettings.Load(Path.Combine(scope.Root, "settings.json")).DebugToolsEnabled);

        var remaining = scope.Runtime.Clock.Remaining;
        var history = scope.Runtime.BreakHistory.Completions.Count;
        var dueSounds = scope.Runtime.DueSoundRequests;
        var completionSounds = scope.Runtime.CompletionSoundRequests;
        foreach (var item in new[]
        {
            ("DebugPreviewAdvance", PetNotice.Advance),
            ("DebugPreviewInvitation", PetNotice.Invitation),
            ("DebugPreviewResting", PetNotice.Resting),
            ("DebugPreviewCompleted", PetNotice.Completed)
        })
        {
            Click(window, item.Item1); Dispatcher.UIThread.RunJobs();
            Assert.Equal(item.Item2, scope.Runtime.PreviewNotice);
            Assert.False(scope.Runtime.Reminder.HasNotice);
            Assert.Equal(remaining, scope.Runtime.Clock.Remaining);
            Assert.Equal(history, scope.Runtime.BreakHistory.Completions.Count);
            Assert.Equal(dueSounds, scope.Runtime.DueSoundRequests);
            Assert.Equal(completionSounds, scope.Runtime.CompletionSoundRequests);
        }
        Click(window, "DebugPreviewClose"); Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Runtime.PreviewNotice);
        Assert.Equal("미리보기 대기 중", Find<TextBlock>(window, "DebugPreviewStatus").Text);
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
