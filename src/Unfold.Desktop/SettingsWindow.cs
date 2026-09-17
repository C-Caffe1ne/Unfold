using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow : Window, IDisposable
{
    private readonly AppRuntime runtime;
    private readonly TextBlock countdown = Ui.Text("60:00", 52, Ui.Accent), state = Ui.Text("진행 준비", 13);
    private readonly Avalonia.Controls.Shapes.Ellipse timerStateDot = new() { Name = "TimerStateIndicator", Width = 8, Height = 8, Fill = DesignSystem.Muted };
    private readonly Border timerStateBadge = new() { Name = "TimerStateBadge", Background = DesignSystem.Raised, CornerRadius = new(12), Padding = new(10, 6) };
    private readonly ComboBox characters = new() { Name = "CharacterPicker", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox routines = new() { Name = "RoutinePicker", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock today = Ui.Text("오늘은 아직 휴식 기록이 없어요", 16);
    private readonly TextBlock historyStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DesignSystem.Muted };
    private readonly AnimationView preview = new() { Name = "CompanionPreview", Width = 240, Height = 240 };
    private readonly TimerControls timerControls;
    private readonly CheckBox showPet;
    private readonly TextBlock activeProfile = Ui.Text("직접 설정한 알림", 12, Ui.Accent);
    private readonly NumericUpDown interval, idle;
    private readonly Button reminderApply;
    private readonly TextBlock reminderSettingsStatus = new() { Text = "타이머를 일시정지하거나 중지하면 시간을 바꿀 수 있어요.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DesignSystem.Muted };
    private int displayedInterval, displayedIdle;
    private string displayedRoutine;
    private bool? intervalEditingAvailable;
    private CharacterPackage? previewCharacter;
    private bool updating;
    public SettingsWindow(AppRuntime runtime)
    {
        this.runtime = runtime; Title = "Unfold · 휴식 알림"; Width = 1120; Height = 800; MinWidth = 860; MinHeight = 680;
        Background = Ui.Background;
        interval = new NumericUpDown { Name = "ReminderInterval", Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        idle = new NumericUpDown { Name = "ReminderIdle", Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        displayedInterval = runtime.Settings.IntervalMinutes; displayedIdle = runtime.Settings.IdleMinutes; displayedRoutine = runtime.Settings.BreakRoutineId;
        routines.ItemsSource = runtime.Routines;
        routines.SelectedItem = runtime.Routines.FirstOrDefault(item => item.Id == runtime.Settings.BreakRoutineId);
        timerControls = new(runtime.TogglePause, runtime.Stop);
        AutomationProperties.SetName(interval, "휴식 알림 간격, 분 단위");
        AutomationProperties.SetName(idle, "자리 비움 시 일시정지 기준, 분 단위");
        AutomationProperties.SetName(routines, "휴식 루틴");
        AutomationProperties.SetName(characters, "함께할 펫");
        state.Name = "TimerStateText";
        state.TextWrapping = TextWrapping.Wrap;
        reminderApply = Ui.AsyncButton("적용", async () =>
        {
            if (interval.Value is not decimal minutes || minutes is < 5 or > 240 || decimal.Truncate(minutes) != minutes ||
                idle.Value is not decimal away || away is < 1 or > 60 || decimal.Truncate(away) != away)
            { reminderSettingsStatus.Text = "분 단위의 정수를 입력해 주세요. 알림 간격은 5~240분, 자리 비움 기준은 1~60분이에요."; reminderSettingsStatus.Foreground = DesignSystem.Error; return; }
            await SaveReminderSettings((int)minutes, (int)away, (routines.SelectedItem as BreakRoutine)?.Id ?? BreakRoutines.DefaultId);
        });
        reminderApply.Name = "ApplyReminderSettings";
        AutomationProperties.SetName(reminderApply, "알림 설정 적용");
        ToolTip.SetTip(reminderApply, "변경한 알림 설정 적용");
        showPet = new CheckBox { Content = "바탕화면에 펫 표시", IsChecked = runtime.Settings.ShowPet };
        showPet.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            try { await runtime.UpdateSettings(runtime.Settings with { ShowPet = showPet.IsChecked == true }); }
            catch (Exception error) { updating = true; showPet.IsChecked = runtime.Settings.ShowPet; updating = false; await Ui.Error(this, error); }
        };
        var login = new CheckBox { Content = "로그인 시 자동 실행" };
        try { login.IsChecked = PlatformServices.StartsAtLogin(); } catch (Exception error) { AppPaths.Log(error); }
        login.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            var attempted = login.IsChecked == true;
            try { PlatformServices.SetStartAtLogin(attempted); }
            catch (Exception error)
            {
                // A failed toggle may have failed before touching the OS state (e.g. an
                // unpublished dev build) or partway through it; re-query reality instead of
                // assuming the attempted direction took effect either way. If even that
                // query fails, fall back to the state before this attempt rather than
                // leaving the checkbox showing the unconfirmed, possibly-wrong new value.
                updating = true;
                try { login.IsChecked = PlatformServices.StartsAtLogin(); }
                catch (Exception queryError) { AppPaths.Log(queryError); login.IsChecked = !attempted; }
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
        Content = BuildDashboard(login);
        Closing += (_, e) => { e.Cancel = true; HideToTray(); };
        Opened += (_, _) => preview.SetRunning(true);
        runtime.Changed += Refresh; Closed += (_, _) => Dispose();
        Refresh();
    }
    public void Dispose() { runtime.Changed -= Refresh; preview.Dispose(); petPage?.Dispose(); }
    public void HideToTray() { Hide(); preview.SetRunning(false); }
    public void ResumePreview() => preview.SetRunning(true);
    private async Task SaveReminderSettings(int minutes, int away, string routineId)
    {
        try
        {
            await runtime.UpdateSettings(runtime.Settings.ApplyReminder(minutes, away, routineId));
            reminderSettingsStatus.Foreground = DesignSystem.Muted; reminderSettingsStatus.Text = "알림 설정을 저장했어요.";
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { AppPaths.Log(error); reminderSettingsStatus.Foreground = DesignSystem.Error; reminderSettingsStatus.Text = "알림 설정을 저장하지 못했어요. 다시 적용해 주세요."; }
    }
    private async void Refresh()
    {
        if (updating) return; updating = true;
        CharacterPackage? loadPreview = null;
        try
        {
            countdown.Text = $"{(int)runtime.Clock.Remaining.TotalMinutes:00}:{runtime.Clock.Remaining.Seconds:00}";
            var stateBrush = DesignSystem.Cream;
            state.Text = runtime.ActivityError ?? (runtime.ActiveReminder is not null ? "휴식 중 · 타이머 대기" :
                runtime.Clock.Stopped ? "중지됨 · 재생하면 새로 시작" : runtime.Clock.Paused ? "일시정지 · 남은 시간 유지 중" :
                runtime.Clock.IdlePaused ? "자리 비움 · 자동 일시정지" : "진행 중 · 작업 시간 측정 중");
            if (runtime.ActivityError is not null || runtime.Clock.Stopped) stateBrush = DesignSystem.Error;
            else if (runtime.ActiveReminder is not null || runtime.Clock.Paused || runtime.Clock.IdlePaused) stateBrush = DesignSystem.Warning;
            state.Foreground = stateBrush; timerStateDot.Fill = stateBrush;
            var summary = runtime.BreakHistory.ForDay(DateTimeOffset.Now);
            today.Text = summary.Count == 0 ? "첫 휴식은 언제든 괜찮아요." :
                $"오늘 {summary.Seconds / 60}분 {summary.Seconds % 60}초의 여유를 만들었어요.";
            today.TextWrapping = TextWrapping.Wrap;
            companionName.Text = runtime.Selected?.Manifest.Name ?? "함께할 펫을 선택해 주세요";
            todayCount.Text = summary.Count.ToString();
            intervalHint.Text = $"{runtime.Settings.IntervalMinutes}분 간격";
            historyStatus.Text = runtime.BreakHistoryError ?? "완료한 휴식은 이 기기에만 저장돼요.";
            timerControls.Refresh(runtime.Clock);
            var canEditInterval = runtime.CanEditTimerInterval;
            interval.IsEnabled = canEditInterval;
            if (!canEditInterval && interval.Value != runtime.Settings.IntervalMinutes)
                interval.Value = runtime.Settings.IntervalMinutes;
            if (intervalEditingAvailable != canEditInterval)
            {
                intervalEditingAvailable = canEditInterval;
                reminderSettingsStatus.Foreground = DesignSystem.Muted;
                reminderSettingsStatus.Text = canEditInterval
                    ? "알림 시간을 변경한 뒤 우측 상단 적용을 눌러 주세요."
                    : "시간을 바꾸려면 타이머를 일시정지하거나 중지해 주세요.";
                ToolTip.SetTip(interval, canEditInterval ? "1분 단위로 알림 시간을 변경할 수 있어요." : "타이머 진행 중에는 시간을 변경할 수 없어요.");
            }
            // The profile page shares the same timer constraint; refresh availability only
            // so a running/paused transition never discards the visible list selection.
            personalizationPage?.RefreshAvailability();
            showPet.IsChecked = runtime.Settings.ShowPet;
            activeProfile.Text = runtime.Settings.WorkProfiles.FirstOrDefault(profile => profile.Id == runtime.Settings.ActiveProfileId) is { } active
                ? $"업무 프로필 · {active.Name}" : "직접 설정한 알림";
            activeProfile.TextWrapping = TextWrapping.Wrap;
            if (displayedInterval != runtime.Settings.IntervalMinutes) interval.Value = displayedInterval = runtime.Settings.IntervalMinutes;
            if (displayedIdle != runtime.Settings.IdleMinutes) idle.Value = displayedIdle = runtime.Settings.IdleMinutes;
            if (!ReferenceEquals(routines.ItemsSource, runtime.Routines))
            {
                var draft = (routines.SelectedItem as BreakRoutine)?.Id;
                routines.ItemsSource = runtime.Routines;
                var id = displayedRoutine == runtime.Settings.BreakRoutineId ? draft : runtime.Settings.BreakRoutineId;
                routines.SelectedItem = runtime.Routines.FirstOrDefault(item => item.Id == id) ?? runtime.Routines.First(item => item.Id == runtime.Settings.BreakRoutineId);
                displayedRoutine = runtime.Settings.BreakRoutineId;
            }
            RefreshRoutineEditing();
            if (!ReferenceEquals(characters.ItemsSource, runtime.Characters)) characters.ItemsSource = runtime.Characters;
            characters.SelectedItem = runtime.Selected;
            if (runtime.Selected is { } selected && previewCharacter != selected)
            {
                previewCharacter = selected; loadPreview = selected;
            }
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = Ui.ErrorText(error); }
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
