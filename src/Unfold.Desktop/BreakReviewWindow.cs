using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Unfold.Core;

namespace Unfold.Desktop;

public sealed class BreakReviewWindow : Window
{
    private readonly BreakReviewView view;
    public BreakReview Review => view.Review;

    public BreakReviewWindow(Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null, Func<DateOnly>? getToday = null)
    {
        Title = "나의 일주일 · Unfold"; Width = 620; Height = 650; MinWidth = 560; MinHeight = 600;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        view = new BreakReviewView(this, readHistory, warning, exportReview, currentDay, Close, getToday: getToday);
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
    private readonly Func<DateOnly> getToday;
    private DateOnly today;
    private DateOnly endDay;
    private readonly TextBlock period = Ui.Text("", 17), summary = Ui.Text("", 22, Ui.Accent);
    private readonly TextBlock status = new() { Name = "ReviewStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock warningText = new() { Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Grid days = new() { RowSpacing = 8 };
    private readonly HashSet<DateOnly> expandedDates = [];
    private readonly List<ToggleButton> dateHeaders = [];
    private readonly Action? returnToTimer;
    private readonly Button previous, next;

    public BreakReviewView(Window owner, Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null, Action? close = null,
        bool showHeader = true, Func<DateOnly>? getToday = null)
    {
        this.owner = owner; read = readHistory; historyWarning = warning ?? (() => null); export = exportReview ?? SaveCsv;
        returnToTimer = close ?? ReturnToSettingsTimer;
        this.getToday = getToday ?? (() => currentDay ?? DateOnly.FromDateTime(DateTime.Now));
        today = this.getToday(); endDay = today; Review = read(endDay);
        previous = Ui.Button("이전 7일", () => Navigate(-7));
        next = Ui.Button("다음 7일", () => Navigate(7));
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
        var explanation = Ui.Text("직접 완료한 휴식을 모았어요. 실제 시간이 없는 구형 기록은 당시 목표 시간을 합산해요.", 13); explanation.TextWrapping = TextWrapping.Wrap;
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
            Ui.Column(status, Ui.Actions(footerActions.ToArray())), "기록 · 내보내기", showHeader);
        Refresh();
    }

    public void Refresh()
    {
        UpdateToday();
        Review = read(endDay); period.Text = $"{Review.StartDay:yyyy-MM-dd} — {Review.EndDay:yyyy-MM-dd}";
        summary.Text = $"휴식 {Review.Entries.Count}회 · {Review.TotalSeconds / 60}분 {Review.TotalSeconds % 60}초 · {Review.DaysWithBreaks}일";
        summary.TextWrapping = TextWrapping.Wrap;
        days.Children.Clear(); days.RowDefinitions.Clear(); dateHeaders.Clear();
        expandedDates.IntersectWith(Review.Days.Where(day => day.Count > 0).Select(day => day.Date));
        if (Review.Entries.Count == 0)
        {
            days.RowDefinitions.Add(new(GridLength.Auto));
            var empty = Ui.Column(Ui.Text(endDay == today ? "이 7일 동안 완료한 휴식이 없어요." : "이 기간에는 완료한 휴식이 없어요."));
            empty.Name = "ReviewEmptyState";
            if (endDay == today)
            {
                var reassurance = Ui.Text("첫 휴식은 언제든 괜찮아요.", DesignSystem.Caption, DesignSystem.TextTertiary);
                var back = Ui.Quiet(Ui.Button("타이머로 돌아가기", () => returnToTimer?.Invoke())); back.Name = "ReviewReturnToTimer";
                empty.Children.Add(reassurance); empty.Children.Add(back);
            }
            days.Children.Add(empty);
        }
        else
        {
            var row = 0;
            foreach (var day in Review.Days.Reverse())
            {
                days.RowDefinitions.Add(new(GridLength.Auto));
                var rowContent = day.Count == 0 ? CreateEmptyDateRow(day) : CreateExpandableDateRow(day);
                Grid.SetRow(rowContent, row++); days.Children.Add(rowContent);
            }
        }
        previous.IsEnabled = endDay > today.AddDays(-77); next.IsEnabled = endDay < today;
        status.Text = Review.Entries.Count == 0 ? "채워야 할 목표는 없어요." : "이 기기에 저장돼요. 내보내거나 공유할지는 직접 결정하세요.";
        status.Foreground = DesignSystem.Muted;
        warningText.Text = historyWarning();
    }

    private Control CreateEmptyDateRow(BreakDay day)
    {
        var header = DateHeader(day, false, out _); header.Name = $"ReviewDate_{day.Date:yyyyMMdd}";
        return new Border { Padding = new(10, 6), Child = header };
    }

    private Control CreateExpandableDateRow(BreakDay day)
    {
        var details = new StackPanel { Name = $"ReviewDetails_{day.Date:yyyyMMdd}", Spacing = 8, Margin = new(12, 4, 12, 10) };
        foreach (var entry in Review.Entries.Where(entry => DateOnly.FromDateTime(entry.CompletedAt.Date) == day.Date)
            .OrderByDescending(entry => entry.CompletedAt).ThenBy(entry => entry.SessionId))
            details.Children.Add(DetailRow(entry));

        var header = DateHeader(day, true, out var chevron);
        var toggle = new ToggleButton
        {
            Name = $"ReviewDateHeader_{day.Date:yyyyMMdd}", Content = header, IsChecked = expandedDates.Contains(day.Date),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new(0), Padding = new(10, 8)
        };
        AddExpandedHeaderStyles(toggle);
        var content = new StackPanel { Spacing = 0 };
        content.Children.Add(toggle); content.Children.Add(details);
        void UpdateExpansion()
        {
            var expanded = toggle.IsChecked == true;
            details.IsVisible = expanded; chevron.Text = expanded ? "▴" : "▾";
            if (expanded) expandedDates.Add(day.Date); else expandedDates.Remove(day.Date);
            AutomationProperties.SetName(toggle, DateAutomationName(day, expanded));
        }
        toggle.IsCheckedChanged += (_, _) => UpdateExpansion();
        toggle.KeyDown += (_, e) => HandleDateHeaderKey(toggle, e);
        dateHeaders.Add(toggle); UpdateExpansion();
        return content;
    }

    private static void AddExpandedHeaderStyles(ToggleButton toggle)
    {
        static Style PresenterStyle(Func<Selector?, Selector> selector, IBrush background) =>
            new(selector) { Setters = { new Setter(ContentPresenter.BackgroundProperty, background) } };

        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Brushes.Transparent));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked").Class(":pointerover")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.Hover));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked").Class(":pressed")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.OutlineSubtle));
    }

    private static Grid DateHeader(BreakDay day, bool expandable, out TextBlock chevron)
    {
        var grid = new Grid { ColumnDefinitions = new("*,Auto,Auto,Auto"), ColumnSpacing = 12 };
        var date = Ui.Text(day.Date.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR")));
        var count = Ui.Text($"{day.Count}회");
        var duration = Ui.Text($"{day.Seconds / 60}분 {day.Seconds % 60}초");
        chevron = Ui.Text(expandable ? "▾" : ""); chevron.Foreground = DesignSystem.TextTertiary;
        Grid.SetColumn(count, 1); Grid.SetColumn(duration, 2); Grid.SetColumn(chevron, 3);
        grid.Children.Add(date); grid.Children.Add(count); grid.Children.Add(duration); grid.Children.Add(chevron);
        return grid;
    }

    private static Grid DetailRow(CompletedBreak entry)
    {
        var grid = new Grid { Name = $"ReviewEntry_{entry.SessionId:N}", ColumnDefinitions = new("*,Auto,Auto"), ColumnSpacing = 12 };
        var routine = Ui.Text(entry.RoutineName ?? entry.RoutineId); routine.Name = $"ReviewEntryRoutine_{entry.SessionId:N}";
        routine.TextWrapping = TextWrapping.Wrap;
        var completed = Ui.Text($"{entry.CompletedAt.ToString("HH:mm", CultureInfo.InvariantCulture)} 완료", DesignSystem.Caption, DesignSystem.TextTertiary);
        var duration = Ui.Text(entry.ActualSeconds is int actual
            ? $"실제 휴식 {Duration(actual)}"
            : $"실제 시간 미기록 · 당시 목표 {Duration(entry.Seconds)}", DesignSystem.Caption, DesignSystem.TextTertiary);
        duration.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(completed, 1); Grid.SetColumn(duration, 2);
        grid.Children.Add(routine); grid.Children.Add(completed); grid.Children.Add(duration);
        return grid;
    }

    private static string Duration(int seconds) => seconds < 60 ? $"{seconds}초" : $"{seconds / 60}분 {seconds % 60:00}초";

    private static string DateAutomationName(BreakDay day, bool expanded) =>
        $"{day.Date.ToString("M월 d일 dddd", CultureInfo.GetCultureInfo("ko-KR"))}, 휴식 {day.Count}회, 기록된 시간 {Duration(day.Seconds)}, {(expanded ? "펼침" : "접힘")}";

    private void HandleDateHeaderKey(ToggleButton header, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                header.IsChecked = header.IsChecked != true; e.Handled = true; break;
            case Key.Left:
                header.IsChecked = false; e.Handled = true; break;
            case Key.Right:
                header.IsChecked = true; e.Handled = true; break;
            case Key.Up:
            case Key.Down:
                var index = dateHeaders.IndexOf(header);
                var target = index + (e.Key == Key.Down ? 1 : -1);
                if (target >= 0 && target < dateHeaders.Count) dateHeaders[target].Focus(NavigationMethod.Directional);
                e.Handled = true; break;
        }
    }

    private void Navigate(int offset)
    {
        UpdateToday();
        var requested = endDay.AddDays(offset);
        endDay = requested > today ? today : requested < today.AddDays(-77) ? today.AddDays(-77) : requested;
        expandedDates.Clear(); Refresh();
    }

    private void UpdateToday()
    {
        var latest = getToday();
        if (endDay == today || endDay > latest) endDay = latest;
        today = latest;
    }

    private void ReturnToSettingsTimer()
    {
        var timer = owner.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "SettingsNavTimer");
        timer?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
