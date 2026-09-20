using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class LocalizationTests
{
    [AvaloniaFact]
    public void ReviewUsesKoreanWeekdaysOnAnEnglishSystemAndPreservesOldExportNames()
    {
        var culture = CultureInfo.CurrentCulture; var uiCulture = CultureInfo.CurrentUICulture;
        BreakReviewWindow? window = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            // Simulate a completed routine from the English UI, before this change.
            var session = new BreakSession(new("look-away", "Look away", [new("Rest your eyes", 20)]), "default-cat");
            session.Start(TimeSpan.Zero); session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); session.Complete();
            var history = new BreakHistory(); history.Add(session, new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.FromHours(9)));
            using var temp = new TempDirectory(); var path = Path.Combine(temp.Path, "break-history.json"); history.Save(path);
            var loaded = BreakHistory.Load(path);
            window = new BreakReviewWindow(loaded.Review, currentDay: new DateOnly(2026, 9, 14)) { Width = 560, Height = 600 };
            window.Show(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            var text = window.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var monday = Assert.Single(text, block => block.Text == "9월 14일 (월)");
            var row = Assert.IsType<Grid>(monday.Parent);
            var count = row.Children.OfType<TextBlock>().Single(block => Grid.GetColumn(block) == 1 && Grid.GetRow(block) == Grid.GetRow(monday));
            Assert.True(monday.Bounds.Right <= count.Bounds.X);
            Assert.Equal("1회", count.Text);
            Assert.Contains(text, block => block.Name == "ReviewCount" && block.Text == "1");
            Assert.Contains(text, block => block.Name == "ReviewDuration" && block.Text == "0분 20초");
            Assert.Contains(text, block => block.Name == "ReviewActiveDays" && block.Text == "1");
            var csv = Encoding.UTF8.GetString(window.Review.Csv()).TrimStart('\uFEFF');
            Assert.StartsWith("confirmed_at,routine_id,routine_name,planned_seconds,character_id,profile_id,profile_name", csv);
            Assert.Contains(",\"look-away\",\"Look away\",20,\"default-cat\",", csv);
            Assert.Equal("눈 쉬어 주기", BreakRoutines.Find("look-away")!.Name);
        }
        finally { window?.Close(); CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture; }
    }

    [AvaloniaFact]
    public void KoreanTextInputAndEnterSavePreserveTheRoutineId()
    {
        BreakRoutine? saved = null;
        var window = new RoutineEditorWindow(new("writing", "Writing pause", [new("Rest my hands", 20)]), routine =>
        { saved = routine; return Task.CompletedTask; });
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            var name = window.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "RoutineName");
            Assert.Equal("루틴 이름", AutomationProperties.GetName(name));
            name.Focus(); name.SelectAll(); window.KeyTextInput("손목 쉬어 주기");
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            // The default button closes and disposes the window on key-down.
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(saved); Assert.False(window.IsVisible);
            Assert.Equal("writing", saved.Id); Assert.Equal("손목 쉬어 주기", saved.Name);
            Assert.Equal("Rest my hands", Assert.Single(saved.Steps).Instruction);
        }
        finally { window.Close(); }
    }

    [Theory]
    [InlineData("End of Central Directory record could not be found.", "파일 형식")]
    [InlineData("A newer version is installed. Choose the same or a newer pack.", "더 최신 버전")]
    [InlineData("The installed companion changed. Reopen the pack preview.", "다시 열어")]
    public void PackErrorsExplainRecoveryInKoreanWithoutLeakingRawDecoderMessages(string raw, string recovery)
    {
        var message = Ui.ErrorText(new InvalidDataException(raw));
        Assert.Contains(recovery, message); Assert.DoesNotContain(raw, message);
    }
}
