using System.Text;
using Unfold.Core;

namespace Unfold.Tests;

public class LegacyPersonalizationTests
{
    private static BreakRoutine Routine(string id = "writing", string name = "Writing pause") => new(id, name, [new("Rest my hands", 20)]);
    private static BreakSession Completed(BreakRoutine? routine = null)
    {
        var session = new BreakSession(routine ?? Routine(), "default-cat"); session.Start(TimeSpan.Zero);
        session.Tick(TimeSpan.FromSeconds(10)); session.Tick(TimeSpan.FromSeconds(20)); Assert.True(session.Complete()); return session;
    }




    [Fact]
    public void SevenDayReviewIncludesEmptyDaysAndUsesCompletionLocalDate()
    {
        var end = new DateTimeOffset(2026, 9, 13, 0, 5, 0, TimeSpan.FromHours(9)); var history = new BreakHistory();
        foreach (var days in new[] { -7, -6, -2, 0, 1 }) history.Add(Completed(), end.AddDays(days));
        var review = history.Review(DateOnly.FromDateTime(end.Date));
        Assert.Equal(7, review.Days.Count); Assert.Equal(3, review.Entries.Count); Assert.Equal(60, review.TotalSeconds);
        Assert.Equal(new DateOnly(2026, 9, 7), review.StartDay); Assert.Equal(3, review.DaysWithBreaks);
        Assert.Equal(1, review.Days[^1].Count); Assert.Equal(0, review.Days[1].Count);
    }
    [Fact]
    public void ReviewExportQuotesNamesNeutralizesFormulasAndKeepsUnicode()
    {
        var history = new BreakHistory(); var date = DateTimeOffset.Now;
        history.Add(Completed(Routine(name: "=작업, \"쉼\"")), date);
        var bytes = history.Review(DateOnly.FromDateTime(date.Date)).Csv();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"'=작업, \"\"쉼\"\"\"", text); Assert.Contains("planned_seconds", text);
        Assert.Equal(3, text.Split("\r\n").Length);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }
    [Fact]
    public void LongKoreanNamesRoundTripAtTheHistoryRecordLimit()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "history.json");
        var routine = Routine(name: new string('쉼', 60));
        var history = new BreakHistory(); var date = DateTimeOffset.Now;
        for (var i = 0; i < BreakHistory.MaxRecords; i++) history.Add(Completed(routine), date.AddSeconds(i));
        history.Save(file); Assert.Equal(BreakHistory.MaxRecords, BreakHistory.Load(file).Completions.Count);
    }
    [Fact]
    public void OlderHistoryStillLoadsAndExportsItsStableRoutineId()
    {
        using var temp = new TempDirectory(); var file = Path.Combine(temp.Path, "history.json");
        File.WriteAllText(file, "{\"version\":1,\"completions\":[{\"sessionId\":\"a57483ac-7c80-4384-bd35-491d955e4e66\",\"completedAt\":\"2026-09-13T00:00:00+09:00\",\"routineId\":\"my-routine\",\"seconds\":20,\"characterId\":\"default-cat\"}]}");
        var history = BreakHistory.Load(file); Assert.Null(Assert.Single(history.Completions).ProfileId);
        var csv = Encoding.UTF8.GetString(history.Review(new(2026, 9, 13)).Csv());
        Assert.Contains("\"my-routine\",\"my-routine\",20", csv);
    }
}
