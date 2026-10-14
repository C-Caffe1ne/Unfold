using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Text.RegularExpressions;

namespace Unfold.Desktop;

internal sealed class UpdateWindow : Window
{
    private readonly AppUpdates updates;
    private readonly Func<Task<bool>> restart;
    private readonly TextBlock status = Ui.Text("", DesignSystem.Body);
    private readonly ProgressBar progress = new() { Name = "UpdateProgress", Minimum = 0, Maximum = 100, Height = 8, IsVisible = false };
    private readonly Button action = Ui.Primary(Ui.Action(L.T("업데이트 확인")));
    private readonly TextBlock notes = Ui.Text("", DesignSystem.Body);
    private readonly StackPanel releaseNotes = new() { Spacing = 8, IsVisible = false };
    private bool closed;
    internal UpdateWindow(AppUpdates updates, Func<Task<bool>> restart)
    {
        this.updates = updates; this.restart = restart;
        Title = L.T("Unfold 업데이트"); Width = 440; MinWidth = 320; MinHeight = 230; SizeToContent = SizeToContent.Height;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        status.Name = "UpdateStatus"; status.TextWrapping = TextWrapping.Wrap;
        notes.Name = "UpdateReleaseNotes"; notes.TextWrapping = TextWrapping.Wrap;
        releaseNotes.Children.Add(Ui.Text(L.T("변경내용"), DesignSystem.Section));
        releaseNotes.Children.Add(new ScrollViewer { Content = notes, MaxHeight = 220,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        action.Name = "UpdateAction";
        var close = Ui.Action(L.T("닫기")); close.IsCancel = true; close.Click += (_, _) => Close();
        action.Click += async (_, _) =>
        {
            if (updates.Downloaded)
            {
                action.IsEnabled = false;
                if (!await restart() && !closed)
                {
                    Refresh();
                    if (updates.State != AppUpdateState.Error) status.Text = L.T("진행 중인 휴식이나 저장하지 않은 작업을 마친 뒤 재시작해 주세요.");
                }
            }
            else if (updates.Release is not null) await updates.Download();
            else await updates.Check();
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(close); actions.Children.Add(action);
        var content = new StackPanel { Spacing = 20, Margin = new(24) };
        content.Children.Add(Ui.Text(L.T("Unfold 업데이트"), DesignSystem.Title));
        content.Children.Add(Ui.Text(L.F("현재 버전 · {0}", AppRelease.DisplayVersion), DesignSystem.Caption));
        content.Children.Add(status); content.Children.Add(releaseNotes); content.Children.Add(progress); content.Children.Add(actions);
        Content = new Border { Background = DesignSystem.Canvas, Child = content };
        AutomationProperties.SetName(progress, L.T("업데이트 다운로드 진행률"));
        updates.Changed += Changed; Closed += (_, _) => { closed = true; updates.Changed -= Changed; };
        Refresh();
    }
    private void Changed()
    {
        if (Dispatcher.UIThread.CheckAccess()) Refresh(); else Dispatcher.UIThread.Post(Refresh);
    }
    private void Refresh()
    {
        if (closed) return;
        status.Text = updates.State switch
        {
            AppUpdateState.UnsupportedInstall => L.T("이 설치본은 앱 내 업데이트를 지원하지 않아요. 업데이트 기능이 포함된 새 버전을 한 번 설치해 주세요."),
            AppUpdateState.Checking => L.T("새 버전을 확인하고 있어요."),
            AppUpdateState.Current => L.T("최신 버전을 사용하고 있어요."),
            AppUpdateState.Available => L.F("새 버전 {0}을 다운로드할 수 있어요.", updates.Release!.Version),
            AppUpdateState.Downloading => L.F("업데이트를 다운로드하고 있어요. {0}%", updates.Progress),
            AppUpdateState.Ready => L.T("업데이트 다운로드가 완료됐어요. 재시작하면 적용돼요."),
            AppUpdateState.Applying => L.T("업데이트를 적용하고 있어요."),
            AppUpdateState.Error => updates.Error,
            _ => L.T("새 버전을 확인할 수 있어요.")
        };
        progress.IsVisible = updates.State == AppUpdateState.Downloading; progress.Value = updates.Progress;
        releaseNotes.IsVisible = updates.Release is not null;
        notes.Text = PlainNotes(updates.Release?.NotesMarkdown);
        action.Content = updates.Downloaded ? L.T("재시작하여 적용") : updates.Release is not null ? L.T("업데이트 다운로드") : L.T("업데이트 확인");
        action.IsEnabled = !updates.Busy && updates.State != AppUpdateState.UnsupportedInstall;
    }
    private static string PlainNotes(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return L.T("자세한 변경내용은 Unfold 웹의 변경 이력에서 확인할 수 있어요.");
        // Keep the modal focused on user changes rather than the full release document.
        var text = Regex.Replace(markdown[..Math.Min(markdown.Length, 12000)], @"\r\n?", "\n");
        var section = Regex.Match(text, @"(?m)^#{1,6}\s+(?:핵심\s*변경|주요\s*변경|변경\s*내용|개선\s*사항)\s*$");
        if (section.Success)
        {
            text = text[(section.Index + section.Length)..];
            var nextSection = Regex.Match(text, @"(?m)^#{1,6}\s+");
            if (nextSection.Success) text = text[..nextSection.Index];
        }
        var items = Regex.Matches(text, @"(?m)^\s*(?:[-*+]|\d+[.)])\s+(.+)$")
            .Select(match => ShortNote(match.Groups[1].Value)).Where(note => note.Length > 0).Take(6).ToArray();
        if (items.Length > 0) return string.Join("\n", items.Select(note => "- " + note));
        var plain = ShortNote(Regex.Replace(text, @"(?m)^#{1,6}.*$", ""));
        return plain.Length > 0 ? plain : L.T("자세한 변경내용은 Unfold 웹의 변경 이력에서 확인할 수 있어요.");
    }
    private static string ShortNote(string note)
    {
        var title = Regex.Match(note, @"^\*\*(.+?)\*\*\s*[:：]\s*");
        note = Regex.Replace(note, @"!?\[([^\]]+)\]\([^)]+\)", "$1").Replace("**", "").Replace("`", "");
        note = Regex.Replace(note, @"\s+", " ").Trim();
        note = Regex.Split(note, @"(?<=[.!?。])\s+")[0];
        if (note.Length <= 100) return note;
        if (title.Success) note = title.Groups[1].Value.Trim();
        return note.Length <= 100 ? note : note[..99].TrimEnd() + "…";
    }
}
