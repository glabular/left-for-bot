using System.Collections.Concurrent;

namespace left_4_bot.L4D2;

public sealed class GoldenPoolEditLimiter
{
    private const int DailyLimit = 3;

    private readonly ConcurrentDictionary<long, DailyEditCount> _counts = new();

    public bool CanEdit(long userId)
    {
        var today = GetMoscowDateToday();

        if (!_counts.TryGetValue(userId, out var entry) || entry.Date != today)
        {
            return true;
        }

        return entry.Count < DailyLimit;
    }

    public void RecordEdit(long userId)
    {
        var today = GetMoscowDateToday();

        _counts.AddOrUpdate(
            userId,
            _ => new DailyEditCount(today, 1),
            (_, existing) => existing.Date == today
                ? new DailyEditCount(today, existing.Count + 1)
                : new DailyEditCount(today, 1));
    }

    private static DateOnly GetMoscowDateToday() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    private sealed record DailyEditCount(DateOnly Date, int Count);
}
