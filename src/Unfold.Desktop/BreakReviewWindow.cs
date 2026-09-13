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
    private readonly TextBlock warningText = new() { Foreground = Brushes.LightSalmon, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel days = new() { Spacing = 8 };
    private readonly Button previous, next;
    public BreakReviewWindow(Func<DateOnly, BreakReview> readHistory, Func<string?>? warning = null,
        Func<BreakReview, Task<string?>>? exportReview = null, DateOnly? currentDay = null)
    {
        read = readHistory; historyWarning = warning ?? (() => null); export = exportReview ?? SaveCsv;
        today = currentDay ?? DateOnly.FromDateTime(DateTime.Now); endDay = today; Review = read(endDay);
        Title = "Your week · Unfold"; Width = 620; Height = 650; MinWidth = 560; MinHeight = 600;
        Background = Ui.Background; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        previous = Ui.Button("Previous 7 days", () => { endDay = endDay.AddDays(-7); Refresh(); });
        next = Ui.Button("Next 7 days", () => { endDay = endDay.AddDays(7); Refresh(); });
        var save = Ui.AsyncButton("Export CSV", async () =>
        {
            try
            {
                var snapshot = Review; var path = await export(snapshot);
                if (path is not null) { status.Foreground = Brushes.LightGray; status.Text = $"Exported {snapshot.Entries.Count} confirmed {BreakWord(snapshot.Entries.Count)} to {Path.GetFileName(path)}."; }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            { status.Foreground = Brushes.LightSalmon; status.Text = "Could not export your review. Please choose a writable local file and try again."; }
        });
        var done = Ui.Button("Done", Close); done.IsCancel = true;
        var explanation = Ui.Text("Moments you chose to confirm. Time is the planned routine length, not measured physical activity.", 13); explanation.TextWrapping = TextWrapping.Wrap;
        var local = Ui.Text("Dates follow the local day recorded at completion. Export includes only the seven days shown.", 12); local.TextWrapping = TextWrapping.Wrap;
        Content = new ScrollViewer { Content = new Border { Padding = new Thickness(24), Child = Ui.Column(
            Ui.Text("Room you made for yourself.", 25), period, summary, explanation,
            new Border { Padding = new Thickness(16), Background = Ui.Panel, CornerRadius = new CornerRadius(12), Child = days },
            Ui.Row(previous, next, Ui.Button("Refresh", Refresh)), local, warningText, status, Ui.Row(save, done)) } };
        Refresh();
    }
    private void Refresh()
    {
        Review = read(endDay); period.Text = $"{Review.StartDay:yyyy-MM-dd} — {Review.EndDay:yyyy-MM-dd}";
        summary.Text = $"{Review.Entries.Count} {BreakWord(Review.Entries.Count)} · {Review.TotalSeconds / 60}m {Review.TotalSeconds % 60}s · {Review.DaysWithBreaks} {(Review.DaysWithBreaks == 1 ? "day" : "days")}";
        summary.TextWrapping = TextWrapping.Wrap;
        days.Children.Clear();
        foreach (var day in Review.Days)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,100,110") };
            var date = Ui.Text(day.Date.ToString("MM-dd ddd")); var count = Ui.Text($"{day.Count} {BreakWord(day.Count)}"); var duration = Ui.Text($"{day.Seconds / 60}m {day.Seconds % 60}s");
            Grid.SetColumn(count, 1); Grid.SetColumn(duration, 2); row.Children.Add(date); row.Children.Add(count); row.Children.Add(duration); days.Children.Add(row);
        }
        previous.IsEnabled = endDay > today.AddDays(-77); next.IsEnabled = endDay < today;
        status.Text = Review.Entries.Count == 0 ? "No confirmed breaks in these seven days. There is no target to catch up with." : "Saved on this device. You decide whether to export or share.";
        status.Foreground = Brushes.LightGray;
        warningText.Text = historyWarning();
    }
    private static string BreakWord(int count) => count == 1 ? "break" : "breaks";
    private async Task<string?> SaveCsv(BreakReview snapshot)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export confirmed breaks", DefaultExtension = "csv",
            SuggestedFileName = $"unfold-breaks-{snapshot.StartDay:yyyy-MM-dd}-{snapshot.EndDay:yyyy-MM-dd}.csv", ShowOverwritePrompt = true });
        if (file is null) return null;
        var path = file.TryGetLocalPath() ?? throw new InvalidOperationException("Choose a local file.");
        var bytes = snapshot.Csv(); await Task.Run(() => AtomicFile.Write(path, bytes)); return path;
    }
}
