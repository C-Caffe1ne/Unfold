using Avalonia;
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
    private readonly AnimationView preview = new() { Width = 120, Height = 120 };
    private readonly Button edit, delete, pause;
    private readonly CheckBox showPet;
    private CharacterPackage? previewCharacter;
    private bool updating;
    public SettingsWindow(AppRuntime runtime)
    {
        this.runtime = runtime; Title = "Unfold · Settings"; Width = 600; Height = 790; MinWidth = 550; MinHeight = 600;
        Background = Ui.Background;
        var interval = new NumericUpDown { Minimum = 5, Maximum = 240, Value = runtime.Settings.IntervalMinutes, Increment = 5, Width = 130, FormatString = "0" };
        var idle = new NumericUpDown { Minimum = 1, Maximum = 60, Value = runtime.Settings.IdleMinutes, Increment = 1, Width = 130, FormatString = "0" };
        pause = Ui.Button("Pause", runtime.TogglePause);
        var apply = Ui.AsyncButton("Apply reminder settings", async () =>
        {
            try { await runtime.UpdateSettings(runtime.Settings with { IntervalMinutes = (int)(interval.Value ?? 60), IdleMinutes = (int)(idle.Value ?? 5) }); }
            catch (Exception error) { await Ui.Error(this, error); }
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
        // Built but left out of the layout below: the MVP ships without the
        // pixel editor entry points, and Refresh() still drives their state.
        edit = Ui.AsyncButton("Edit", () => runtime.OpenEditor(runtime.Selected));
        delete = Ui.AsyncButton("Delete", async () => { if (runtime.Selected is { } selected) await runtime.DeleteCharacter(selected); });
        var body = Ui.Column(Ui.Text("UNFOLD", 14, Ui.Accent), Ui.Text("Make room for a small break.", 26),
            new Border { Background = Ui.Panel, CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = Ui.Column(
                Ui.Text("NEXT STRETCH", 12), countdown, state, Ui.Row(pause, Ui.Button("Reset", runtime.Reset), Ui.AsyncButton("Stretch now", runtime.ShowReminder))) },
            Ui.Row(Ui.Column(Ui.Text("Remind me every (min)", 12), interval), Ui.Column(Ui.Text("Pause when away (min)", 12), idle)), apply,
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
    private async void Refresh()
    {
        // `updating` only needs to span the synchronous block below, which suppresses the
        // checkbox/combobox handlers while we set control values programmatically. Holding
        // it across the await that follows used to make Refresh drop any Changed event that
        // arrived while a clip was loading — including the once-a-second clock tick — so
        // the countdown/status/checkbox/combobox sync below could silently go stale for as
        // long as a preview clip was in flight.
        updating = true;
        try
        {
            countdown.Text = $"{(int)runtime.Clock.Remaining.TotalMinutes:00}:{runtime.Clock.Remaining.Seconds:00}";
            state.Text = runtime.ActivityError ?? (runtime.Clock.Paused ? "Paused by you" : runtime.Clock.IdlePaused ? "Paused while you're away" : "Counting active time");
            pause.Content = runtime.Clock.Paused ? "Resume" : "Pause";
            showPet.IsChecked = runtime.Settings.ShowPet;
            if (!ReferenceEquals(characters.ItemsSource, runtime.Characters)) characters.ItemsSource = runtime.Characters;
            characters.SelectedItem = runtime.Selected;
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
    }
}
