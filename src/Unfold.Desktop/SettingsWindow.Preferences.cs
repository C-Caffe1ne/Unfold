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
    private readonly Button preferencesSave = new() { Name = "SavePreferences", Content = "저장", Classes = { "unfold-action", "primary" }, IsEnabled = false };
    private readonly Button preferencesCancel = new() { Name = "CancelPreferences", Content = "취소", Classes = { "unfold-action", "quiet" } };
    private readonly TextBlock preferencesStatus = new() { Name = "PreferencesStatus", IsVisible = false,
        FontSize = DesignSystem.Caption, Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private StackPanel? preferencesSections;
    private PreferencesValues? observedPreferences;
    private string? dueSoundId, completedSoundId, dueSoundName, completedSoundName;
    private Action? refreshSoundNames;
    private int soundImports;
    private bool savingPreferences, restoringPreferences;

    private sealed record PreferencesValues(BubbleDirection Direction, bool SoundsEnabled,
        string? DueId, string? CompletedId, string? DueName, string? CompletedName, int Idle, int Snooze)
    {
        public static PreferencesValues From(AppSettings settings) => new(settings.BubbleDirection, settings.ReminderSoundsEnabled,
            settings.ReminderSoundId, settings.CompletionSoundId, settings.ReminderSoundName, settings.CompletionSoundName,
            settings.IdleMinutes, settings.SnoozeMinutes);
    }

    private bool TryReadPreferences(out PreferencesValues? value)
    {
        value = null;
        static bool ValidMinutes(NumericUpDown input) => input.Value is decimal number && number is >= 1 and <= 60 &&
            decimal.Truncate(number) == number && decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var text) && text == number;
        if (bubbleDirection.SelectedItem is not BubbleDirection direction || !ValidMinutes(idle) || !ValidMinutes(snooze)) return false;
        value = new(direction, soundsEnabled.IsChecked == true, dueSoundId, completedSoundId, dueSoundName, completedSoundName,
            (int)idle.Value!.Value, (int)snooze.Value!.Value);
        return true;
    }

    private void RefreshPreferencesState()
    {
        var busy = savingPreferences || soundImports > 0;
        preferencesSave.IsEnabled = !busy && TryReadPreferences(out var pending) && pending != PreferencesValues.From(runtime.Settings);
        preferencesCancel.IsEnabled = !busy;
        if (preferencesSections is not null) preferencesSections.IsEnabled = !busy;
    }

    private void PreferencesEdited()
    {
        if (updating || restoringPreferences) return;
        preferencesStatus.IsVisible = false;
        if (observedPreferences is not null && !TryReadPreferences(out _))
            ShowPreferencesError("자리 비움과 다시 알림 시간은 1~60분의 정수로 입력해 주세요.");
        RefreshPreferencesState();
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
            idle.Value = saved.Idle; snooze.Value = saved.Snooze;
            // Restore raw invalid/empty text too, including when the numeric Value has not changed.
            idle.Text = saved.Idle.ToString(CultureInfo.CurrentCulture); snooze.Text = saved.Snooze.ToString(CultureInfo.CurrentCulture);
            dueSoundId = saved.DueId; completedSoundId = saved.CompletedId;
            dueSoundName = saved.DueName; completedSoundName = saved.CompletedName;
            refreshSoundNames?.Invoke(); observedPreferences = saved;
        }
        finally { restoringPreferences = false; }
        preferencesStatus.IsVisible = false; RefreshPreferencesState();
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

    private async Task SavePreferences()
    {
        if (savingPreferences || soundImports > 0 || !TryReadPreferences(out var pending) || pending is null ||
            pending == PreferencesValues.From(runtime.Settings)) return;
        savingPreferences = true; preferencesStatus.IsVisible = false; RefreshPreferencesState();
        try
        {
            var current = runtime.Settings;
            await runtime.UpdateSettings(current with
            {
                BubbleDirection = pending.Direction, ReminderSoundsEnabled = pending.SoundsEnabled,
                ReminderSoundId = pending.DueId, CompletionSoundId = pending.CompletedId,
                ReminderSoundName = pending.DueName, CompletionSoundName = pending.CompletedName,
                IdleMinutes = pending.Idle, SnoozeMinutes = pending.Snooze,
                ActiveProfileId = pending.Idle == current.IdleMinutes ? current.ActiveProfileId : null
            });
            observedPreferences = PreferencesValues.From(runtime.Settings);
        }
        catch (Exception error)
        {
            AppPaths.Log(error); ShowPreferencesError("설정을 저장하지 못했어요. " + Ui.ErrorText(error));
        }
        finally { savingPreferences = false; RefreshPreferencesState(); }
    }

    private Control BuildPreferencesFooter()
    {
        foreach (var button in new[] { preferencesCancel, preferencesSave })
        {
            button.Width = DesignSystem.SettingsActionWidth; button.Height = DesignSystem.SettingsControlHeight;
        }
        AutomationProperties.SetName(preferencesSave, "설정 저장");
        AutomationProperties.SetName(preferencesCancel, "설정 변경 취소");
        preferencesSave.Click += async (_, _) => await SavePreferences();
        preferencesCancel.Click += (_, _) =>
        {
            if (savingPreferences || soundImports > 0) return;
            stopSoundPreview?.Invoke(); RestorePreferences(PreferencesValues.From(runtime.Settings));
        };
        var actions = Ui.Row(preferencesCancel, preferencesSave);
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        var footer = new Grid { Name = "SettingsPreferencesFooter", ColumnDefinitions = new("*,24,Auto"), Margin = new(0, 24, 0, 0) };
        footer.Children.Add(preferencesStatus); Grid.SetColumn(actions, 2); footer.Children.Add(actions);
        return footer;
    }

    private static TextBlock SettingsHeading(string text) => new() { Text = text, FontSize = DesignSystem.Section,
        FontWeight = FontWeight.SemiBold, Foreground = DesignSystem.Cream };
    private static TextBlock SettingsLabel(string text) => new() { Text = text, FontSize = DesignSystem.Body,
        Foreground = DesignSystem.Cream, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
}
