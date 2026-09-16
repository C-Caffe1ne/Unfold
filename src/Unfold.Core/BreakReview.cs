using System.Globalization;
using System.Text;

namespace Unfold.Core;

public sealed record BreakDay(DateOnly Date, int Count, int Seconds);
public sealed record BreakReview(DateOnly StartDay, DateOnly EndDay, IReadOnlyList<BreakDay> Days, IReadOnlyList<CompletedBreak> Entries)
{
    public int TotalSeconds => Entries.Sum(item => item.RecordedSeconds);
    public int DaysWithBreaks => Days.Count(day => day.Count > 0);
    public byte[] Csv()
    {
        var csv = new StringBuilder("confirmed_at,routine_id,routine_name,planned_seconds,character_id,profile_id,profile_name,actual_seconds\r\n");
        foreach (var entry in Entries)
            csv.Append(string.Join(",", new[]
            {
                Cell(entry.CompletedAt.ToString("O", CultureInfo.InvariantCulture)), Cell(entry.RoutineId),
                Cell(entry.RoutineName ?? entry.RoutineId), entry.Seconds.ToString(CultureInfo.InvariantCulture),
                Cell(entry.CharacterId), Cell(entry.ProfileId ?? ""), Cell(entry.ProfileName ?? ""),
                entry.ActualSeconds?.ToString(CultureInfo.InvariantCulture) ?? ""
            })).Append("\r\n");
        // BOM preserves Korean names in spreadsheet applications. Every text cell is quoted.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
    private static string Cell(string value)
    {
        // Quoting alone does not prevent spreadsheet formula interpretation.
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
