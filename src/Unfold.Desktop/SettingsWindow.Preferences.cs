using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private readonly ComboBox bubbleDirection = new() { Name = "BubbleDirection" };
    private readonly CheckBox soundsEnabled = new() { Name = "ReminderSoundsEnabled", Content = "알림 효과음 사용" };
    private readonly Slider soundVolume = new() { Name = "ReminderVolumePercent", Minimum = 0, Maximum = 100,
        TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 10, Value = 100, Classes = { "thumb-hover-slider" } };
    private readonly SoundVolumeIcon soundVolumeIcon = new() { Name = "ReminderVolumeIcon" };
    private readonly Slider dueSoundVolume = SoundVolumeSlider("ReminderSoundVolumePercent");
    private readonly Slider completedSoundVolume = SoundVolumeSlider("CompletionSoundVolumePercent");
    private readonly SoundVolumeIcon dueSoundVolumeIcon = new() { Name = "ReminderSoundVolumeIcon" };
    private readonly SoundVolumeIcon completedSoundVolumeIcon = new() { Name = "CompletionSoundVolumeIcon" };
    private static Slider SoundVolumeSlider(string name) => new() { Name = name, Minimum = 0, Maximum = 100,
        TickFrequency = 1, IsSnapToTickEnabled = true, SmallChange = 1, LargeChange = 10, Value = 100, Classes = { "thumb-hover-slider" } };
    private readonly TextBlock preferencesStatus = new() { Name = "PreferencesStatus", IsVisible = false,
        FontSize = DesignSystem.Caption, Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private StackPanel? preferencesSections;
    private PreferencesValues? observedPreferences;
    private string? dueSoundId, completedSoundId, dueSoundName, completedSoundName;
    private Action? refreshSoundNames;
    private int soundImports;
    private bool restoringPreferences;
    private long preferencesEditVersion;
    private readonly ReminderSounds importedSounds = new(Path.Combine(AppPaths.DataRoot, "Sounds"));

    private sealed record PreferencesValues(BubbleDirection Direction, bool SoundsEnabled, int VolumePercent, int DueVolumePercent, int CompletedVolumePercent,
        string? DueId, string? CompletedId, string? DueName, string? CompletedName, int Idle, int Snooze, bool DebugTools)
    {
        public static PreferencesValues From(AppSettings settings) => new(settings.BubbleDirection, settings.ReminderSoundsEnabled, settings.ReminderVolumePercent,
            settings.ReminderSoundVolumePercent, settings.CompletionSoundVolumePercent,
            settings.ReminderSoundId, settings.CompletionSoundId, settings.ReminderSoundName, settings.CompletionSoundName,
            settings.IdleMinutes, settings.SnoozeMinutes, settings.DebugToolsEnabled);
    }

    private bool TryReadPreferences(out PreferencesValues? value)
    {
        static bool ValidMinutes(NumericUpDown input) => input.Value is decimal number && number is >= 1 and <= 60 &&
            decimal.Truncate(number) == number && decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var text) && text == number;
        var idleValid = ValidMinutes(idle); var snoozeValid = ValidMinutes(snooze);
        value = new(bubbleDirection.SelectedItem is BubbleDirection direction ? direction : runtime.Settings.BubbleDirection,
            soundsEnabled.IsChecked == true, (int)Math.Round(soundVolume.Value),
            (int)Math.Round(dueSoundVolume.Value), (int)Math.Round(completedSoundVolume.Value), dueSoundId, completedSoundId, dueSoundName, completedSoundName,
            idleValid ? (int)idle.Value!.Value : runtime.Settings.IdleMinutes,
            snoozeValid ? (int)snooze.Value!.Value : runtime.Settings.SnoozeMinutes, debugToolsEnabled.IsChecked == true);
        return idleValid && snoozeValid && bubbleDirection.SelectedItem is BubbleDirection;
    }

    private void RefreshPreferencesState()
    {
        if (preferencesSections is not null) preferencesSections.IsEnabled = soundImports == 0;
    }

    private void PreferencesEdited()
    {
        if (updating || restoringPreferences || observedPreferences is null || disposed || soundImports > 0) return;
        preferencesStatus.IsVisible = false;
        if (!TryReadPreferences(out var pending))
            ShowPreferencesError("자리 비움과 다시 알림 시간은 1~60분의 정수로 입력해 주세요.");
        _ = ApplyPreferences(pending!, ++preferencesEditVersion);
    }

    private void ShowPreferencesError(string message)
    {
        preferencesStatus.Text = message;
        preferencesStatus.IsVisible = true;
    }

    private void RestorePreferences(PreferencesValues saved)
    {
        restoringPreferences = true;
        try
        {
            bubbleDirection.SelectedItem = saved.Direction; soundsEnabled.IsChecked = saved.SoundsEnabled;
            soundVolume.Value = saved.VolumePercent;
            dueSoundVolume.Value = saved.DueVolumePercent; completedSoundVolume.Value = saved.CompletedVolumePercent;
            debugToolsEnabled.IsChecked = saved.DebugTools;
            idle.Value = saved.Idle; snooze.Value = saved.Snooze;
            // Restore raw invalid/empty text too, including when the numeric Value has not changed.
            idle.Text = saved.Idle.ToString(CultureInfo.CurrentCulture); snooze.Text = saved.Snooze.ToString(CultureInfo.CurrentCulture);
            dueSoundId = saved.DueId; completedSoundId = saved.CompletedId;
            dueSoundName = saved.DueName; completedSoundName = saved.CompletedName;
            refreshSoundNames?.Invoke(); observedPreferences = saved;
            RefreshDebugToolsVisibility();
            if (!saved.DebugTools) runtime.CloseReminderPreview();
        }
        finally { restoringPreferences = false; }
        preferencesStatus.IsVisible = false; RefreshPreferencesState(); CleanupImportedSounds();
    }

    private void SyncPreferencesFromRuntime()
    {
        if (observedPreferences is null) return;
        var saved = PreferencesValues.From(runtime.Settings);
        if (saved != observedPreferences)
        {
            // Runtime refreshes must not erase an in-progress form edit.
            if (TryReadPreferences(out var pending) && pending == observedPreferences) RestorePreferences(saved);
            observedPreferences = saved;
        }
        RefreshPreferencesState();
    }

    private async Task ApplyPreferences(PreferencesValues pending, long version)
    {
        if (pending == PreferencesValues.From(runtime.Settings)) return;
        try
        {
            var current = runtime.Settings;
            // UpdateSettings persists and sets Settings before its asynchronous pet refresh.
            // Each edit starts immediately so rapid edits and hiding the window keep the newest value.
            var apply = runtime.UpdateSettings(current with
            {
                BubbleDirection = pending.Direction, ReminderSoundsEnabled = pending.SoundsEnabled, ReminderVolumePercent = pending.VolumePercent,
                ReminderSoundVolumePercent = pending.DueVolumePercent, CompletionSoundVolumePercent = pending.CompletedVolumePercent,
                ReminderSoundId = pending.DueId, CompletionSoundId = pending.CompletedId,
                ReminderSoundName = pending.DueName, CompletionSoundName = pending.CompletedName,
                IdleMinutes = pending.Idle, SnoozeMinutes = pending.Snooze, DebugToolsEnabled = pending.DebugTools,
                ActiveProfileId = pending.Idle == current.IdleMinutes ? current.ActiveProfileId : null
            });
            observedPreferences = PreferencesValues.From(runtime.Settings);
            await apply;
        }
        catch (Exception error)
        {
            AppPaths.Log(error);
            if (!disposed && version == preferencesEditVersion)
                ShowPreferencesError("설정을 저장하지 못했어요. " + Ui.ErrorText(error));
        }
        finally { CleanupImportedSounds(); }
    }

    private void CleanupImportedSounds()
    {
        // Imports run off-thread: wait until their IDs have joined the draft before collecting.
        if (soundImports > 0) return;
        var retained = new List<string?> { runtime.Settings.ReminderSoundId, runtime.Settings.CompletionSoundId };
        if (!disposed && observedPreferences is not null) { retained.Add(dueSoundId); retained.Add(completedSoundId); }
        importedSounds.RemoveUnused(retained, AppPaths.Log);
    }

    private static TextBlock SettingsHeading(string text) => new() { Text = text, FontSize = DesignSystem.Section,
        FontWeight = FontWeight.SemiBold, Foreground = DesignSystem.Cream };
    private static TextBlock SettingsLabel(string text) => new() { Text = text, FontSize = DesignSystem.Body,
        Foreground = DesignSystem.Cream, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
}
