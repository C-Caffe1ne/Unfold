using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class ProfileEditorWindow : Window
{
    public ProfileEditorWindow(AppSettings settings, WorkProfile? existing, Func<WorkProfile, Task> save)
    {
        Title = "업무 프로필 · Unfold"; Width = 500; Height = 520; CanResize = false;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var id = existing?.Id ?? "profile-" + Guid.NewGuid().ToString("N");
        var name = new TextBox { Name = "ProfileName", Text = existing?.Name ?? "집중하는 시간", MaxLength = 60 };
        var interval = new NumericUpDown { Name = "ProfileInterval", Minimum = 5, Maximum = 240, Increment = 1,
            Value = existing?.IntervalMinutes ?? settings.IntervalMinutes, FormatString = "0" };
        var idle = new NumericUpDown { Name = "ProfileIdle", Minimum = 1, Maximum = 60, Increment = 1,
            Value = existing?.IdleMinutes ?? settings.IdleMinutes, FormatString = "0" };
        var routines = BreakRoutines.ForSettings(settings);
        var routine = new ComboBox { Name = "ProfileRoutine", ItemsSource = routines, HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = routines.FirstOrDefault(item => item.Id == (existing?.RoutineId ?? settings.BreakRoutineId)) ?? routines[0] };
        AutomationProperties.SetName(name, "프로필 이름"); AutomationProperties.SetName(interval, "스트레칭 시간, 분 단위");
        AutomationProperties.SetName(idle, "자리 비움 기준, 분 단위"); AutomationProperties.SetName(routine, "프로필의 휴식 루틴");
        var error = new TextBlock { Name = "ProfileError", IsVisible = false, Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
        var cancel = Ui.Button("취소", Close); cancel.IsCancel = true;
        var saveButton = Ui.AsyncButton("프로필 저장", async () =>
        {
            try
            {
                var profile = new WorkProfile(id, name.Text?.Trim() ?? "", (int)(interval.Value ?? 60), (int)(idle.Value ?? 5),
                    (routine.SelectedItem as BreakRoutine)?.Id ?? BreakRoutines.DefaultId);
                profile.Validate(); await save(profile); Close();
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            { error.IsVisible = true; error.Text = exception is ArgumentException ? exception.Message : "프로필을 저장하지 못했어요. 다시 시도해 주세요."; }
        });
        saveButton.IsDefault = true; Ui.Primary(saveButton); Ui.Quiet(cancel);
        var timing = new Grid { ColumnDefinitions = new("*,12,*") };
        timing.Children.Add(Ui.Field("스트레칭 시간 (분)", interval));
        var away = Ui.Field("자리 비움 기준 (분)", idle); Grid.SetColumn(away, 2); timing.Children.Add(away);
        Content = Ui.Page(this, "나의 작업에 맞는 리듬.", "자주 쓰는 설정을 저장하고, 작업에 맞춰 적용해 보세요.",
            Ui.Card(Ui.Column(Ui.Field("프로필 이름", name), timing, Ui.Field("휴식 루틴", routine))),
            Ui.Column(error, Ui.Actions(cancel, saveButton)), "업무 프로필");
        Opened += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
