using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private readonly ReminderSoundPlayer settingsSoundPlayer = new();
    private readonly CancellationTokenSource soundImportCancellation = new();
    private Action? stopSoundPreview;
    internal Func<ReminderSound, Task<string?>>? ChooseSoundFile { get; set; }
    internal Func<ReminderSound, AppSettings, CancellationToken, Task>? PlaySoundPreview { get; set; }

    private Border BuildNotificationSettingsCard()
    {
        var opacityLabel = SettingsLabel("말풍선 불투명도");
        var opacityValue = Ui.Caption("100%"); opacityValue.Name = "BubbleOpacityValue";
        bubbleOpacity.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(bubbleOpacity, "말풍선 불투명도, 퍼센트");
        var opacityControls = new Grid { Width = 320, ColumnDefinitions = new("*,12,56") };
        opacityControls.Children.Add(bubbleOpacity); Grid.SetColumn(opacityValue, 2); opacityControls.Children.Add(opacityValue);
        bubbleOpacity.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) { opacityValue.Text = $"{bubbleOpacity.Value:0}%"; PreferencesEdited(); } };
        Control DialogueRow(string caption, TextBox input)
        {
            AutomationProperties.SetName(input, caption + " 펫 대사");
            input.TextChanged += (_, _) => PreferencesEdited();
            return Ui.Column(SettingsLabel(caption), input);
        }
        var resetDialogue = Ui.Button("기본 대사로 복원", () =>
        {
            var defaults = new AppSettings();
            restoringPreferences = true;
            try { advanceDialogue.Text = defaults.AdvanceDialogue; invitationDialogue.Text = defaults.InvitationDialogue;
                restingDialogue.Text = defaults.RestingDialogue; overtimeDialogue.Text = defaults.OvertimeDialogue; completedDialogue.Text = defaults.CompletedDialogue; }
            finally { restoringPreferences = false; }
            PreferencesEdited();
        });
        resetDialogue.Name = "ResetPetDialogue";
        var bubbleFields = Ui.Column(NotificationSettingRow("BubbleOpacityRow", opacityLabel, opacityControls),
            DialogueRow("사전 알림", advanceDialogue), DialogueRow("휴식 초대", invitationDialogue),
            DialogueRow("휴식 중", restingDialogue), DialogueRow("휴식 시간 초과", overtimeDialogue),
            DialogueRow("휴식 완료", completedDialogue), resetDialogue);
        bubbleFields.Spacing = DesignSystem.SettingsRowGap;
        soundsEnabled.FontSize = DesignSystem.Body;
        var previewButtons = new Dictionary<ReminderSound, Button>();
        CancellationTokenSource? previewCancellation = null;
        ReminderSound? playing = null;
        AppSettings PendingSoundSettings() => runtime.Settings with
        {
            ReminderSoundsEnabled = soundsEnabled.IsChecked == true,
            ReminderVolumePercent = (int)Math.Round(soundVolume.Value),
            ReminderSoundVolumePercent = (int)Math.Round(dueSoundVolume.Value),
            CompletionSoundVolumePercent = (int)Math.Round(completedSoundVolume.Value),
            ReminderSoundId = dueSoundId, CompletionSoundId = completedSoundId,
            ReminderSoundName = dueSoundName, CompletionSoundName = completedSoundName
        };
        void RefreshPlaybackButtons()
        {
            foreach (var (kind, button) in previewButtons)
            {
                ((SoundPreviewIcon)button.Content!).IsPlaying = playing == kind;
                var label = (kind == ReminderSound.Due ? "스트레칭 알림" : "완료 알림") +
                    (playing == kind ? " 미리듣기 중지" : " 효과음 미리듣기");
                AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label);
            }
        }
        stopSoundPreview = () =>
        {
            previewCancellation?.Cancel(); previewCancellation = null;
            settingsSoundPlayer.Stop(); playing = null; RefreshPlaybackButtons();
        };
        Grid VolumeControls(string name, Slider slider, SoundVolumeIcon icon, TextBlock caption, Button? play = null)
        {
            Ui.KeyboardFocusLabel(slider, caption);
            AutomationProperties.SetLabeledBy(slider, caption);
            var label = caption.Text;
            AutomationProperties.SetName(slider, label + ", 퍼센트");
            slider.MinWidth = 0; slider.VerticalAlignment = VerticalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.SetVolume(slider.Value);
            var percent = Ui.Caption($"{slider.Value:0}%"); percent.Name = slider.Name + "Value";
            percent.VerticalAlignment = VerticalAlignment.Center; percent.TextAlignment = TextAlignment.Right;
            var controls = new Grid { Name = name, Width = 360, Height = DesignSystem.SettingsControlHeight,
                ColumnDefinitions = new($"24,12,*,12,44,12,{DesignSystem.SettingsControlHeight}") };
            controls.Children.Add(icon); Grid.SetColumn(slider, 2); controls.Children.Add(slider);
            Grid.SetColumn(percent, 4); controls.Children.Add(percent);
            if (play is not null) { Grid.SetColumn(play, 6); controls.Children.Add(play); }
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property != Slider.ValueProperty) return;
                icon.SetVolume(slider.Value); percent.Text = $"{slider.Value:0}%";
                PreferencesEdited();
            };
            return controls;
        }
        Control SoundRow(string caption, ReminderSound kind)
        {
            var label = Ui.Caption(""); label.Name = kind == ReminderSound.Due ? "DueSoundName" : "CompletionSoundName";
            label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            void RefreshLabel()
            {
                var id = kind == ReminderSound.Due ? dueSoundId : completedSoundId;
                var name = kind == ReminderSound.Due ? dueSoundName : completedSoundName;
                label.Text = id is null ? "기본 효과음" : name ?? "가져온 효과음 · " + id[..8];
                ToolTip.SetTip(label, label.Text);
            }
            refreshSoundNames += RefreshLabel;
            RefreshLabel();
            var play = new Button { Content = new SoundPreviewIcon(), Classes = { "unfold-action" },
                Padding = new Thickness(8) };
            play.Name = kind == ReminderSound.Due ? "PreviewDueSound" : "PreviewCompletionSound";
            play.Width = DesignSystem.SettingsControlHeight;
            ToolTip.SetShowDelay(play, 500);
            previewButtons.Add(kind, play);
            play.Click += async (_, _) =>
            {
                var stop = playing == kind; stopSoundPreview();
                if (stop) return;
                using var cancellation = new CancellationTokenSource();
                previewCancellation = cancellation; playing = kind; RefreshPlaybackButtons();
                try
                {
                    preferencesStatus.IsVisible = false;
                    await (PlaySoundPreview ?? settingsSoundPlayer.Preview)(kind, PendingSoundSettings(), cancellation.Token);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception error)
                {
                    if (!cancellation.IsCancellationRequested)
                    { ShowPreferencesError("미리듣기를 하지 못했어요. " + Ui.ErrorText(error)); AppPaths.Log(error); }
                }
                finally
                {
                    if (previewCancellation == cancellation)
                    { previewCancellation = null; playing = null; RefreshPlaybackButtons(); }
                    CleanupImportedSounds();
                }
            };
            var import = Ui.AsyncButton("파일 가져오기", async () =>
            {
                soundImports++; RefreshPreferencesState();
                var imported = false;
                try
                {
                    string? path;
                    if (ChooseSoundFile is { } choose) path = await choose(kind);
                    else
                    {
                        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = caption + " 효과음 가져오기", AllowMultiple = false,
                            FileTypeFilter = [new FilePickerFileType(ReminderSoundImporter.SupportedFileTypes) { Patterns = ReminderSoundImporter.FilePatterns }] });
                        path = files.FirstOrDefault()?.TryGetLocalPath();
                    }
                    if (path is null || disposed) return;
                    var id = await ReminderSoundImporter.Import(path, importedSounds, soundImportCancellation.Token);
                    if (disposed) return;
                    stopSoundPreview();
                    var fileName = Path.GetFileName(path);
                    if (kind == ReminderSound.Due) { dueSoundId = id; dueSoundName = fileName; }
                    else { completedSoundId = id; completedSoundName = fileName; }
                    RefreshLabel(); imported = true;
                }
                catch (OperationCanceledException) when (disposed) { }
                catch (Exception error) { if (!disposed) ShowPreferencesError(error is InvalidDataException ? error.Message : "효과음을 가져오지 못했어요."); AppPaths.Log(error); }
                finally { soundImports--; RefreshPreferencesState(); if (imported) PreferencesEdited(); CleanupImportedSounds(); }
            });
            import.Name = kind == ReminderSound.Due ? "ImportDueSound" : "ImportCompletionSound";
            import.Width = DesignSystem.SettingsImportWidth;
            AutomationProperties.SetName(import, caption + " 효과음 파일 가져오기");
            var reset = Ui.Button("기본", () =>
            {
                stopSoundPreview();
                if (kind == ReminderSound.Due) { dueSoundId = null; dueSoundName = null; }
                else { completedSoundId = null; completedSoundName = null; }
                RefreshLabel(); PreferencesEdited(); CleanupImportedSounds();
            });
            reset.Name = kind == ReminderSound.Due ? "ResetDueSound" : "ResetCompletionSound";
            reset.Width = DesignSystem.SettingsResetWidth; reset.Classes.Add("settings-reset");
            AutomationProperties.SetName(reset, caption + " 기본 효과음 사용");
            foreach (var button in new[] { play, import, reset })
            { button.Classes.Add("compact"); button.Height = DesignSystem.SettingsControlHeight; }
            var captionLabel = SettingsLabel(caption);
            var file = Ui.Column(captionLabel, label); file.Spacing = 4; file.VerticalAlignment = VerticalAlignment.Top;
            var sourceButtons = Ui.Row(import, reset); sourceButtons.HorizontalAlignment = HorizontalAlignment.Right;
            var slider = kind == ReminderSound.Due ? dueSoundVolume : completedSoundVolume;
            var icon = kind == ReminderSound.Due ? dueSoundVolumeIcon : completedSoundVolumeIcon;
            var volumeControls = VolumeControls(kind == ReminderSound.Due ? "DueSoundVolumeControls" : "CompletionSoundVolumeControls",
                slider, icon, captionLabel, play);
            volumeControls.Width = double.NaN;
            var editor = Ui.Column(sourceButtons, volumeControls); editor.Width = 360; editor.Spacing = 12;
            return NotificationSettingRow(kind == ReminderSound.Due ? "DueSoundRow" : "CompletionSoundRow",
                file, editor, gap: 24);
        }
        var soundRows = Ui.Column(SoundRow("스트레칭 알림", ReminderSound.Due), SoundRow("완료 알림", ReminderSound.Completed));
        soundRows.Spacing = DesignSystem.SettingsRowGap;
        var volumeLabel = SettingsLabel("전체 소리");
        var volumeControls = VolumeControls("ReminderVolumeControls", soundVolume, soundVolumeIcon, volumeLabel);
        var volume = NotificationSettingRow("ReminderVolumeRow", volumeLabel, volumeControls);
        var soundGroup = Ui.Column(volume, soundRows, soundsEnabled);
        soundGroup.Spacing = DesignSystem.SettingsRowGap; soundGroup.Margin = new(0, 24, 0, 0);
        var fields = Ui.Column(bubbleFields, soundGroup); fields.Spacing = 0;
        var body = Ui.Column(SettingsHeading("알림 설정"), fields);
        soundsEnabled.IsCheckedChanged += (_, _) => PreferencesEdited();
        RefreshPlaybackButtons();
        body.Margin = new(DesignSystem.Inset); body.Spacing = DesignSystem.Inset;
        return Card("SettingsNotificationCard", body, DesignSystem.Surface, new(28));
    }

    private static Grid NotificationSettingRow(string name, Control label, Control editor, int gap = 16)
    {
        var row = new Grid { Name = name, ColumnDefinitions = new($"*,{gap},Auto"), RowDefinitions = new("Auto") };
        row.Children.Add(label); Grid.SetColumn(editor, 2); row.Children.Add(editor);
        editor.HorizontalAlignment = HorizontalAlignment.Right;
        var maximumEditorWidth = editor.MaxWidth;
        bool? stacked = null;
        row.SizeChanged += (_, e) =>
        {
            editor.MaxWidth = Math.Min(maximumEditorWidth, e.NewSize.Width);
            var narrow = e.NewSize.Width < 480;
            if (stacked == narrow) return;
            stacked = narrow;
            row.ColumnDefinitions = narrow ? new("*") : new($"*,{gap},Auto");
            row.RowDefinitions = narrow ? new("Auto,12,Auto") : new("Auto");
            Grid.SetColumn(editor, narrow ? 0 : 2); Grid.SetRow(editor, narrow ? 2 : 0);
        };
        return row;
    }
}
