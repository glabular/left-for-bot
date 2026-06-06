using System.Collections.Concurrent;

namespace left_4_bot.L4D2;

public sealed class GoldenPoolEditLimiter
{
    private const int DailyLimit = 3;

    private readonly ConcurrentDictionary<long, DailyEditCount> _counts = new();
    private readonly ConcurrentDictionary<long, object> _userLocks = new();

    public bool TryApplyEdit(long userId, Func<bool> editSucceeded)
    {
        var gate = _userLocks.GetOrAdd(userId, _ => new object());

        lock (gate)
        {
            var today = GetMoscowDateToday();

            if (_counts.TryGetValue(userId, out var entry) && entry.Date == today && entry.Count >= DailyLimit)
            {
                return false;
            }

            if (!editSucceeded())
            {
                return true;
            }

            _counts.AddOrUpdate(
                userId,
                _ => new DailyEditCount(today, 1),
                (_, existing) => existing.Date == today
                    ? new DailyEditCount(today, existing.Count + 1)
                    : new DailyEditCount(today, 1));

            return true;
        }
    }

    private static DateOnly GetMoscowDateToday() =>
        DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    private sealed record DailyEditCount(DateOnly Date, int Count);
}
