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
    private readonly ListBox routines = new() { Name = "RoutineLibrary", Height = 270 };
    private readonly ListBox profiles = new() { Name = "WorkProfiles", Height = 270 };
    private readonly TextBlock status = new() { Name = "LibraryStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock detail = new() { Name = "ProfileDetail", TextWrapping = TextWrapping.Wrap };
    private readonly Button editRoutine, deleteRoutine, useRoutine, editProfile, deleteProfile, applyProfile;
    public PersonalizationWindow(Func<AppSettings> getSettings, Func<AppSettings, Task> saveSettings)
    {
        settings = getSettings; save = saveSettings;
        Title = "My routines & work profiles · Unfold"; Width = 640; Height = 620; MinWidth = 600; MinHeight = 580;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        AutomationProperties.SetName(routines, "Routine library"); AutomationProperties.SetName(profiles, "Work profiles");
        editRoutine = Action("Edit routine", () => EditRoutine(routines.SelectedItem as BreakRoutine));
        useRoutine = Action("Use routine", async () =>
        {
            if (routines.SelectedItem is BreakRoutine selected)
                await Save(settings().ApplyReminder(settings().IntervalMinutes, settings().IdleMinutes, selected.Id), "Routine selected for the next invitation.");
        });
        deleteRoutine = Action("Delete routine", async () =>
        {
            if (routines.SelectedItem is not BreakRoutine selected) return;
            settings().RemoveRoutine(selected.Id);
            if (await Ui.Confirm(this, "Delete routine?", $"Remove {selected.Name}? Completed history and an open break will stay unchanged.", "Delete", "Cancel") == 0)
                await Save(settings().RemoveRoutine(selected.Id), "Routine removed.");
        });
        editProfile = Action("Edit profile", () => EditProfile(profiles.SelectedItem as WorkProfile));
        applyProfile = Action("Apply profile", async () =>
        {
            if (profiles.SelectedItem is WorkProfile selected) await Save(settings().ApplyProfile(selected.Id), $"Applied {selected.Name}. An open break keeps its original steps.");
        });
        deleteProfile = Action("Delete profile", async () =>
        {
            if (profiles.SelectedItem is not WorkProfile selected) return;
            if (await Ui.Confirm(this, "Delete profile?", $"Remove {selected.Name}? Current reminder settings and history will stay unchanged.", "Delete", "Cancel") == 0)
                await Save(settings().RemoveProfile(selected.Id), "Profile removed.");
        });
        routines.SelectionChanged += (_, _) => RefreshActions(); profiles.SelectionChanged += (_, _) => RefreshActions();
        var routineHelp = Ui.Text("Keep a few pauses for different kinds of work. Built-in routines stay available.", 13); routineHelp.TextWrapping = TextWrapping.Wrap;
        var profileHelp = Ui.Text("A profile combines a routine, reminder interval and away threshold. Apply it manually when you need it.", 13); profileHelp.TextWrapping = TextWrapping.Wrap;
        var routineTab = Ui.Column(routineHelp, routines, Ui.Row(Action("New routine", () => EditRoutine(null)), editRoutine, useRoutine, deleteRoutine));
        var profileTab = Ui.Column(profileHelp, profiles, detail, Ui.Row(Action("New profile", () => EditProfile(null)), editProfile, applyProfile, deleteProfile));
        var tabs = new TabControl { Name = "PersonalizationTabs", ItemsSource = new[]
        {
            new TabItem { Header = "My routines", Content = routineTab }, new TabItem { Header = "Work profiles", Content = profileTab }
        } };
        var close = Ui.Button("Done", Close); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right;
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(24), Child = Ui.Column(
            Ui.Text("Make room in your own way.", 25, Ui.Accent), tabs, status, close) } };
        Refresh();
    }
    private Button Action(string label, Func<Task> action)
    {
        var button = new Button { Content = label, Padding = new Thickness(10, 6) };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
            { status.Foreground = Brushes.LightSalmon; status.Text = error is ArgumentException ? error.Message : "Changes could not be saved. Please try again."; }
            finally { button.IsEnabled = true; RefreshActions(); }
        };
        return button;
    }
    private async Task Save(AppSettings value, string message)
    {
        await save(value); Refresh(); status.Foreground = Brushes.LightGray; status.Text = message;
    }
    private async Task EditRoutine(BreakRoutine? existing)
    {
        var current = settings();
        if (existing is null && current.CustomRoutine is not null && current.AdditionalRoutines.Count >= AppSettings.MaxAdditionalRoutines)
            throw new ArgumentException("Your library already has 20 custom routines. Edit or remove one first.");
        var id = existing?.Id ?? (current.CustomRoutine is null ? BreakRoutines.CustomId : "routine-" + Guid.NewGuid().ToString("N"));
        var editor = new RoutineEditorWindow(existing, routine => Save(settings().SaveRoutine(routine), "Routine saved and selected for the next invitation."), id);
        await editor.ShowDialog(this);
    }
    private async Task EditProfile(WorkProfile? existing)
    {
        if (existing is null && settings().WorkProfiles.Count >= AppSettings.MaxWorkProfiles)
            throw new ArgumentException("You already have 10 profiles. Edit or remove one first.");
        var editor = new ProfileEditorWindow(settings(), existing, profile => Save(settings().SaveProfile(profile), "Profile saved. Select Apply profile to use it."));
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
        detail.Text = profile is null ? "No profiles yet. Save a setup you use often." :
            $"{BreakRoutines.ForSettings(settings()).FirstOrDefault(item => item.Id == profile.RoutineId)?.Name} · pause after {profile.IdleMinutes} min away";
    }
}
