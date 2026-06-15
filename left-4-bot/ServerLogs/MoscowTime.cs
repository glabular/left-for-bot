namespace left_4_bot.ServerLogs;

internal static class MoscowTime
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
}
