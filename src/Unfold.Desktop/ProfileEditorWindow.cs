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
        Title = "Work profile · Unfold"; Width = 500; Height = 520; CanResize = false;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var id = existing?.Id ?? "profile-" + Guid.NewGuid().ToString("N");
        var name = new TextBox { Name = "ProfileName", Text = existing?.Name ?? "Focused work", MaxLength = 60 };
        var interval = new NumericUpDown { Name = "ProfileInterval", Minimum = 5, Maximum = 240, Increment = 5,
            Value = existing?.IntervalMinutes ?? settings.IntervalMinutes, FormatString = "0" };
        var idle = new NumericUpDown { Name = "ProfileIdle", Minimum = 1, Maximum = 60, Increment = 1,
            Value = existing?.IdleMinutes ?? settings.IdleMinutes, FormatString = "0" };
        var routines = BreakRoutines.ForSettings(settings);
        var routine = new ComboBox { Name = "ProfileRoutine", ItemsSource = routines, HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = routines.FirstOrDefault(item => item.Id == (existing?.RoutineId ?? settings.BreakRoutineId)) ?? routines[0] };
        AutomationProperties.SetName(name, "Profile name"); AutomationProperties.SetName(interval, "Reminder interval in minutes");
        AutomationProperties.SetName(idle, "Away threshold in minutes"); AutomationProperties.SetName(routine, "Profile routine");
        var error = new TextBlock { Name = "ProfileError", Foreground = Brushes.LightSalmon, TextWrapping = TextWrapping.Wrap };
        var cancel = Ui.Button("Cancel", Close); cancel.IsCancel = true;
        var saveButton = Ui.AsyncButton("Save profile", async () =>
        {
            try
            {
                var profile = new WorkProfile(id, name.Text?.Trim() ?? "", (int)(interval.Value ?? 60), (int)(idle.Value ?? 5),
                    (routine.SelectedItem as BreakRoutine)?.Id ?? BreakRoutines.DefaultId);
                profile.Validate(); await save(profile); Close();
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            { error.Text = exception is ArgumentException ? exception.Message : "Could not save your profile. Please try again."; }
        });
        saveButton.IsDefault = true;
        var help = Ui.Text("Save your setup, then apply it when your work changes.", 13); help.TextWrapping = TextWrapping.Wrap;
        var actions = Ui.Row(cancel, saveButton); actions.HorizontalAlignment = HorizontalAlignment.Right;
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(24), Child = Ui.Column(
            Ui.Text("A rhythm for your work.", 24, Ui.Accent), help, Ui.Text("NAME", 12), name,
            Ui.Text("Remind me every (min)", 12), interval, Ui.Text("Pause when away (min)", 12), idle,
            Ui.Text("BREAK ROUTINE", 12), routine, error, actions) } };
        Opened += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}
