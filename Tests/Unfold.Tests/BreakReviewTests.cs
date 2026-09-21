using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

[Collection("Timer settings")]
public class BreakReviewTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static Button Button(Window window, string label) =>
        window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label) || AutomationProperties.GetName(button) == label);

    private static void Press(Window window, string label)
    {
        Button(window, label).RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Layout(window);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
    }

    private static void KeyPress(Window window, Key key, PhysicalKey physicalKey, string? text = null)
    {
        window.KeyPress(key, RawInputModifiers.None, physicalKey, text);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, text);
        Layout(window);
    }

    private static ToggleButton Header(Window window, DateOnly day) =>
        Find<ToggleButton>(window, $"ReviewDateHeader_{day:yyyyMMdd}");

    private static CompletedBreak Entry(string guid, DateOnly day, int hour, int minute, string routineId,
        string? routineName, int plannedSeconds, int? actualSeconds, TimeSpan? offset = null) =>
        new(new Guid(guid), new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, offset ?? TimeSpan.FromHours(9)),
            routineId, plannedSeconds, "default-cat", routineName, ActualSeconds: actualSeconds);

    private static BreakReview Review(DateOnly endDay, params CompletedBreak[] entries)
    {
        var start = endDay.AddDays(-6);
        var visible = entries.Where(entry =>
        {
            var date = DateOnly.FromDateTime(entry.CompletedAt.Date);
            return date >= start && date <= endDay;
        }).OrderBy(entry => entry.CompletedAt).ThenBy(entry => entry.SessionId).ToArray();
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = start.AddDays(offset);
            var daily = visible.Where(entry => DateOnly.FromDateTime(entry.CompletedAt.Date) == date).ToArray();
            return new BreakDay(date, daily.Length, daily.Sum(entry => entry.RecordedSeconds));
        }).ToArray();
        return new(start, endDay, days, visible);
    }

    [AvaloniaFact]
    public void ReviewDateRowShowsOnlyCompletionTimeAndActualDurationAndPreservesExportData()
    {
        var entry = Entry("00000000-0000-0000-0000-000000000001", Today, 9, 42, "small-reset", "잠깐의 여유", 60, 65,
            TimeSpan.FromHours(-7));
        var zero = Entry("00000000-0000-0000-0000-000000000002", Today, 8, 30, "zero", "바로 돌아온 휴식", 20, 0);
        var window = new BreakReviewWindow(_ => Review(Today, entry, zero), currentDay: Today);
        try
        {
            window.Show(); Layout(window);
            var header = Header(window, Today); var details = Find<StackPanel>(window, "ReviewDetails_20260917");
            Assert.False(header.IsChecked); Assert.False(details.IsVisible);
            header.Focus(); KeyPress(window, Key.Enter, PhysicalKey.Enter);
            Assert.True(header.IsChecked); Assert.True(details.IsVisible); Assert.True(header.IsFocused);
            var texts = details.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToArray();
            Assert.DoesNotContain("잠깐의 여유", texts); Assert.DoesNotContain("바로 돌아온 휴식", texts);
            Assert.Equal(4, texts.Length); Assert.Contains("09:42 완료", texts); Assert.Contains("실제 휴식 1분 05초", texts);
            Assert.Contains("실제 휴식 0초", texts);
            Assert.Contains("잠깐의 여유", System.Text.Encoding.UTF8.GetString(window.Review.Csv()));
            Assert.Contains("펼침", AutomationProperties.GetName(header));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ExpandedDateHeaderTextKeepsMinimumContrastAgainstEffectiveBackground()
    {
        var entry = Entry("00000000-0000-0000-0000-000000000071", Today, 9, 42,
            "small-reset", "잠깐의 여유", 60, 65);
        var window = new BreakReviewWindow(_ => Review(Today, entry), currentDay: Today);
        try
        {
            window.Show(); Layout(window);
            var header = Header(window, Today); header.IsChecked = true; Layout(window);
            var content = Assert.IsType<Grid>(header.Content);
            var labels = content.Children.OfType<TextBlock>().Where(text => text.Text is not "▴" and not "▾").ToArray();
            Assert.Equal(3, labels.Length);
            foreach (var label in labels)
            {
                var background = EffectiveBackground(label);
                var contrast = Contrast(Assert.IsAssignableFrom<IBrush>(label.Foreground), background);
                Assert.True(contrast >= 4.5,
                    $"Expanded header '{label.Text}' contrast was {contrast:F2}:1; foreground={label.Foreground}, background={background}.");
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewDateRowMarksMissingActualTimeAndKeepsLegacyTargetInSummaryAndExport()
    {
        var entry = Entry("00000000-0000-0000-0000-000000000002", Today, 14, 18, "legacy-routine", null, 20, null);
        var window = new BreakReviewWindow(_ => Review(Today, entry), currentDay: Today);
        try
        {
            window.Show(); Layout(window); Header(window, Today).IsChecked = true; Layout(window);
            var texts = Find<StackPanel>(window, "ReviewDetails_20260917").GetVisualDescendants().OfType<TextBlock>()
                .Select(text => text.Text).ToArray();
            Assert.Equal(2, texts.Length); Assert.Contains("14:18 완료", texts);
            Assert.Contains("실제 휴식 시간 미기록", texts);
            Assert.DoesNotContain("legacy-routine", texts);
            Assert.DoesNotContain("실제 휴식 20초", texts);
            Assert.Equal(20, window.Review.TotalSeconds);
            Assert.Contains("legacy-routine\",20,", System.Text.Encoding.UTF8.GetString(window.Review.Csv()));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewDateRowsSortNewestFirstAndEntriesLatestFirst()
    {
        var newest = Entry("00000000-0000-0000-0000-000000000010", Today, 8, 0, "newest", "최신 날짜", 20, 20);
        var earlierDay = Today.AddDays(-2);
        var late = Entry("00000000-0000-0000-0000-000000000030", earlierDay, 15, 0, "late", "나중 기록", 20, 20);
        var tieFirst = Entry("00000000-0000-0000-0000-000000000001", earlierDay, 11, 0, "tie-first", "같은 시각 첫째", 20, 20);
        var tieSecond = Entry("00000000-0000-0000-0000-000000000002", earlierDay, 11, 0, "tie-second", "같은 시각 둘째", 20, 20);
        var window = new BreakReviewWindow(_ => Review(Today, tieSecond, late, newest, tieFirst), currentDay: Today);
        try
        {
            window.Show(); Layout(window);
            var dates = window.GetVisualDescendants().OfType<TextBlock>()
                .Select(text => text.Text).OfType<string>().Where(text => text.StartsWith("9월 ", StringComparison.Ordinal)).ToArray();
            Assert.Equal(["9월 17일 (목)", "9월 16일 (수)", "9월 15일 (화)", "9월 14일 (월)",
                "9월 13일 (일)", "9월 12일 (토)", "9월 11일 (금)"], dates);
            var headers = window.GetVisualDescendants().OfType<ToggleButton>()
                .Where(header => header.Name?.StartsWith("ReviewDateHeader_", StringComparison.Ordinal) == true).ToArray();
            Assert.Equal(["ReviewDateHeader_20260917", "ReviewDateHeader_20260915"], headers.Select(header => header.Name));
            var details = Find<StackPanel>(window, "ReviewDetails_20260915");
            Assert.Equal([
                "ReviewEntry_00000000000000000000000000000030",
                "ReviewEntry_00000000000000000000000000000001",
                "ReviewEntry_00000000000000000000000000000002"
            ], details.Children.OfType<Border>().Select(row => row.Name));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewEmptyCurrentWeekOffersReturnToTimerWithoutHidingPeriodNavigation()
    {
        BreakReview Current(DateOnly end) => Review(end);
        var window = new BreakReviewWindow(Current, exportReview: _ => Task.FromResult<string?>("empty.csv"), currentDay: Today);
        try
        {
            window.Show(); Layout(window);
            Assert.NotNull(Button(window, "타이머로 돌아가기"));
            Assert.True(Button(window, "이전 7일").IsVisible); Assert.True(Button(window, "CSV 내보내기").IsVisible);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "첫 휴식은 언제든 괜찮아요.");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewEmptyPastWeekDoesNotOfferStartAction()
    {
        var window = new BreakReviewWindow(end => Review(end), currentDay: Today);
        try
        {
            window.Show(); Layout(window); Press(window, "이전 7일");
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "타이머로 돌아가기"));
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "이 기간에는 완료한 휴식이 없어요.");
            Assert.True(Button(window, "다음 7일").IsEnabled); Assert.True(Button(window, "CSV 내보내기").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewExpansionPersistsOnRefreshAndResetsAcrossPeriods()
    {
        BreakReview Read(DateOnly end) => Review(end,
            Entry("00000000-0000-0000-0000-000000000041", end, 9, 0, "routine", "날짜별 기록", 20, 20));
        var window = new BreakReviewWindow(Read, currentDay: Today);
        try
        {
            window.Show(); Layout(window); Header(window, Today).IsChecked = true; Layout(window);
            Press(window, "새로고침"); Assert.True(Header(window, Today).IsChecked);
            Press(window, "이전 7일"); Assert.False(Header(window, Today.AddDays(-7)).IsChecked);
            Press(window, "다음 7일"); Assert.False(Header(window, Today).IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewDateHeadersToggleWithEnterSpaceAndArrowKeys()
    {
        var olderDay = Today.AddDays(-2);
        var window = new BreakReviewWindow(_ => Review(Today,
            Entry("00000000-0000-0000-0000-000000000051", Today, 9, 0, "new", "최신", 20, 20),
            Entry("00000000-0000-0000-0000-000000000052", olderDay, 9, 0, "old", "과거", 20, 20)), currentDay: Today);
        try
        {
            window.Show(); Layout(window); var newest = Header(window, Today); var older = Header(window, olderDay);
            newest.Focus(); KeyPress(window, Key.Enter, PhysicalKey.Enter); Assert.True(newest.IsChecked);
            KeyPress(window, Key.Left, PhysicalKey.ArrowLeft); Assert.False(newest.IsChecked);
            KeyPress(window, Key.Right, PhysicalKey.ArrowRight); Assert.True(newest.IsChecked);
            KeyPress(window, Key.Down, PhysicalKey.ArrowDown); Assert.True(older.IsFocused);
            KeyPress(window, Key.Space, PhysicalKey.Space, " "); Assert.True(older.IsChecked);
            KeyPress(window, Key.Up, PhysicalKey.ArrowUp); Assert.True(newest.IsFocused);
            Assert.DoesNotContain(window.GetVisualDescendants().OfType<ToggleButton>(),
                header => header.Name == $"ReviewDateHeader_{Today.AddDays(-1):yyyyMMdd}");
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewDateHeaderPressKeepsHoverAppearanceWithoutTransitions()
    {
        var entry = Entry("00000000-0000-0000-0000-000000000053", Today, 9, 0, "press", "클릭", 20, 20);
        var window = new BreakReviewWindow(_ => Review(Today, entry), currentDay: Today);
        try
        {
            window.Show(); Layout(window);
            var header = Header(window, Today);
            var presenter = header.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");
            var point = header.TranslatePoint(new Point(12, 12), window)!.Value;
            window.MouseMove(point); Layout(window);
            var hover = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background);
            var bounds = header.Bounds;
            void AssertNoMotion()
            {
                Assert.True(header.Transitions is null || header.Transitions.Count == 0);
                Assert.True(presenter.Transitions is null || presenter.Transitions.Count == 0);
                Assert.Equal(Matrix.Identity, header.RenderTransform?.Value ?? Matrix.Identity);
                Assert.Equal(Matrix.Identity, presenter.RenderTransform?.Value ?? Matrix.Identity);
                Assert.Equal(bounds, header.Bounds);
            }
            AssertNoMotion();
            // Exercise both unchecked and checked press styles, including release.
            foreach (var expanded in new[] { true, false })
            {
                window.MouseDown(point, MouseButton.Left); Layout(window);
                var pressed = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background);
                Assert.Equal(hover.Color, pressed.Color); AssertNoMotion();
                window.MouseUp(point, MouseButton.Left); Layout(window);
                Assert.Equal(expanded, header.IsChecked); AssertNoMotion();
            }
            window.MouseMove(new Point(0, 0)); header.Focus(); Layout(window);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " "); Layout(window);
            AssertNoMotion();
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " "); Layout(window);
            Assert.True(header.IsChecked);
            AssertNoMotion();
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReviewDetailKeepsKoreanWeekdayAndHidesLongRoutineNameAtMinimumSizes()
    {
        var longName = string.Concat(Enumerable.Repeat("긴이름", 20));
        var entry = Entry("00000000-0000-0000-0000-000000000061", Today, 16, 30, "long-routine", longName, 60, 65);
        var window = new BreakReviewWindow(_ => Review(Today, entry), currentDay: Today) { Width = 560, Height = 600 };
        try
        {
            window.Show(); Layout(window); Header(window, Today).IsChecked = true; Layout(window);
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "9월 17일 (목)");
            AssertDetailFits(window, entry.SessionId, longName);
        }
        finally { window.Close(); }

        using var scope = new SettingsReviewScope(longName);
        scope.Window.Width = 860; scope.Window.Height = 680;
        Find<Button>(scope.Window, "SettingsNavReview").RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent)); Layout(scope.Window);
        var day = DateOnly.FromDateTime(DateTime.Now);
        Header(scope.Window, day).IsChecked = true; Layout(scope.Window);
        Assert.Contains(scope.Window.GetVisualDescendants().OfType<TextBlock>(), text =>
            text.Text == day.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR")));
        Assert.DoesNotContain(scope.Window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == longName);
        var row = scope.Window.GetVisualDescendants().OfType<Border>().Single(control => control.Name?.StartsWith("ReviewEntry_", StringComparison.Ordinal) == true);
        Assert.True(row.Bounds.Right <= Assert.IsAssignableFrom<Control>(row.Parent).Bounds.Width + 1);
    }

    private static void AssertDetailFits(Window window, Guid sessionId, string longName)
    {
        var row = Find<Border>(window, $"ReviewEntry_{sessionId:N}");
        var texts = row.GetVisualDescendants().OfType<TextBlock>().ToArray();
        Assert.Equal(2, texts.Length); Assert.DoesNotContain(texts, text => text.Text == longName);
        var completed = texts.Single(text => text.Text?.EndsWith(" 완료") == true);
        var duration = texts.Single(text => text.Text?.StartsWith("실제 휴식", StringComparison.Ordinal) == true);
        var first = completed.TranslatePoint(default, row)!.Value;
        var second = duration.TranslatePoint(default, row)!.Value;
        Assert.True(first.Y + completed.Bounds.Height <= second.Y + .5 || first.X + completed.Bounds.Width <= second.X + .5);
        Assert.True(second.X + duration.Bounds.Width <= row.Bounds.Width + .5);
    }

    [AvaloniaFact]
    public void ReviewSummaryControlsAndFooterFitWithExpandedRecordsAndExportFailure()
    {
        var entry = Entry("00000000-0000-0000-0000-000000000081", Today, 9, 42, "hidden-routine", "가볍게 몸 풀기", 60, 65);
        foreach (var size in new[] { new Size(1120, 800), new Size(860, 680), new Size(620, 650), new Size(560, 600) })
        {
            var tab = size.Width >= 860;
            var window = new Window { Width = size.Width, Height = size.Height };
            var view = new BreakReviewView(window, end => Review(end, entry),
                () => "일부 기록을 읽지 못했어요. 현재 확인할 수 있는 기록만 표시하고 있어요.",
                _ => throw new IOException("No space"), currentDay: Today, close: tab ? null : () => { }, showHeader: !tab);
            // Mirror the tab's rail/frame inset and the compatibility window's page frame.
            window.Content = tab ? new Border { Padding = new(111, 31, 31, 31), Child = view } : Ui.PageFrame(window, view);
            try
            {
                window.Show(); Layout(window); Header(window, Today).IsChecked = true; Layout(window);
                var cardWidth = Find<Border>(window, "ReviewSummaryCard").Bounds.Width;
                Assert.True(cardWidth <= 760.5, $"{size}: summary width {cardWidth}");
                Assert.Equal("1", Find<TextBlock>(window, "ReviewCount").Text);
                Assert.Equal("1분 5초", Find<TextBlock>(window, "ReviewDuration").Text);
                Assert.Equal("1", Find<TextBlock>(window, "ReviewActiveDays").Text);
                var period = Find<TextBlock>(window, "ReviewPeriod");
                foreach (var name in new[] { "ReviewPrevious", "ReviewNext", "ReviewRefresh" })
                {
                    var button = Find<Button>(window, name); Assert.IsType<PathIcon>(button.Content);
                    Assert.Equal(new Size(40, 40), button.Bounds.Size);
                    Assert.NotNull(ToolTip.GetTip(button));
                    Assert.True(button.TranslatePoint(default, window)!.Value.X > period.TranslatePoint(default, window)!.Value.X + period.Bounds.Width);
                }
                Press(window, "CSV 내보내기");
                Assert.Contains("내보내지 못했어요", Find<TextBlock>(window, "ReviewStatus").Text);
                Assert.True(Find<Button>(window, "ReviewExport").IsEnabled);
                var scroll = Find<ScrollViewer>(window, "PageBodyScroll");
                var footer = Find<Grid>(window, "ReviewFooter");
                var footerTop = footer.TranslatePoint(default, window)!.Value.Y;
                Assert.True(footerTop >= scroll.TranslatePoint(default, window)!.Value.Y + scroll.Bounds.Height);
                foreach (var name in new[] { "ReviewStatus", "ReviewExport" })
                {
                    var control = Find<Control>(window, name); var origin = control.TranslatePoint(default, window)!.Value;
                    Assert.True(origin.Y + control.Bounds.Height <= size.Height);
                    Assert.True(origin.X + control.Bounds.Width <= size.Width);
                }
                scroll.ScrollToEnd(); Layout(window);
                Assert.Equal(footerTop, footer.TranslatePoint(default, window)!.Value.Y);
                Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + .5);
                AssertDetailFits(window, entry.SessionId, entry.RoutineName!);
            }
            finally { window.Close(); }
        }
    }

    private static IBrush EffectiveBackground(Visual visual)
    {
        foreach (var candidate in visual.GetVisualAncestors())
        {
            IBrush? brush = candidate switch
            {
                ContentPresenter presenter => presenter.Background,
                Border border => border.Background,
                Panel panel => panel.Background,
                TemplatedControl control => control.Background,
                _ => null
            };
            if (brush is ISolidColorBrush solid && solid.Color.A > 0 && solid.Opacity > 0) return brush;
        }
        throw new InvalidOperationException("Expanded date header has no effective solid background.");
    }

    private static double Contrast(IBrush first, IBrush second)
    {
        static double Luminance(IBrush brush)
        {
            var color = ((ISolidColorBrush)brush).Color;
            static double Linear(byte channel)
            {
                var value = channel / 255d;
                return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
            }
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        }
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    private sealed class SettingsReviewScope : IDisposable
    {
        private readonly TempDirectory temp = new();
        private readonly string? previous = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
        private readonly ClassicDesktopStyleApplicationLifetime lifetime = new();
        private readonly AppRuntime runtime;
        public SettingsWindow Window { get; }

        public SettingsReviewScope(string routineName)
        {
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", temp.Path);
            var routine = new BreakRoutine("long-routine", routineName, [new("잠시 쉬어요.", 1)]);
            var session = new BreakSession(routine, "default-cat");
            session.Start(TimeSpan.Zero); session.Tick(TimeSpan.FromSeconds(1)); session.Complete();
            var history = new BreakHistory(); history.Add(session, DateTimeOffset.Now);
            history.Save(Path.Combine(temp.Path, "break-history.json"));
            runtime = new(lifetime); Window = new(runtime); Window.Show(); Layout(Window);
        }

        public void Dispose()
        {
            foreach (var dialog in Window.OwnedWindows.ToArray()) dialog.Close();
            Window.HideToTray(); Window.Dispose(); runtime.Dispose(); lifetime.Dispose();
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", previous); temp.Dispose();
        }
    }
}
