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
    private Action? stopSoundPreview;
    internal Func<ReminderSound, Task<string?>>? ChooseSoundFile { get; set; }
    internal Func<ReminderSound, AppSettings, CancellationToken, Task>? PlaySoundPreview { get; set; }

    private Border BuildNotificationSettingsCard()
    {
        bubbleDirection.ItemsSource = Enum.GetValues<BubbleDirection>();
        bubbleDirection.Classes.Add("settings-choice");
        bubbleDirection.Width = DesignSystem.SettingsChoiceWidth;
        bubbleDirection.Height = DesignSystem.SettingsControlHeight;
        bubbleDirection.HorizontalAlignment = HorizontalAlignment.Right;
        bubbleDirection.MaxDropDownHeight = 180;
        bubbleDirection.ItemTemplate = new FuncDataTemplate<BubbleDirection>((value, _) => new TextBlock
        {
            Text = value switch { BubbleDirection.Top => "위", BubbleDirection.Bottom => "아래", BubbleDirection.Left => "왼쪽", _ => "오른쪽" },
            VerticalAlignment = VerticalAlignment.Center
        });
        bubbleDirection.ContainerPrepared += (_, e) => e.Container.Classes.Add("settings-choice-item");
        var directionLabel = SettingsLabel("말풍선 위치");
        Ui.KeyboardFocusLabel(bubbleDirection, directionLabel);
        AutomationProperties.SetLabeledBy(bubbleDirection, directionLabel);
        AutomationProperties.SetName(bubbleDirection, "말풍선 위치");
        var position = NotificationSettingRow("BubbleDirectionRow", directionLabel, bubbleDirection);
        soundsEnabled.FontSize = DesignSystem.Body;
        var previewButtons = new Dictionary<ReminderSound, Button>();
        CancellationTokenSource? previewCancellation = null;
        ReminderSound? playing = null;
        AppSettings PendingSoundSettings() => runtime.Settings with
        {
            ReminderSoundsEnabled = soundsEnabled.IsChecked == true,
            ReminderVolumePercent = (int)Math.Round(soundVolume.Value),
            ReminderSoundId = dueSoundId, CompletionSoundId = completedSoundId,
            ReminderSoundName = dueSoundName, CompletionSoundName = completedSoundName
        };
        void RefreshPlaybackButtons()
        {
            foreach (var (kind, button) in previewButtons)
            {
                button.Content = playing == kind ? "정지" : "미리듣기";
                AutomationProperties.SetName(button, (kind == ReminderSound.Due ? "스트레칭 알림" : "완료 알림") +
                    (playing == kind ? " 미리듣기 정지" : " 효과음 미리듣기"));
            }
        }
        stopSoundPreview = () =>
        {
            previewCancellation?.Cancel(); previewCancellation = null;
            settingsSoundPlayer.Stop(); playing = null; RefreshPlaybackButtons();
        };
        Control SoundRow(string caption, ReminderSound kind)
        {
            var label = Ui.Caption(""); label.Name = kind == ReminderSound.Due ? "DueSoundName" : "CompletionSoundName";
            label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            void RefreshLabel()
            {
                var id = kind == ReminderSound.Due ? dueSoundId : completedSoundId;
                var name = kind == ReminderSound.Due ? dueSoundName : completedSoundName;
                label.Text = id is null ? "기본 효과음" : name ?? "가져온 WAV · " + id[..8];
                ToolTip.SetTip(label, label.Text);
            }
            refreshSoundNames += RefreshLabel;
            RefreshLabel();
            var play = Ui.Button("미리듣기", () => { });
            play.Name = kind == ReminderSound.Due ? "PreviewDueSound" : "PreviewCompletionSound";
            play.Width = DesignSystem.SettingsPreviewWidth;
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
                }
            };
            var import = Ui.AsyncButton("파일 가져오기", async () =>
            {
                soundImports++; RefreshPreferencesState();
                try
                {
                    string? path;
                    if (ChooseSoundFile is { } choose) path = await choose(kind);
                    else
                    {
                        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = caption + " 효과음 가져오기", AllowMultiple = false,
                            FileTypeFilter = [new FilePickerFileType("WAV 효과음") { Patterns = ["*.wav"] }] });
                        path = files.FirstOrDefault()?.TryGetLocalPath();
                    }
                    if (path is null) return;
                    var library = new ReminderSounds(Path.Combine(AppPaths.DataRoot, "Sounds"));
                    var id = await Task.Run(() => library.Import(path));
                    stopSoundPreview();
                    var fileName = Path.GetFileName(path);
                    if (kind == ReminderSound.Due) { dueSoundId = id; dueSoundName = fileName; }
                    else { completedSoundId = id; completedSoundName = fileName; }
                    RefreshLabel(); PreferencesEdited();
                }
                catch (Exception error) { ShowPreferencesError(error is InvalidDataException ? error.Message : "효과음을 가져오지 못했어요."); AppPaths.Log(error); }
                finally { soundImports--; RefreshPreferencesState(); }
            });
            import.Name = kind == ReminderSound.Due ? "ImportDueSound" : "ImportCompletionSound";
            import.Width = DesignSystem.SettingsImportWidth;
            AutomationProperties.SetName(import, caption + " 효과음 파일 가져오기");
            var reset = Ui.Button("기본", () =>
            {
                stopSoundPreview();
                if (kind == ReminderSound.Due) { dueSoundId = null; dueSoundName = null; }
                else { completedSoundId = null; completedSoundName = null; }
                RefreshLabel(); PreferencesEdited();
            });
            reset.Name = kind == ReminderSound.Due ? "ResetDueSound" : "ResetCompletionSound";
            reset.Width = DesignSystem.SettingsResetWidth; reset.Classes.Add("settings-reset");
            AutomationProperties.SetName(reset, caption + " 기본 효과음 사용");
            foreach (var button in new[] { play, import, reset })
            { button.Classes.Add("compact"); button.Height = DesignSystem.SettingsControlHeight; }
            var file = Ui.Column(SettingsLabel(caption), label); file.Spacing = 4;
            return NotificationSettingRow(kind == ReminderSound.Due ? "DueSoundRow" : "CompletionSoundRow",
                file, Ui.Row(play, import, reset), gap: 24);
        }
        var soundRows = Ui.Column(SoundRow("스트레칭 알림", ReminderSound.Due), SoundRow("완료 알림", ReminderSound.Completed));
        soundRows.Spacing = DesignSystem.SettingsRowGap;
        var volumeLabel = SettingsLabel("알림 소리 크기");
        Ui.KeyboardFocusLabel(soundVolume, volumeLabel);
        AutomationProperties.SetLabeledBy(soundVolume, volumeLabel);
        AutomationProperties.SetName(soundVolume, "알림 소리 크기, 퍼센트");
        soundVolume.MinWidth = 0; soundVolume.VerticalAlignment = VerticalAlignment.Center;
        var volumeControls = new Grid { Name = "ReminderVolumeControls", Width = 280, ColumnDefinitions = new("*,12,48") };
        volumeControls.Children.Add(soundVolume);
        soundVolumeValue.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(soundVolumeValue, 2); volumeControls.Children.Add(soundVolumeValue);
        var volume = NotificationSettingRow("ReminderVolumeRow", volumeLabel, volumeControls);
        soundVolume.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            soundVolumeValue.Text = $"{(int)Math.Round(soundVolume.Value)}%";
            stopSoundPreview?.Invoke(); PreferencesEdited();
        };
        var soundGroup = Ui.Column(soundRows, volume, soundsEnabled);
        soundGroup.Spacing = 12; soundRows.Margin = new(0, 24, 0, 8);
        var fields = Ui.Column(position, soundGroup); fields.Spacing = 0;
        var body = Ui.Column(SettingsHeading("알림 설정"), fields);
        bubbleDirection.SelectionChanged += (_, _) => PreferencesEdited();
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
