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
    private readonly BreakReviewView view;
    public BreakReview Review => view.Review;

    public BreakReviewWindow(Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null)
    {
        Title = "나의 일주일 · Unfold"; Width = 620; Height = 650; MinWidth = 560; MinHeight = 600;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        view = new BreakReviewView(this, readHistory, warning, exportReview, currentDay, Close);
        Content = Ui.PageFrame(this, view);
    }
}

internal sealed class BreakReviewView : UserControl
{
    private readonly Window owner;
    public BreakReview Review { get; private set; }
    private readonly Func<DateOnly, BreakReview> read;
    private readonly Func<string?> historyWarning;
    private readonly Func<BreakReview, Task<string?>> export;
    private readonly DateOnly today;
    private DateOnly endDay;
    private readonly TextBlock period = Ui.Text("", 17), summary = Ui.Text("", 22, Ui.Accent);
    private readonly TextBlock status = new() { Name = "ReviewStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock warningText = new() { Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Grid days = new() { ColumnDefinitions = new("*,100,110"), RowSpacing = 8 };
    private readonly Button previous, next;

    public BreakReviewView(Window owner, Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null, Action? close = null)
    {
        this.owner = owner; read = readHistory; historyWarning = warning ?? (() => null); export = exportReview ?? SaveCsv;
        today = currentDay ?? DateOnly.FromDateTime(DateTime.Now); endDay = today; Review = read(endDay);
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
        var explanation = Ui.Text("직접 완료한 휴식을 모았어요. 시간은 실제 활동을 측정한 값이 아닌, 루틴에 설정한 길이예요.", 13); explanation.TextWrapping = TextWrapping.Wrap;
        var local = Ui.Text("완료 당시 기기의 날짜를 기준으로 표시해요. 내보내기에는 화면에 보이는 7일만 포함돼요.", 12); local.TextWrapping = TextWrapping.Wrap;
        var footerActions = new List<Control>();
        if (close is not null)
        {
            var done = Ui.Button("닫기", close); done.IsCancel = true; footerActions.Add(Ui.Quiet(done));
        }
        footerActions.Add(Ui.Primary(save));
        Content = Ui.PageContent("나를 위해 만든 여유.", "직접 완료한 휴식을 일주일 단위로 살펴보세요.",
            Ui.Column(Ui.Card(Ui.Column(period, summary, explanation)), Ui.Card(days),
                Ui.Actions(previous, next, Ui.Quiet(Ui.Button("새로고침", Refresh))), local, warningText),
            Ui.Column(status, Ui.Actions(footerActions.ToArray())), "기록 · 내보내기");
        Refresh();
    }

    public void Refresh()
    {
        Review = read(endDay); period.Text = $"{Review.StartDay:yyyy-MM-dd} — {Review.EndDay:yyyy-MM-dd}";
        summary.Text = $"휴식 {Review.Entries.Count}회 · {Review.TotalSeconds / 60}분 {Review.TotalSeconds % 60}초 · {Review.DaysWithBreaks}일";
        summary.TextWrapping = TextWrapping.Wrap;
        days.Children.Clear(); days.RowDefinitions.Clear();
        for (var row = 0; row < Review.Days.Count; row++)
        {
            var day = Review.Days[row]; days.RowDefinitions.Add(new(GridLength.Auto));
            var date = Ui.Text(day.Date.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR"))); var count = Ui.Text($"{day.Count}회"); var duration = Ui.Text($"{day.Seconds / 60}분 {day.Seconds % 60}초");
            Grid.SetRow(date, row); Grid.SetRow(count, row); Grid.SetRow(duration, row);
            Grid.SetColumn(count, 1); Grid.SetColumn(duration, 2); days.Children.Add(date); days.Children.Add(count); days.Children.Add(duration);
        }
        previous.IsEnabled = endDay > today.AddDays(-77); next.IsEnabled = endDay < today;
        status.Text = Review.Entries.Count == 0 ? "이 7일 동안 완료한 휴식이 없어요. 채워야 할 목표는 없으니 편하게 시작하세요." : "이 기기에 저장돼요. 내보내거나 공유할지는 직접 결정하세요.";
        status.Foreground = DesignSystem.Muted;
        warningText.Text = historyWarning();
    }

    private async Task<string?> SaveCsv(BreakReview snapshot)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new() { Title = "완료한 휴식 내보내기", DefaultExtension = "csv",
            SuggestedFileName = $"unfold-breaks-{snapshot.StartDay:yyyy-MM-dd}-{snapshot.EndDay:yyyy-MM-dd}.csv", ShowOverwritePrompt = true });
        if (file is null) return null;
        var path = file.TryGetLocalPath() ?? throw new InvalidOperationException("이 기기에 저장할 파일을 선택해 주세요.");
        var bytes = snapshot.Csv(); await Task.Run(() => AtomicFile.Write(path, bytes)); return path;
    }
}
