namespace left_4_bot.Persistence;

public static class ToteEconomy
{
    public const int StarterTabs = 1000;

    public const int CommandRewardTabs = 25;

    /// <summary>
    /// Tabs for a rewarded command when <c>args</c> is non-empty.
    /// </summary>
    public const int CommandRewardWithArgsTabs = 35;

    public const int CommandRewardsPerGamingDay = 3;

    public const int DailyCategoryPlace1Tabs = 30;

    public const int DailyCategoryPlace2Tabs = 25;

    public const int DailyCategoryPlace3Tabs = 20;

    /// <summary>
    /// Minimum distinct bettors for a daily market; otherwise the market
    /// is cancelled and all stakes are refunded.
    /// </summary>
    public const int MinBettorsPerMarket = 3;

    /// <summary>
    /// Minimum single bet in Tabs.
    /// </summary>
    public const int MinBetTabs = 50;

    /// <summary>
    /// Moscow hour (UTC+3) when the betting window closes.
    /// </summary>
    public const int BetWindowCloseHourMsk = 20;

    /// <summary>
    /// Former Moscow hour for the "bets still open" reminder (no longer sent).
    /// </summary>
    public const int BetReminderHourMsk = 16;
}
