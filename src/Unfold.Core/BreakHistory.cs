using System.Text.Json;

namespace Unfold.Core;

public sealed record CompletedBreak(Guid SessionId, DateTimeOffset CompletedAt, string RoutineId, int Seconds, string CharacterId,
    string? RoutineName = null, string? ProfileId = null, string? ProfileName = null);
public sealed record BreakSummary(int Count, int Seconds);

public sealed class BreakHistory
{
    private sealed record StoredHistory(int Version, List<CompletedBreak> Completions);
    private readonly List<CompletedBreak> completions = [];
    public const int MaxRecords = 2000;
    public IReadOnlyList<CompletedBreak> Completions => completions.AsReadOnly();
    public static BreakHistory Load(string path)
    {
        var result = new BreakHistory();
        if (!File.Exists(path)) return result;
        var stored = JsonSerializer.Deserialize<StoredHistory>(ImageCodec.ReadBounded(path, 4 * 1024 * 1024), CharacterLibrary.JsonOptions);
        if (stored is null || stored.Version != 1 || stored.Completions is null || stored.Completions.Count > MaxRecords)
            throw new InvalidDataException("Invalid break history.");
        var ids = new HashSet<Guid>();
        foreach (var item in stored.Completions)
        {
            Validate(item);
            if (!ids.Add(item.SessionId)) throw new InvalidDataException("Duplicate break history entry.");
            result.completions.Add(item);
        }
        return result;
    }
    public bool Add(BreakSession session, DateTimeOffset completedAt)
    {
        if (session.State != BreakSessionState.Completed || completions.Any(item => item.SessionId == session.Id)) return false;
        var item = new CompletedBreak(session.Id, completedAt, session.Routine.Id, session.Routine.DurationSeconds, session.CharacterId,
            session.Routine.Name, session.ProfileId, session.ProfileName);
        Validate(item);
        completions.RemoveAll(previous => previous.CompletedAt < completedAt.AddDays(-90));
        completions.Add(item);
        if (completions.Count > MaxRecords) completions.RemoveRange(0, completions.Count - MaxRecords);
        return true;
    }
    public BreakSummary ForDay(DateTimeOffset day)
    {
        var items = completions.Where(item => item.CompletedAt.Date == day.Date).ToArray();
        return new(items.Length, items.Sum(item => item.Seconds));
    }
    public void Save(string path) => AtomicFile.Write(path,
        JsonSerializer.SerializeToUtf8Bytes(new StoredHistory(1, completions), CharacterLibrary.JsonOptions));
    public BreakReview Review(DateOnly endDay)
    {
        var start = endDay.AddDays(-6);
        var entries = completions.Where(item => DateOnly.FromDateTime(item.CompletedAt.Date) >= start &&
            DateOnly.FromDateTime(item.CompletedAt.Date) <= endDay).OrderBy(item => item.CompletedAt).ThenBy(item => item.SessionId).ToArray();
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = start.AddDays(offset); var daily = entries.Where(item => DateOnly.FromDateTime(item.CompletedAt.Date) == date).ToArray();
            return new BreakDay(date, daily.Length, daily.Sum(item => item.Seconds));
        }).ToArray();
        return new(start, endDay, Array.AsReadOnly(days), Array.AsReadOnly(entries));
    }
    private static void Validate(CompletedBreak? item)
    {
        if (item is null || item.SessionId == Guid.Empty || item.CompletedAt == default || item.Seconds is < 1 or > 600 ||
            !CharacterLibrary.SafeId(item.RoutineId) || !CharacterLibrary.SafeId(item.CharacterId) ||
            item.RoutineName is { Length: > 60 } || item.ProfileName is { Length: > 60 } ||
            (item.ProfileId is not null && !CharacterLibrary.SafeId(item.ProfileId)))
            throw new InvalidDataException("Invalid completed break.");
    }
}
