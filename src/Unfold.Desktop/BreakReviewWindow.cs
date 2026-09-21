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
    private readonly TextBlock period = new() { Name = "ReviewPeriod", FontSize = DesignSystem.Section,
        FontWeight = FontWeight.SemiBold, Foreground = DesignSystem.Cream, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock count = Ui.Text("", 32), duration = Ui.Text("", 28), activeDays = Ui.Text("", 32);
    private readonly TextBlock status = new() { Name = "ReviewStatus", FontSize = DesignSystem.Caption,
        Foreground = DesignSystem.Muted, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false };
    private readonly TextBlock warningText = new() { Name = "ReviewWarning", FontSize = DesignSystem.Caption,
        Foreground = DesignSystem.Error, TextWrapping = TextWrapping.Wrap };
    private readonly Grid days = new() { Name = "ReviewDays" };
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
        Name = "ReviewView";
        previous = PeriodButton("ReviewPrevious", "이전 7일", "M15,4 L7,12 L15,20 L17,18 L11,12 L17,6 Z", () => Navigate(-7));
        next = PeriodButton("ReviewNext", "다음 7일", "M7,4 L15,12 L7,20 L5,18 L11,12 L5,6 Z", () => Navigate(7));
        var refresh = Ui.Quiet(PeriodButton("ReviewRefresh", "새로고침", "M20,3 V10 H13 L15.6,7.4 A7,7 0 1 0 18.9,13 H21 A9,9 0 1 1 17,6 Z", Refresh));
        var save = Ui.AsyncButton("CSV 내보내기", async () =>
        {
            try
            {
                var snapshot = Review; var path = await export(snapshot);
                if (path is not null) { status.Foreground = DesignSystem.Muted; status.Text = $"완료한 휴식 {snapshot.Entries.Count}회를 {Path.GetFileName(path)} 파일로 내보냈어요."; status.IsVisible = true; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { status.Foreground = DesignSystem.Error; status.Text = "회고를 내보내지 못했어요."; status.IsVisible = true; }
        });
        save.Name = "ReviewExport"; save.Height = DesignSystem.ReviewControlHeight;
        AutomationProperties.SetName(save, "CSV 내보내기");
        save.Content = Ui.Row(Icon("M11,2 H13 V13 L17,9 L18.5,10.5 L12,17 L5.5,10.5 L7,9 L11,13 Z M3,16 H5 V20 H19 V16 H21 V22 H3 Z"),
            new TextBlock { Text = "CSV 내보내기", FontSize = DesignSystem.Body, VerticalAlignment = VerticalAlignment.Center });
        var footerActions = new List<Control>();
        if (close is not null)
        {
            var done = Ui.Button("닫기", close); done.IsCancel = true;
            done.Height = DesignSystem.ReviewControlHeight; footerActions.Add(Ui.Quiet(done));
        }
        footerActions.Add(Ui.Primary(save));
        var heading = new Grid { Name = "ReviewPeriodControls", ColumnDefinitions = new("*,Auto"), ColumnSpacing = DesignSystem.Gap };
        heading.Children.Add(period); var navigation = Ui.Row(previous, next, refresh);
        Grid.SetColumn(navigation, 1); heading.Children.Add(navigation);
        count.Name = "ReviewCount"; duration.Name = "ReviewDuration"; activeDays.Name = "ReviewActiveDays";
        duration.TextWrapping = TextWrapping.Wrap;
        var metrics = new Grid { ColumnDefinitions = new("*,1.5*,*"), ColumnSpacing = 16 };
        metrics.Children.Add(Metric("완료한 휴식", count, "회"));
        var timeMetric = Metric("기록된 시간", duration); Grid.SetColumn(timeMetric, 1); metrics.Children.Add(timeMetric);
        var dayMetric = Metric("휴식한 날", activeDays, "일"); Grid.SetColumn(dayMetric, 2); metrics.Children.Add(dayMetric);
        var summary = Ui.Column(heading, metrics); summary.Spacing = DesignSystem.Inset;
        var summaryCard = Ui.Card(summary, DesignSystem.Inset); summaryCard.Name = "ReviewSummaryCard";
        var dailyHeading = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = DesignSystem.Space };
        dailyHeading.Children.Add(new TextBlock { Text = "날짜별 기록", FontSize = DesignSystem.Section,
            FontWeight = FontWeight.SemiBold, Foreground = DesignSystem.Cream });
        var legend = Ui.Caption("완료 횟수 · 기록된 시간"); legend.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(legend, 1); dailyHeading.Children.Add(legend);
        var daily = Ui.Column(dailyHeading, days); daily.Spacing = DesignSystem.Space;
        var dailyCard = Ui.Card(daily, DesignSystem.Inset); dailyCard.Name = "ReviewDailyCard";
        var sections = Ui.Column(summaryCard, dailyCard, warningText); sections.Spacing = DesignSystem.Inset;
        sections.MaxWidth = DesignSystem.ReviewContentWidth; sections.HorizontalAlignment = HorizontalAlignment.Stretch;
        var body = new Grid { Name = "ReviewBody", ColumnDefinitions = new("*") };
        body.ColumnDefinitions[0].MaxWidth = DesignSystem.ReviewContentWidth; body.Children.Add(sections);
        var footer = new Grid { Name = "ReviewFooter", ColumnDefinitions = new("*,Auto"), ColumnSpacing = DesignSystem.Inset };
        footer.Children.Add(status); var actions = Ui.Actions(footerActions.ToArray());
        actions.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(actions, 1); footer.Children.Add(actions);
        Content = Ui.PageContent("나를 위해 만든 여유.", "",
            body, footer, "기록 · 내보내기", showHeader);
        Refresh();
    }

    public void Refresh()
    {
        UpdateToday();
        Review = read(endDay);
        var end = Review.EndDay.ToString(Review.StartDay.Year == Review.EndDay.Year ? "MM.dd" : "yyyy.MM.dd", CultureInfo.InvariantCulture);
        period.Text = $"{Review.StartDay:yyyy.MM.dd} — {end}";
        period.TextWrapping = TextWrapping.Wrap;
        count.Text = Review.Entries.Count.ToString(); duration.Text = $"{Review.TotalSeconds / 60}분 {Review.TotalSeconds % 60}초";
        activeDays.Text = Review.DaysWithBreaks.ToString();
        days.Children.Clear(); days.RowDefinitions.Clear(); dateHeaders.Clear();
        expandedDates.IntersectWith(Review.Days.Where(day => day.Count > 0).Select(day => day.Date));
        if (Review.Entries.Count == 0)
        {
            days.RowDefinitions.Add(new(GridLength.Auto));
            var empty = Ui.Column(Ui.Text(endDay == today ? "이 7일 동안 완료한 휴식이 없어요." : "이 기간에는 완료한 휴식이 없어요."));
            empty.Name = "ReviewEmptyState"; empty.Margin = new(0, 20);
            if (endDay == today)
            {
                var back = Ui.Quiet(Ui.Button("타이머로 돌아가기", () => returnToTimer?.Invoke())); back.Name = "ReviewReturnToTimer";
                back.Height = DesignSystem.ReviewControlHeight; back.HorizontalAlignment = HorizontalAlignment.Left;
                empty.Children.Add(back);
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
                var divider = new Border { BorderBrush = DesignSystem.Outline,
                    BorderThickness = row < Review.Days.Count - 1 ? new(0, 0, 0, 1) : new(0), Child = rowContent };
                Grid.SetRow(divider, row++); days.Children.Add(divider);
            }
        }
        previous.IsEnabled = endDay > today.AddDays(-77); next.IsEnabled = endDay < today;
        status.Text = ""; status.IsVisible = false; status.Foreground = DesignSystem.Muted;
        warningText.Text = historyWarning();
        warningText.IsVisible = !string.IsNullOrWhiteSpace(warningText.Text);
    }

    private Control CreateEmptyDateRow(BreakDay day)
    {
        var header = DateHeader(day, false, out _); header.Name = $"ReviewDate_{day.Date:yyyyMMdd}";
        foreach (var text in header.Children.OfType<TextBlock>()) text.Foreground = DesignSystem.Muted;
        return new Border { Padding = new(0, 8), MinHeight = DesignSystem.ReviewDateHeight, Child = header };
    }

    private Control CreateExpandableDateRow(BreakDay day)
    {
        var details = new StackPanel { Name = $"ReviewDetails_{day.Date:yyyyMMdd}", Spacing = DesignSystem.Gap, Margin = new(0, 4, 0, 12) };
        foreach (var entry in Review.Entries.Where(entry => DateOnly.FromDateTime(entry.CompletedAt.Date) == day.Date)
            .OrderByDescending(entry => entry.CompletedAt).ThenBy(entry => entry.SessionId))
            details.Children.Add(DetailRow(entry));

        var header = DateHeader(day, true, out var chevron);
        var toggle = new ToggleButton
        {
            Name = $"ReviewDateHeader_{day.Date:yyyyMMdd}", Content = header, IsChecked = expandedDates.Contains(day.Date),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new(0), Padding = new(0, 8),
            MinHeight = DesignSystem.ReviewDateHeight
        };
        AddDateHeaderStyles(toggle);
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

    private static void AddDateHeaderStyles(ToggleButton toggle)
    {
        // Fluent scales the ToggleButton itself on press, independently of its presenter.
        toggle.RenderTransform = null;
        toggle.Transitions = null;
        static Style PresenterStyle(Func<Selector?, Selector> selector, IBrush background) =>
            new(selector) { Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, background),
                new Setter(ContentPresenter.TransitionsProperty, null)
            } };

        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>()
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Brushes.Transparent));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":pointerover")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.Hover));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":pressed")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Brushes.Transparent));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":pointerover").Class(":pressed")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.Hover));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Brushes.Transparent));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked").Class(":pointerover")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.Hover));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked").Class(":pressed")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Brushes.Transparent));
        toggle.Styles.Add(PresenterStyle(s => s.OfType<ToggleButton>().Class(":checked").Class(":pointerover").Class(":pressed")
            .Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), DesignSystem.Hover));
    }

    private static Grid DateHeader(BreakDay day, bool expandable, out TextBlock chevron)
    {
        var grid = new Grid { ColumnDefinitions = new("*,48,116,16"), ColumnSpacing = DesignSystem.Gap };
        var date = Ui.Text(day.Date.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR")));
        var count = Ui.Text($"{day.Count}회");
        var duration = Ui.Text($"{day.Seconds / 60}분 {day.Seconds % 60}초");
        count.TextAlignment = duration.TextAlignment = TextAlignment.Right;
        chevron = Ui.Text(expandable ? "▾" : ""); chevron.Foreground = DesignSystem.TextTertiary;
        Grid.SetColumn(count, 1); Grid.SetColumn(duration, 2); Grid.SetColumn(chevron, 3);
        grid.Children.Add(date); grid.Children.Add(count); grid.Children.Add(duration); grid.Children.Add(chevron);
        return grid;
    }

    private static Border DetailRow(CompletedBreak entry)
    {
        var completed = Ui.Text($"{entry.CompletedAt.ToString("HH:mm", CultureInfo.InvariantCulture)} 완료", DesignSystem.Caption, DesignSystem.TextTertiary);
        var duration = Ui.Text(entry.ActualSeconds is int actual
            ? $"실제 휴식 {Duration(actual)}"
            : "실제 휴식 시간 미기록", DesignSystem.Caption, DesignSystem.TextTertiary);
        var values = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = DesignSystem.Gap, LineSpacing = 4 };
        values.Children.Add(completed); values.Children.Add(duration);
        return new Border { Name = $"ReviewEntry_{entry.SessionId:N}", BorderBrush = DesignSystem.Outline,
            BorderThickness = new(2, 0, 0, 0), Padding = new(12, 0), Child = values };
    }

    private static StackPanel Metric(string label, TextBlock value, string? unit = null)
    {
        Control number = value;
        if (unit is not null) number = Ui.Row(value, new TextBlock { Text = unit, FontSize = DesignSystem.Body,
            Foreground = DesignSystem.Muted, VerticalAlignment = VerticalAlignment.Bottom, Margin = new(0, 0, 0, 4) });
        var metric = Ui.Column(Ui.Text(label, DesignSystem.Body, DesignSystem.Muted), number);
        metric.Spacing = 4; return metric;
    }

    private static PathIcon Icon(string path) => new() { Width = 16, Height = 16, Data = Geometry.Parse(path) };

    private static Button PeriodButton(string name, string label, string path, Action action)
    {
        var button = Ui.Button(label, action); button.Name = name; button.Content = Icon(path);
        button.Width = button.Height = DesignSystem.ReviewControlHeight; button.Padding = new(10);
        AutomationProperties.SetName(button, label); ToolTip.SetTip(button, label); ToolTip.SetShowDelay(button, 500);
        return button;
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
