using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class PersonalizationWindow : Window
{
    private readonly Func<AppSettings> settings;
    private readonly Func<AppSettings, Task> save;
    private readonly ListBox routines = new() { Name = "RoutineLibrary", Height = 230 };
    private readonly ListBox profiles = new() { Name = "WorkProfiles", Height = 230 };
    private readonly TextBlock status = new() { Name = "LibraryStatus", IsVisible = false, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock detail = new() { Name = "ProfileDetail", TextWrapping = TextWrapping.Wrap };
    private readonly Button editRoutine, deleteRoutine, useRoutine, editProfile, deleteProfile, applyProfile;
    public PersonalizationWindow(Func<AppSettings> getSettings, Func<AppSettings, Task> saveSettings)
    {
        settings = getSettings; save = saveSettings;
        Title = "내 루틴 · 업무 프로필 · Unfold"; Width = 640; Height = 620; MinWidth = 600; MinHeight = 580;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetName(routines, "루틴 목록"); AutomationProperties.SetName(profiles, "업무 프로필");
        editRoutine = Action("루틴 편집", () => EditRoutine(routines.SelectedItem as BreakRoutine));
        useRoutine = Action("루틴 사용", async () =>
        {
            if (routines.SelectedItem is BreakRoutine selected)
                await Save(settings().ApplyReminder(settings().IntervalMinutes, settings().IdleMinutes, selected.Id), "다음 휴식에 사용할 루틴을 선택했어요.");
        });
        deleteRoutine = Action("루틴 삭제", async () =>
        {
            if (routines.SelectedItem is not BreakRoutine selected) return;
            settings().RemoveRoutine(selected.Id);
            if (await Ui.Confirm(this, "루틴을 삭제할까요?", $"‘{selected.Name}’ 루틴을 삭제할까요? 완료 기록과 진행 중인 휴식은 유지돼요.", "삭제", "취소") == 0)
                await Save(settings().RemoveRoutine(selected.Id), "루틴을 삭제했어요.");
        });
        editProfile = Action("프로필 편집", () => EditProfile(profiles.SelectedItem as WorkProfile));
        applyProfile = Action("프로필 적용", async () =>
        {
            if (profiles.SelectedItem is WorkProfile selected) await Save(settings().ApplyProfile(selected.Id), $"‘{selected.Name}’ 프로필을 적용했어요. 진행 중인 휴식은 기존 순서대로 이어져요.");
        });
        deleteProfile = Action("프로필 삭제", async () =>
        {
            if (profiles.SelectedItem is not WorkProfile selected) return;
            if (await Ui.Confirm(this, "프로필을 삭제할까요?", $"‘{selected.Name}’ 프로필을 삭제할까요? 현재 알림 설정과 기록은 유지돼요.", "삭제", "취소") == 0)
                await Save(settings().RemoveProfile(selected.Id), "프로필을 삭제했어요.");
        });
        Ui.Primary(useRoutine); Ui.Primary(applyProfile); Ui.Danger(deleteRoutine); Ui.Danger(deleteProfile);
        routines.SelectionChanged += (_, _) => RefreshActions(); profiles.SelectionChanged += (_, _) => RefreshActions();
        var routineHelp = Ui.Caption("작업에 맞는 휴식을 만들어 두세요. 기본 루틴도 언제든 사용할 수 있어요."); routineHelp.TextWrapping = TextWrapping.Wrap;
        var profileHelp = Ui.Caption("루틴과 알림 간격, 자리 비움 기준을 프로필에 저장해 두고 필요할 때 직접 적용하세요."); profileHelp.TextWrapping = TextWrapping.Wrap;
        var routineActions = Ui.Actions(Action("새 루틴", () => EditRoutine(null)), editRoutine, useRoutine, deleteRoutine);
        var routineTab = Ui.Column(routineHelp, routines);
        var profileActions = Ui.Actions(Action("새 프로필", () => EditProfile(null)), editProfile, applyProfile, deleteProfile);
        var profileTab = Ui.Column(profileHelp, profiles, detail);
        var tabs = new TabControl { Name = "PersonalizationTabs", ItemsSource = new[]
        {
            new TabItem { Header = "내 루틴", Content = routineTab }, new TabItem { Header = "업무 프로필", Content = profileTab }
        } };
        var actionHost = new ContentControl { Content = routineActions };
        tabs.SelectionChanged += (_, _) => actionHost.Content = tabs.SelectedIndex == 1 ? profileActions : routineActions;
        var close = Ui.Button("닫기", Close); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right;
        Content = Ui.Page(this, "나만의 방식으로 쉬어 가세요.", "루틴을 만들고 작업에 맞는 프로필을 골라 보세요.",
            tabs, Ui.Column(status, actionHost, Ui.Actions(Ui.Quiet(close))), "루틴 · 프로필");
        Refresh();
    }
    private Button Action(string label, Func<Task> action)
    {
        var button = Ui.Action(label);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
            { status.IsVisible = true; status.Foreground = DesignSystem.Error; status.Text = error is ArgumentException ? error.Message : "변경 사항을 저장하지 못했어요. 다시 시도해 주세요."; }
            finally { button.IsEnabled = true; RefreshActions(); }
        };
        return button;
    }
    private async Task Save(AppSettings value, string message)
    {
        await save(value); Refresh(); status.IsVisible = true; status.Foreground = DesignSystem.Muted; status.Text = message;
    }
    private async Task EditRoutine(BreakRoutine? existing)
    {
        var current = settings();
        if (existing is null && current.CustomRoutine is not null && current.AdditionalRoutines.Count >= AppSettings.MaxAdditionalRoutines)
            throw new ArgumentException("내 루틴 20개를 모두 사용 중이에요. 기존 루틴을 편집하거나 삭제해 주세요.");
        var id = existing?.Id ?? (current.CustomRoutine is null ? BreakRoutines.CustomId : "routine-" + Guid.NewGuid().ToString("N"));
        var editor = new RoutineEditorWindow(existing, routine => Save(settings().SaveRoutine(routine), "루틴을 저장했어요. 다음 휴식부터 사용해요."), id);
        await editor.ShowDialog(this);
    }
    private async Task EditProfile(WorkProfile? existing)
    {
        if (existing is null && settings().WorkProfiles.Count >= AppSettings.MaxWorkProfiles)
            throw new ArgumentException("프로필 10개를 모두 사용 중이에요. 기존 프로필을 편집하거나 삭제해 주세요.");
        var editor = new ProfileEditorWindow(settings(), existing, profile => Save(settings().SaveProfile(profile), "프로필을 저장했어요. ‘프로필 적용’을 누르면 사용할 수 있어요."));
        await editor.ShowDialog(this);
    }
    private void Refresh()
    {
        var current = settings(); var selectedProfile = (profiles.SelectedItem as WorkProfile)?.Id;
        routines.ItemsSource = BreakRoutines.ForSettings(current);
        routines.SelectedItem = ((IReadOnlyList<BreakRoutine>)routines.ItemsSource).FirstOrDefault(item => item.Id == current.BreakRoutineId);
        profiles.ItemsSource = current.WorkProfiles;
        profiles.SelectedItem = current.WorkProfiles.FirstOrDefault(item => item.Id == selectedProfile) ?? current.WorkProfiles.FirstOrDefault();
        RefreshActions();
    }
    private void RefreshActions()
    {
        var selected = routines.SelectedItem as BreakRoutine;
        editRoutine.IsEnabled = deleteRoutine.IsEnabled = selected is not null && BreakRoutines.Find(selected.Id) is null;
        useRoutine.IsEnabled = selected is not null;
        var profile = profiles.SelectedItem as WorkProfile;
        editProfile.IsEnabled = deleteProfile.IsEnabled = applyProfile.IsEnabled = profile is not null;
        detail.Text = profile is null ? "아직 프로필이 없어요. 자주 쓰는 설정을 저장해 보세요." :
            $"{BreakRoutines.ForSettings(settings()).FirstOrDefault(item => item.Id == profile.RoutineId)?.Name} · {profile.IdleMinutes}분 자리 비움 시 일시정지";
    }
}
