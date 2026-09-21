using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System.Globalization;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow : Window, IDisposable
{
    private readonly AppRuntime runtime;
    private readonly TextBlock countdown = Ui.Text("60:00", 52, Ui.Accent), state = Ui.Text("진행 준비", 13);
    private readonly Avalonia.Controls.Shapes.Ellipse timerStateDot = new() { Name = "TimerStateIndicator", Width = 8, Height = 8, Fill = DesignSystem.Muted };
    private readonly Border timerStateBadge = new() { Name = "TimerStateBadge", Background = DesignSystem.Raised, CornerRadius = new(12), Padding = new(10, 6) };
    private readonly ComboBox characters = new() { Name = "CharacterPicker", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock today = new() { FontSize = 16, IsVisible = false };
    private readonly TextBlock historyStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap,
        Foreground = DesignSystem.Error, IsVisible = false };
    private readonly AnimationView preview = new() { Name = "CompanionPreview", Width = DesignSystem.PetBaseSize,
        Height = DesignSystem.PetBaseSize, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider petScale = new() { Name = "PetScale", Minimum = 50, Maximum = 150,
        TickFrequency = 10, IsSnapToTickEnabled = true, Value = 100, Classes = { "thumb-hover-slider" } };
    private readonly TextBlock petScaleValue = new() { Name = "PetScaleValue", FontSize = DesignSystem.Body,
        Foreground = DesignSystem.Cream, Text = "100%" };
    private Grid? companionPreviewStage;
    private readonly TimerControls timerControls;
    private readonly CheckBox showPet, launchAtLogin;
    private readonly NumericUpDown interval, breakDuration, idle, snooze;
    private readonly Button homeTimingApply;
    private readonly TextBlock homeTimingStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap,
        Foreground = DesignSystem.Muted, IsVisible = false };
    private int displayedInterval, displayedBreakDuration;
    private bool? intervalEditingAvailable;
    private CharacterPackage? previewCharacter;
    private bool updating;
    private bool savingHomeTiming;
    public SettingsWindow(AppRuntime runtime)
    {
        this.runtime = runtime; Title = "Unfold · 휴식 알림"; Width = 1120; Height = 800; MinWidth = 640; MinHeight = 560;
        Background = Ui.Background;
        interval = new NumericUpDown { Name = "ReminderInterval", Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        breakDuration = new NumericUpDown { Name = "BreakDurationMinutes", Minimum = 1, Maximum = 10, Value = runtime.Settings.BreakDurationMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        idle = new NumericUpDown { Name = "ReminderIdle", Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        snooze = new NumericUpDown { Name = "SnoozeMinutes", Minimum = 1, Maximum = 60, Value = runtime.Settings.SnoozeMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        displayedInterval = runtime.Settings.IntervalMinutes;
        displayedBreakDuration = runtime.Settings.BreakDurationMinutes;
        timerControls = new(runtime.TogglePause, () => _ = runtime.RequestStop());
        AutomationProperties.SetName(interval, "스트레칭 알림 간격, 분 단위");
        AutomationProperties.SetName(breakDuration, "휴식 시간, 분 단위");
        AutomationProperties.SetName(idle, "자리 비움 시간, 분 단위");
        AutomationProperties.SetName(snooze, "다시 알림 시간, 분 단위");
        AutomationProperties.SetName(characters, "함께할 펫");
        AutomationProperties.SetName(petScale, "펫 크기, 퍼센트");
        countdown.Name = "TimerCountdown";
        state.Name = "TimerStateText";
        state.TextWrapping = TextWrapping.Wrap;
        homeTimingApply = Ui.Action("저장");
        homeTimingApply.Click += async (_, _) =>
        {
            if (savingHomeTiming || !homeTimingApply.IsEnabled ||
                !TryReadHomeMinutes(interval, 5, 240, out var minutes) ||
                !TryReadHomeMinutes(breakDuration, 1, 10, out var rest)) return;
            await SaveHomeTimingSettings(minutes, rest);
        };
        homeTimingApply.Name = "ApplyHomeTimingSettings";
        AutomationProperties.SetName(homeTimingApply, "스트레칭과 휴식 시간 저장");
        ToolTip.SetTip(homeTimingApply, "변경한 스트레칭과 휴식 시간 저장");
        showPet = new CheckBox { Name = "ShowPetOnDesktop", Content = "바탕화면에 펫 표시", IsChecked = runtime.Settings.ShowPet };
        showPet.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            try { await runtime.UpdateSettings(runtime.Settings with { ShowPet = showPet.IsChecked == true }); }
            catch (Exception error) { updating = true; showPet.IsChecked = runtime.Settings.ShowPet; updating = false; await Ui.Error(this, error); }
        };
        launchAtLogin = new CheckBox { Name = "LaunchAtLogin", Content = "로그인 시 자동 실행" };
        try { launchAtLogin.IsChecked = PlatformServices.StartsAtLogin(); } catch (Exception error) { AppPaths.Log(error); }
        launchAtLogin.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            var attempted = launchAtLogin.IsChecked == true;
            try { PlatformServices.SetStartAtLogin(attempted); }
            catch (Exception error)
            {
                // A failed toggle may have failed before touching the OS state (e.g. an
                // unpublished dev build) or partway through it; re-query reality instead of
                // assuming the attempted direction took effect either way. If even that
                // query fails, fall back to the state before this attempt rather than
                // leaving the checkbox showing the unconfirmed, possibly-wrong new value.
                updating = true;
                try { launchAtLogin.IsChecked = PlatformServices.StartsAtLogin(); }
                catch (Exception queryError) { AppPaths.Log(queryError); launchAtLogin.IsChecked = !attempted; }
                updating = false;
                await Ui.Error(this, error);
            }
        };
        characters.SelectionChanged += async (_, _) =>
        {
            if (updating || characters.SelectedItem is not CharacterPackage selected) return;
            try { await runtime.UpdateSettings(runtime.Settings with { SelectedCharacterId = selected.Manifest.Id }); }
            catch (Exception error) { updating = true; characters.SelectedItem = runtime.Selected; updating = false; await Ui.Error(this, error); }
        };
        petScale.PropertyChanged += async (_, e) =>
        {
            if (e.Property != RangeBase.ValueProperty || updating) return;
            var percent = Math.Clamp((int)Math.Round(petScale.Value / 10) * 10, 50, 150);
            SetPetScalePreview(percent);
            if (percent == runtime.Settings.PetScalePercent) return;
            try { await runtime.UpdateSettings(runtime.Settings with { PetScalePercent = percent }); }
            catch (Exception error)
            {
                updating = true; petScale.Value = runtime.Settings.PetScalePercent;
                SetPetScalePreview(runtime.Settings.PetScalePercent); updating = false;
                await Ui.Error(this, error);
            }
        };
        homeTimingStatus.Name = "HomeTimingStatus";
        Content = BuildDashboard();
        foreach (var input in new[] { interval, breakDuration })
            input.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumericUpDown.ValueProperty || e.Property == NumericUpDown.TextProperty) TimingEdited();
            };
        foreach (var input in new[] { idle, snooze })
            input.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumericUpDown.ValueProperty || e.Property == NumericUpDown.TextProperty) PreferencesEdited();
            };
        Closing += (_, e) => { e.Cancel = true; HideToTray(); };
        Opened += (_, _) => preview.SetRunning(true);
        runtime.Changed += Refresh; Closed += (_, _) => Dispose();
        Refresh();
    }
    public void Dispose() { runtime.Changed -= Refresh; runtime.CloseReminderPreview(); stopSoundPreview?.Invoke(); settingsSoundPlayer.Dispose(); preview.Dispose(); petPage?.Dispose(); }
    public void HideToTray() { runtime.CloseReminderPreview(); stopSoundPreview?.Invoke(); Hide(); preview.SetRunning(false); }
    public void ResumePreview() => preview.SetRunning(true);
    private void TimingEdited()
    {
        if (updating) return;
        homeTimingStatus.Text = ""; homeTimingStatus.IsVisible = false;
        RefreshHomeTimingState();
    }
    private static bool TryReadHomeMinutes(NumericUpDown input, int minimum, int maximum, out int minutes)
    {
        minutes = 0;
        if (input.Value is not decimal value || value < minimum || value > maximum || decimal.Truncate(value) != value ||
            !decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var text) || text != value) return false;
        minutes = (int)value; return true;
    }
    private void RefreshHomeTimingState()
    {
        var validInterval = TryReadHomeMinutes(interval, 5, 240, out var minutes);
        var validRest = TryReadHomeMinutes(breakDuration, 1, 10, out var rest);
        var changed = !validInterval || !validRest || minutes != runtime.Settings.IntervalMinutes || rest != runtime.Settings.BreakDurationMinutes;
        homeTimingApply.Opacity = changed ? 1 : 0;
        homeTimingApply.IsHitTestVisible = changed;
        homeTimingApply.IsEnabled = changed && validInterval && validRest && !savingHomeTiming;
    }
    private void SetPetScalePreview(int percent)
    {
        var actualSize = DesignSystem.PetBaseSize * percent / 100d;
        petScaleValue.Text = $"{percent}%";
        preview.Width = preview.Height = actualSize;
        if (companionPreviewStage is not null)
            companionPreviewStage.Width = companionPreviewStage.Height = actualSize;
    }
    internal async Task<bool> CanCloseDraft()
    {
        if (petPage is null) return true;
        if (petPage.IsBusy)
        {
            Show(); Activate();
            await Ui.Confirm(this, "파일 작업 중이에요", "파일을 확인하거나 저장하고 있어요. 작업이 끝난 뒤 종료해 주세요.", "확인");
            return false;
        }
        if (!petPage.HasUnsavedDraft) return true;
        Show(); Activate(); await OpenPetPacks();
        return await petPage.CanCloseDraft();
    }
    private async Task SaveHomeTimingSettings(int minutes, int rest)
    {
        savingHomeTiming = true; RefreshHomeTimingState();
        try
        {
            var updated = runtime.Settings with { IntervalMinutes = minutes, BreakDurationMinutes = rest,
                ActiveProfileId = minutes == runtime.Settings.IntervalMinutes ? runtime.Settings.ActiveProfileId : null };
            await runtime.UpdateSettings(updated);
            homeTimingStatus.Foreground = DesignSystem.Muted; homeTimingStatus.Text = "저장했어요.";
            homeTimingStatus.IsVisible = true;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { AppPaths.Log(error); homeTimingStatus.Foreground = DesignSystem.Error; homeTimingStatus.Text = "저장하지 못했어요."; homeTimingStatus.IsVisible = true; }
        finally { savingHomeTiming = false; RefreshHomeTimingState(); }
    }
    private async void Refresh()
    {
        if (updating) return; updating = true;
        CharacterPackage? loadPreview = null;
        try
        {
            countdown.Text = $"{(int)runtime.Clock.Remaining.TotalMinutes:00}:{runtime.Clock.Remaining.Seconds:00}";
            var stateBrush = DesignSystem.Success;
            state.Text = runtime.ActivityError is not null ? "상태 확인 필요" :
                runtime.Reminder.Notice == PetNotice.Invitation ? "휴식 대기 중" :
                runtime.Reminder.Notice == PetNotice.Resting ? "휴식 중" :
                runtime.Clock.Stopped ? "중지됨" : runtime.Clock.Paused ? "일시정지" :
                runtime.Clock.IdlePaused ? "자리 비움" : "진행 중";
            ToolTip.SetTip(timerStateBadge, runtime.ActivityError);
            if (runtime.ActivityError is not null) stateBrush = DesignSystem.Error;
            else if (runtime.Clock.Stopped) stateBrush = DesignSystem.Stopped;
            else if (runtime.ActiveReminder is not null || runtime.Clock.Paused || runtime.Clock.IdlePaused) stateBrush = DesignSystem.Warning;
            state.Foreground = stateBrush; timerStateDot.Fill = stateBrush;
            var summary = runtime.BreakHistory.ForDay(DateTimeOffset.Now);
            today.Text = summary.Count == 0 ? "" : $"{summary.Seconds / 60}분 {summary.Seconds % 60}초";
            today.IsVisible = summary.Count > 0;
            today.TextWrapping = TextWrapping.Wrap;
            companionName.Text = runtime.Selected?.Manifest.Name ?? "함께할 펫을 선택해 주세요";
            todayCount.Text = summary.Count.ToString();
            intervalHint.Text = $"{runtime.Settings.IntervalMinutes}분 간격";
            historyStatus.Text = runtime.BreakHistoryError ?? "";
            historyStatus.IsVisible = runtime.BreakHistoryError is not null;
            timerControls.Refresh(runtime.Clock);
            var canEditInterval = runtime.CanEditTimerInterval;
            interval.IsEnabled = canEditInterval;
            var discardedInterval = !canEditInterval &&
                (!TryReadHomeMinutes(interval, 5, 240, out var pendingInterval) || pendingInterval != runtime.Settings.IntervalMinutes);
            if (discardedInterval)
            {
                interval.Value = runtime.Settings.IntervalMinutes;
                interval.Text = runtime.Settings.IntervalMinutes.ToString(CultureInfo.CurrentCulture);
            }
            if (intervalEditingAvailable != canEditInterval)
            {
                intervalEditingAvailable = canEditInterval;
                homeTimingStatus.Text = ""; homeTimingStatus.IsVisible = false;
                ToolTip.SetTip(interval, canEditInterval ? "1분 단위로 스트레칭 시간을 변경할 수 있어요." : "타이머 진행 중에는 스트레칭 시간을 변경할 수 없어요.");
            }
            if (discardedInterval)
            {
                homeTimingStatus.Foreground = DesignSystem.Warning;
                homeTimingStatus.Text = "저장하지 않은 변경을 되돌렸어요.";
                homeTimingStatus.IsVisible = true;
            }
            showPet.IsChecked = runtime.Settings.ShowPet;
            if ((int)petScale.Value != runtime.Settings.PetScalePercent) petScale.Value = runtime.Settings.PetScalePercent;
            SetPetScalePreview(runtime.Settings.PetScalePercent);
            if (displayedInterval != runtime.Settings.IntervalMinutes) interval.Value = displayedInterval = runtime.Settings.IntervalMinutes;
            if (displayedBreakDuration != runtime.Settings.BreakDurationMinutes) breakDuration.Value = displayedBreakDuration = runtime.Settings.BreakDurationMinutes;
            RefreshHomeTimingState();
            SyncPreferencesFromRuntime(); RefreshDebugPreviewStatus();
            if (!ReferenceEquals(characters.ItemsSource, runtime.Characters)) characters.ItemsSource = runtime.Characters;
            characters.SelectedItem = runtime.Selected;
            if (runtime.Selected is { } selected && previewCharacter != selected)
            {
                previewCharacter = selected; loadPreview = selected;
            }
        }
        catch (Exception error)
        {
            AppPaths.Log(error); state.Text = "상태 확인 필요";
            state.Foreground = timerStateDot.Fill = DesignSystem.Error;
            ToolTip.SetTip(timerStateBadge, Ui.ErrorText(error));
        }
        finally { updating = false; }
        if (loadPreview is null) return;
        try
        {
            var frames = await runtime.Clip("idle");
            if (runtime.Selected == loadPreview && previewCharacter == loadPreview)
                preview.SetFrames(frames, true, loadPreview.Manifest.RenderStyle == "pixel");
            if (!IsVisible) preview.SetRunning(false);
        }
        catch (Exception error) { AppPaths.Log(error); }
    }
}
