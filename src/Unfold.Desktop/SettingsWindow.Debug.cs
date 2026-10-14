using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed partial class SettingsWindow
{
    private readonly CheckBox debugToolsEnabled = new() { Name = "DebugToolsEnabled", Content = L.T("디버그 도구 사용") };
    private readonly TextBlock debugPreviewStatus = new() { Name = "DebugPreviewStatus", FontSize = DesignSystem.Caption,
        Foreground = DesignSystem.Muted, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private StackPanel? debugToolsBody;

    private Border BuildDebugSettingsCard()
    {
        debugToolsEnabled.IsCheckedChanged += (_, _) =>
        {
            if (restoringPreferences || updating) return;
            RefreshDebugToolsVisibility();
            if (debugToolsEnabled.IsChecked != true) runtime.CloseReminderPreview();
            PreferencesEdited();
        };
        AutomationProperties.SetName(debugToolsEnabled, L.T("디버그 도구 사용"));

        Button PreviewButton(string name, string text, PetNotice notice)
        {
            var button = Ui.AsyncButton(text, async () =>
            {
                try { await runtime.ShowReminderPreview(notice); RefreshDebugPreviewStatus(); }
                catch (Exception error)
                {
                    AppPaths.Log(error); debugPreviewStatus.Foreground = DesignSystem.Error;
                    debugPreviewStatus.Text = Ui.ErrorText(error);
                }
            });
            button.Name = name; button.Classes.Add("compact"); button.Height = DesignSystem.SettingsControlHeight;
            return button;
        }

        var advance = PreviewButton("DebugPreviewAdvance", L.T("5분 전 알림"), PetNotice.Advance);
        var invitation = PreviewButton("DebugPreviewInvitation", L.T("스트레칭 알림"), PetNotice.Invitation);
        var resting = PreviewButton("DebugPreviewResting", L.T("휴식 진행"), PetNotice.Resting);
        var completed = PreviewButton("DebugPreviewCompleted", L.T("완료 알림"), PetNotice.Completed);
        var close = Ui.Button(L.T("미리보기 종료"), () => { runtime.CloseReminderPreview(); RefreshDebugPreviewStatus(); });
        close.Name = "DebugPreviewClose"; close.Classes.Add("compact"); close.Height = DesignSystem.SettingsControlHeight;
        var actions = Ui.Actions(advance, invitation, resting, completed, close);
        actions.HorizontalAlignment = HorizontalAlignment.Left;

        debugPreviewStatus.Text = L.T("미리보기 대기 중");
        debugToolsBody = Ui.Column(actions, debugPreviewStatus); debugToolsBody.Spacing = DesignSystem.Space;
        var body = Ui.Column(SettingsHeading(L.T("디버그 도구")), debugToolsEnabled, debugToolsBody);
        body.Spacing = DesignSystem.Inset; body.Margin = new(DesignSystem.Inset);
        RefreshDebugToolsVisibility();
        return Card("SettingsDebugToolsCard", body, DesignSystem.Surface, new(28));
    }

    private void RefreshDebugToolsVisibility()
    {
        if (debugToolsBody is not null) debugToolsBody.IsVisible = debugToolsEnabled.IsChecked == true;
    }

    private void RefreshDebugPreviewStatus()
    {
        if (debugToolsBody is null) return;
        debugPreviewStatus.Foreground = DesignSystem.Muted;
        debugPreviewStatus.Text = runtime.PreviewNotice switch
        {
            PetNotice.Advance => L.T("5분 전 알림 미리보기 중"),
            PetNotice.Invitation => L.T("스트레칭 알림 미리보기 중"),
            PetNotice.Resting => L.T("휴식 진행 미리보기 중"),
            PetNotice.Completed => L.T("완료 알림 미리보기 중"),
            _ => L.T("미리보기 대기 중")
        };
    }
}
