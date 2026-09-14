using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly TextBlock countdown = Ui.Text("60:00", 52, Ui.Accent), state = Ui.Text("준비", 13);
    private readonly ComboBox characters = new() { Name = "CharacterPicker", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox routines = new() { Name = "RoutinePicker", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock today = Ui.Text("오늘은 아직 휴식 기록이 없어요", 16);
    private readonly TextBlock historyStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DesignSystem.Muted };
    private readonly AnimationView preview = new() { Name = "CompanionPreview", Width = 240, Height = 240 };
    private readonly TimerControls timerControls;
    private readonly CheckBox showPet;
    private readonly TextBlock activeProfile = Ui.Text("직접 설정한 알림", 12, Ui.Accent);
    private readonly NumericUpDown interval, idle;
    private readonly TextBlock reminderSettingsStatus = new() { Text = "알림 간격은 변경하면 자동으로 저장돼요.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = DesignSystem.Muted };
    private int displayedInterval, displayedIdle;
    private string displayedRoutine;
    private CharacterPackage? previewCharacter;
    private bool updating;
    public SettingsWindow(AppRuntime runtime)
    {
        this.runtime = runtime; Title = "Unfold · 휴식 알림"; Width = 1120; Height = 800; MinWidth = 860; MinHeight = 680;
        Background = Ui.Background;
        interval = new NumericUpDown { Name = "ReminderInterval", Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 5, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        idle = new NumericUpDown { Name = "ReminderIdle", Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, FormatString = "0" };
        displayedInterval = runtime.Settings.IntervalMinutes; displayedIdle = runtime.Settings.IdleMinutes; displayedRoutine = runtime.Settings.BreakRoutineId;
        routines.ItemsSource = runtime.Routines;
        routines.SelectedItem = runtime.Routines.FirstOrDefault(item => item.Id == runtime.Settings.BreakRoutineId);
        timerControls = new(runtime.TogglePause, runtime.Stop, runtime.Reset);
        AutomationProperties.SetName(interval, "휴식 알림 간격, 분 단위");
        AutomationProperties.SetName(idle, "자리 비움 시 일시정지 기준, 분 단위");
        AutomationProperties.SetName(routines, "휴식 루틴");
        AutomationProperties.SetName(characters, "함께할 펫");
        state.TextWrapping = TextWrapping.Wrap;
        interval.ValueChanged += async (_, _) =>
        {
            if (updating || interval.Value is not decimal minutes || minutes is < 5 or > 240 || decimal.Truncate(minutes) != minutes ||
                minutes == runtime.Settings.IntervalMinutes) return;
            await SaveReminderSettings((int)minutes, runtime.Settings.IdleMinutes, runtime.Settings.BreakRoutineId);
        };
        var apply = Ui.AsyncButton("알림 설정 적용", async () =>
        {
            if (interval.Value is not decimal minutes || minutes is < 5 or > 240 || decimal.Truncate(minutes) != minutes ||
                idle.Value is not decimal away || away is < 1 or > 60 || decimal.Truncate(away) != away)
            { reminderSettingsStatus.Text = "분 단위의 정수를 입력해 주세요. 알림 간격은 5~240분, 자리 비움 기준은 1~60분이에요."; reminderSettingsStatus.Foreground = DesignSystem.Error; return; }
            await SaveReminderSettings((int)minutes, (int)away, (routines.SelectedItem as BreakRoutine)?.Id ?? BreakRoutines.DefaultId);
        });
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
        Content = BuildDashboard(apply, login);
        Closing += (_, e) => { e.Cancel = true; HideToTray(); };
        Opened += (_, _) => preview.SetRunning(true);
        runtime.Changed += Refresh; Closed += (_, _) => { runtime.Changed -= Refresh; preview.Dispose(); };
        Refresh();
    }
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
            state.Text = runtime.ActivityError ?? (runtime.ActiveReminder is not null ? "휴식 중 · 작업 타이머 대기" :
                runtime.Clock.Stopped ? "정지됨 · 시작 버튼을 눌러 주세요" : runtime.Clock.Paused ? "일시정지 · 계속 버튼을 눌러 주세요" : runtime.Clock.IdlePaused ? "자리 비움으로 일시정지" : "작업 시간을 세고 있어요");
            var summary = runtime.BreakHistory.ForDay(DateTimeOffset.Now);
            today.Text = summary.Count == 0 ? "첫 휴식은 언제든 괜찮아요." :
                $"오늘 {summary.Seconds / 60}분 {summary.Seconds % 60}초의 여유를 만들었어요.";
            today.TextWrapping = TextWrapping.Wrap;
            companionName.Text = runtime.Selected?.Manifest.Name ?? "함께할 펫을 선택해 주세요";
            todayCount.Text = summary.Count.ToString();
            intervalHint.Text = $"{runtime.Settings.IntervalMinutes}분 간격";
            historyStatus.Text = runtime.BreakHistoryError ?? "완료한 휴식은 이 기기에만 저장돼요.";
            timerControls.Refresh(runtime.Clock);
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
