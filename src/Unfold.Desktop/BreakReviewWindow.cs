using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class BreakReviewWindow : Window
{
    public BreakReview Review { get; private set; }
    private readonly Func<DateOnly, BreakReview> read;
    private readonly Func<string?> historyWarning;
    private readonly Func<BreakReview, Task<string?>> export;
    private readonly DateOnly today;
    private DateOnly endDay;
    private readonly TextBlock period = Ui.Text("", 17), summary = Ui.Text("", 22, Ui.Accent);
    private readonly TextBlock status = new() { Name = "ReviewStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock warningText = new() { Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel days = new() { Spacing = 8 };
    private readonly Button previous, next;
    public BreakReviewWindow(Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null)
    {
        read = readHistory; historyWarning = warning ?? (() => null); export = exportReview ?? SaveCsv;
        today = currentDay ?? DateOnly.FromDateTime(DateTime.Now); endDay = today; Review = read(endDay);
        Title = "나의 일주일 · Unfold"; Width = 620; Height = 650; MinWidth = 560; MinHeight = 600;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        previous = Ui.Button("이전 7일", () => { endDay = endDay.AddDays(-7); Refresh(); });
        next = Ui.Button("다음 7일", () => { endDay = endDay.AddDays(7); Refresh(); });
        var save = Ui.AsyncButton("CSV 내보내기", async () =>
        {
            try
            {
                var snapshot = Review; var path = await export(snapshot);
                if (path is not null) { status.Foreground = DesignSystem.Muted; status.Text = $"완료한 휴식 {snapshot.Entries.Count}회를 {Path.GetFileName(path)} 파일로 내보냈어요."; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { status.Foreground = DesignSystem.Error; status.Text = "회고를 내보내지 못했어요. 이 기기의 저장 가능한 위치를 선택해 다시 시도해 주세요."; }
        });
        var done = Ui.Button("닫기", Close); done.IsCancel = true;
        var explanation = Ui.Text("직접 완료한 휴식을 모았어요. 시간은 실제 활동을 측정한 값이 아닌, 루틴에 설정한 길이예요.", 13); explanation.TextWrapping = TextWrapping.Wrap;
        var local = Ui.Text("완료 당시 기기의 날짜를 기준으로 표시해요. 내보내기에는 화면에 보이는 7일만 포함돼요.", 12); local.TextWrapping = TextWrapping.Wrap;
        Content = Ui.Page(this, "나를 위해 만든 여유.", "직접 완료한 휴식을 일주일 단위로 살펴보세요.",
            Ui.Column(Ui.Card(Ui.Column(period, summary, explanation)), Ui.Card(days),
                Ui.Actions(previous, next, Ui.Quiet(Ui.Button("새로고침", Refresh))), local, warningText),
            Ui.Column(status, Ui.Actions(Ui.Quiet(done), Ui.Primary(save))), "기록 · 내보내기");
        Refresh();
    }
    private void Refresh()
    {
        Review = read(endDay); period.Text = $"{Review.StartDay:yyyy-MM-dd} — {Review.EndDay:yyyy-MM-dd}";
        summary.Text = $"휴식 {Review.Entries.Count}회 · {Review.TotalSeconds / 60}분 {Review.TotalSeconds % 60}초 · {Review.DaysWithBreaks}일";
        summary.TextWrapping = TextWrapping.Wrap;
        days.Children.Clear();
        foreach (var day in Review.Days)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,100,110") };
            var date = Ui.Text(day.Date.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR"))); var count = Ui.Text($"{day.Count}회"); var duration = Ui.Text($"{day.Seconds / 60}분 {day.Seconds % 60}초");
            Grid.SetColumn(count, 1); Grid.SetColumn(duration, 2); row.Children.Add(date); row.Children.Add(count); row.Children.Add(duration); days.Children.Add(row);
        }
        previous.IsEnabled = endDay > today.AddDays(-77); next.IsEnabled = endDay < today;
        status.Text = Review.Entries.Count == 0 ? "이 7일 동안 완료한 휴식이 없어요. 채워야 할 목표는 없으니 편하게 시작하세요." : "이 기기에 저장돼요. 내보내거나 공유할지는 직접 결정하세요.";
        status.Foreground = DesignSystem.Muted;
        warningText.Text = historyWarning();
    }
    private async Task<string?> SaveCsv(BreakReview snapshot)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "완료한 휴식 내보내기", DefaultExtension = "csv",
            SuggestedFileName = $"unfold-breaks-{snapshot.StartDay:yyyy-MM-dd}-{snapshot.EndDay:yyyy-MM-dd}.csv", ShowOverwritePrompt = true });
        if (file is null) return null;
        var path = file.TryGetLocalPath() ?? throw new InvalidOperationException("이 기기에 저장할 파일을 선택해 주세요.");
        var bytes = snapshot.Csv(); await Task.Run(() => AtomicFile.Write(path, bytes)); return path;
    }
}
