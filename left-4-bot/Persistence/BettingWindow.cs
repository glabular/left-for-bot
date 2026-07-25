using left_4_bot.ServerLogs;

namespace left_4_bot.Persistence;

/// <summary>
/// Betting window for the current gaming day (closes at <see cref="ToteEconomy.BetWindowCloseHourMsk"/> MSK).
/// </summary>
public static class BettingWindow
{
    public static bool IsOpen(GamingDayOptions options)
    {
        var moscow = GamingDay.NowMoscow();
        return moscow.Hour < ToteEconomy.BetWindowCloseHourMsk;
    }
}
