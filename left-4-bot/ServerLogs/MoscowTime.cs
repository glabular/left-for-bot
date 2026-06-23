namespace left_4_bot.ServerLogs;

// Calendar date in Moscow (UTC+3). For gaming-day boundaries use GamingDay.GetCurrentLabel.
internal static class MoscowTime
{
    public static DateOnly Today => DateOnly.FromDateTime(GamingDay.NowMoscow());
}
