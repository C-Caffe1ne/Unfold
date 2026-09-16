using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private Border BuildSpeechSettingsCard()
    {
        var direction = new ComboBox { Name = "BubbleDirection", ItemsSource = Enum.GetValues<BubbleDirection>(),
            SelectedItem = runtime.Settings.BubbleDirection, HorizontalAlignment = HorizontalAlignment.Stretch };
        direction.ItemTemplate = new FuncDataTemplate<BubbleDirection>((value, _) => Ui.Text(value switch
        { BubbleDirection.Top => "위", BubbleDirection.Bottom => "아래", BubbleDirection.Left => "왼쪽", _ => "오른쪽" }));
        var snooze = new NumericUpDown { Name = "SnoozeMinutes", Minimum = 1, Maximum = 60, Increment = 1,
            Value = runtime.Settings.SnoozeMinutes, FormatString = "0", HorizontalAlignment = HorizontalAlignment.Stretch };
        var sounds = new CheckBox { Name = "ReminderSoundsEnabled", Content = "알림·완료 효과음", IsChecked = runtime.Settings.ReminderSoundsEnabled };
        var status = Ui.Caption("5분 전 안내 → 휴식 시작 → 완료"); status.Name = "SpeechSettingsStatus";
        AutomationProperties.SetName(direction, "말풍선 위치"); AutomationProperties.SetName(snooze, "다시 알릴 시간, 분");
        var save = Ui.AsyncButton("적용", async () =>
        {
            try
            {
                if (snooze.Value is not decimal minutes || minutes is < 1 or > 60 || decimal.Truncate(minutes) != minutes)
                    throw new ArgumentException("다시 알릴 시간은 1~60분의 정수로 입력해 주세요.");
                await runtime.UpdateSettings(runtime.Settings with { BubbleDirection = (BubbleDirection)direction.SelectedItem!,
                    SnoozeMinutes = (int)minutes, ReminderSoundsEnabled = sounds.IsChecked == true });
                status.Text = "말풍선 알림 설정을 저장했어요.";
            }
            catch (Exception error) { status.Text = "저장하지 못했어요. " + Ui.ErrorText(error); AppPaths.Log(error); }
        });
        save.Name = "ApplySpeechSettings"; Ui.Primary(save); save.Classes.Add("compact");
        var heading = new Grid { ColumnDefinitions = new("*,Auto") };
        heading.Children.Add(Ui.Text("말풍선 알림", 17)); Grid.SetColumn(save, 1); heading.Children.Add(save);
        Control SoundRow(string caption, ReminderSound kind)
        {
            var label = Ui.Caption("");
            void RefreshLabel() => label.Text = (kind == ReminderSound.Due ? runtime.Settings.ReminderSoundId : runtime.Settings.CompletionSoundId) is null ? "기본 효과음" : "가져온 WAV";
            RefreshLabel();
            var import = Ui.AsyncButton("파일…", async () =>
            {
                try
                {
                    var files = await StorageProvider.OpenFilePickerAsync(new() { Title = caption + " 효과음 가져오기", AllowMultiple = false,
                        FileTypeFilter = [new FilePickerFileType("WAV 효과음") { Patterns = ["*.wav"] }] });
                    if (files.FirstOrDefault()?.TryGetLocalPath() is not { } path) return;
                    var library = new ReminderSounds(Path.Combine(AppPaths.DataRoot, "Sounds"));
                    var id = await Task.Run(() => library.Import(path));
                    await runtime.UpdateSettings(kind == ReminderSound.Due ? runtime.Settings with { ReminderSoundId = id } : runtime.Settings with { CompletionSoundId = id });
                    RefreshLabel(); status.Text = caption + " 효과음을 저장했어요.";
                }
                catch (Exception error) { status.Text = error is InvalidDataException ? error.Message : "효과음을 가져오지 못했어요."; AppPaths.Log(error); }
            });
            import.Name = kind == ReminderSound.Due ? "ImportDueSound" : "ImportCompletionSound";
            AutomationProperties.SetName(import, caption + " 효과음 파일 가져오기");
            var reset = Ui.AsyncButton("기본", async () =>
            {
                try
                {
                    await runtime.UpdateSettings(kind == ReminderSound.Due ? runtime.Settings with { ReminderSoundId = null } : runtime.Settings with { CompletionSoundId = null });
                    RefreshLabel(); status.Text = "기본 효과음으로 바꿨어요.";
                }
                catch (Exception error) { status.Text = "효과음을 저장하지 못했어요."; AppPaths.Log(error); }
            });
            AutomationProperties.SetName(reset, caption + " 기본 효과음 사용");
            import.Classes.Add("compact"); reset.Classes.Add("compact");
            var row = new Grid { ColumnDefinitions = new("*,Auto,6,Auto") };
            row.Children.Add(Ui.Column(Ui.Text(caption, 12), label)); Grid.SetColumn(import, 1); row.Children.Add(import); Grid.SetColumn(reset, 3); row.Children.Add(reset);
            return row;
        }
        var body = Ui.Column(heading, Ui.Field("말풍선 위치", direction), Ui.Field("다시 알릴 시간 (분)", snooze), sounds,
            SoundRow("스트레칭 시간", ReminderSound.Due), SoundRow("완료", ReminderSound.Completed),
            Ui.Caption("WAV · 최대 30초 / 5 MiB"), status);
        body.Margin = new Thickness(20); body.Spacing = 10;
        return Card("SettingsSpeechCard", body, DesignSystem.Surface, new(28));
    }
}
