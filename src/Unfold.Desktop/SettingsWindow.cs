using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class SettingsWindow : Window
{
    private readonly AppRuntime runtime;
    private readonly TextBlock countdown = Ui.Text("60:00", 52, Ui.Accent), state = Ui.Text("Ready", 13);
    private readonly ComboBox characters = new() { MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox routines = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock today = Ui.Text("No breaks yet today", 16);
    private readonly TextBlock historyStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
    private readonly AnimationView preview = new() { Width = 120, Height = 120 };
    private readonly TimerControls timerControls;
    private readonly CheckBox showPet;
    private readonly TextBlock activeProfile = Ui.Text("Custom reminder settings", 12, Ui.Accent);
    private readonly NumericUpDown interval, idle;
    private readonly TextBlock reminderSettingsStatus = new() { Text = "Interval changes are saved automatically.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray };
    private int displayedInterval, displayedIdle;
    private string displayedRoutine;
    private CharacterPackage? previewCharacter;
    private bool updating;
    public SettingsWindow(AppRuntime runtime)
    {
<<<<<<< HEAD
        this.runtime = runtime; Title = "Unfold · Stretch Reminder"; Width = 600; Height = 850; MinWidth = 550; MinHeight = 600;
=======
        this.runtime = runtime; Title = "Unfold · Settings"; Width = 600; Height = 790; MinWidth = 550; MinHeight = 600;
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163
        Background = Ui.Background;
        interval = new NumericUpDown { Name = "ReminderInterval", Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 5, Width = 130, FormatString = "0" };
        idle = new NumericUpDown { Name = "ReminderIdle", Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, Width = 130, FormatString = "0" };
        displayedInterval = runtime.Settings.IntervalMinutes; displayedIdle = runtime.Settings.IdleMinutes; displayedRoutine = runtime.Settings.BreakRoutineId;
        routines.ItemsSource = runtime.Routines;
        routines.SelectedItem = runtime.Routines.FirstOrDefault(item => item.Id == runtime.Settings.BreakRoutineId);
        timerControls = new(runtime.TogglePause, runtime.Stop, runtime.Reset);
        AutomationProperties.SetName(interval, "Remind me every, in minutes");
        AutomationProperties.SetName(idle, "Pause when away, in minutes");
        interval.ValueChanged += async (_, _) =>
        {
            if (updating || interval.Value is not decimal minutes || minutes is < 5 or > 240 || decimal.Truncate(minutes) != minutes ||
                minutes == runtime.Settings.IntervalMinutes) return;
            await SaveReminderSettings((int)minutes, runtime.Settings.IdleMinutes, runtime.Settings.BreakRoutineId);
        };
        var apply = Ui.AsyncButton("Apply reminder settings", async () =>
        {
            if (interval.Value is not decimal minutes || minutes is < 5 or > 240 || decimal.Truncate(minutes) != minutes ||
                idle.Value is not decimal away || away is < 1 or > 60 || decimal.Truncate(away) != away)
            { reminderSettingsStatus.Text = "Enter whole minutes: interval 5–240, away threshold 1–60."; reminderSettingsStatus.Foreground = Brushes.LightSalmon; return; }
            await SaveReminderSettings((int)minutes, (int)away, (routines.SelectedItem as BreakRoutine)?.Id ?? BreakRoutines.DefaultId);
        });
        showPet = new CheckBox { Content = "Show desktop pet", IsChecked = runtime.Settings.ShowPet };
        showPet.IsCheckedChanged += async (_, _) =>
        {
            if (updating) return;
            try { await runtime.UpdateSettings(runtime.Settings with { ShowPet = showPet.IsChecked == true }); }
            catch (Exception error) { updating = true; showPet.IsChecked = runtime.Settings.ShowPet; updating = false; await Ui.Error(this, error); }
        };
        var login = new CheckBox { Content = "Launch at login" };
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
        var body = Ui.Column(Ui.Text("UNFOLD", 14, Ui.Accent), Ui.Text("Make room for a small break.", 26),
            new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = Ui.Column(
                Ui.Text("NEXT STRETCH", 12), countdown, state, timerControls) },
            Ui.Row(Ui.Column(Ui.Text("Remind me every (min)", 12), interval), Ui.Column(Ui.Text("Pause when away (min)", 12), idle)),
            reminderSettingsStatus,
            activeProfile, Ui.Text("YOUR BREAK", 12, Ui.Accent), routines, Ui.Row(apply, Ui.AsyncButton("Edit my routine", async () =>
            {
                var dialog = new RoutineEditorWindow(runtime.Settings.CustomRoutine, routine => runtime.UpdateSettings(runtime.Settings.SaveRoutine(routine)));
                await dialog.ShowDialog(this);
            })),
            Ui.AsyncButton("My routines & work profiles", async () =>
            {
                var dialog = new PersonalizationWindow(() => runtime.Settings, runtime.UpdateSettings); await dialog.ShowDialog(this);
            }),
            new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Child = Ui.Column(
                Ui.Text("MOMENTS FOR YOURSELF", 12, Ui.Accent), today, historyStatus, Ui.AsyncButton("Review & export", async () =>
                {
                    var dialog = new BreakReviewWindow(runtime.BreakHistory.Review, () => runtime.BreakHistoryError); await dialog.ShowDialog(this);
                })) },
            new Separator(), Ui.Text("YOUR COMPANION", 12, Ui.Accent), Ui.Row(preview, Ui.Column(characters)),
            showPet, login, new Separator(), Ui.Row(Ui.Text("Closing this window keeps Unfold in the tray.", 12), Ui.AsyncButton("Quit", runtime.Quit)));
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(28), Child = body } };
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
            reminderSettingsStatus.Foreground = Brushes.LightGray; reminderSettingsStatus.Text = "Reminder settings saved.";
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        { AppPaths.Log(error); reminderSettingsStatus.Foreground = Brushes.LightSalmon; reminderSettingsStatus.Text = "Could not save reminder settings. Please try Apply again."; }
    }
    private async void Refresh()
    {
<<<<<<< HEAD
        if (updating) return; updating = true;
        CharacterPackage? loadPreview = null;
=======
        // `updating` only needs to span the synchronous block below, which suppresses the
        // checkbox/combobox handlers while we set control values programmatically. Holding
        // it across the await that follows used to make Refresh drop any Changed event that
        // arrived while a clip was loading — including the once-a-second clock tick — so
        // the countdown/status/checkbox/combobox sync below could silently go stale for as
        // long as a preview clip was in flight.
        updating = true;
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163
        try
        {
            countdown.Text = $"{(int)runtime.Clock.Remaining.TotalMinutes:00}:{runtime.Clock.Remaining.Seconds:00}";
            state.Text = runtime.ActivityError ?? (runtime.ActiveReminder is not null ? "A break is open · work timer on hold" :
                runtime.Clock.Stopped ? "Stopped · press play to start" : runtime.Clock.Paused ? "Paused · press play to continue" : runtime.Clock.IdlePaused ? "Paused while you're away" : "Counting active time");
            var summary = runtime.BreakHistory.ForDay(DateTimeOffset.Now);
            today.Text = summary.Count == 0 ? "No breaks yet today. Start with one small moment." :
                $"Today · {summary.Count} {(summary.Count == 1 ? "break" : "breaks")} · {summary.Seconds / 60}m {summary.Seconds % 60}s";
            today.TextWrapping = TextWrapping.Wrap;
            historyStatus.Text = runtime.BreakHistoryError ?? "Breaks you confirm are saved only on this device.";
            timerControls.Refresh(runtime.Clock);
            showPet.IsChecked = runtime.Settings.ShowPet;
            activeProfile.Text = runtime.Settings.WorkProfiles.FirstOrDefault(profile => profile.Id == runtime.Settings.ActiveProfileId) is { } active
                ? $"Work profile · {active.Name}" : "Custom reminder settings";
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
<<<<<<< HEAD
            if (runtime.Selected is { } selected && previewCharacter != selected)
            {
                previewCharacter = selected; loadPreview = selected;
            }
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = error.Message; }
        finally { updating = false; }
        if (loadPreview is null) return;
        try
        {
            var frames = await runtime.Clip("idle");
            if (runtime.Selected == loadPreview && previewCharacter == loadPreview)
                preview.SetFrames(frames, true, loadPreview.Manifest.RenderStyle == "pixel");
            if (!IsVisible) preview.SetRunning(false);
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = error.Message; }
=======
            edit.IsEnabled = delete.IsEnabled = runtime.Selected is { IsBuiltIn: false };
        }
        catch (Exception error) { AppPaths.Log(error); state.Text = error.Message; }
        finally { updating = false; }
        if (runtime.Selected is not { } selected || previewCharacter == selected) return;
        // Latched immediately (not after the await): AppRuntime.Clip() caches a failed
        // decode forever for a given character+key, so retrying the same still-selected
        // identity on every subsequent Changed event (e.g. the clock tick, once a second)
        // could never succeed anyway and would only re-log the same stale error endlessly.
        // A character that actually gets fixed and reloaded arrives here as a new
        // CharacterPackage instance (Reload()/SavedCharacter() always construct fresh
        // ones), which compares unequal and is retried normally.
        previewCharacter = selected;
        try
        {
            var frames = await runtime.Clip("idle");
            // The selection may have moved on again while this clip was loading; only the
            // load that still matches the current selection is allowed to land.
            if (runtime.Selected != selected) return;
            preview.SetFrames(frames, true, selected.Manifest.RenderStyle == "pixel");
            if (!IsVisible) preview.SetRunning(false);
        }
        // Kept out of the block above: a failed clip load must not stomp the countdown
        // status text that block just set (the original bug shared one catch for both).
        catch (Exception error) { AppPaths.Log(error); }
>>>>>>> 6da89eee87644cab6f3ff27383b181423a636163
    }
}
