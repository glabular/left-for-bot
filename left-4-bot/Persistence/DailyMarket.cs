namespace left_4_bot.Persistence;

/// <summary>
/// One betting market row per gaming-day anchor.
/// </summary>
public sealed class DailyMarket
{
    public long Id { get; set; }

    public DateOnly GamingDayAnchor { get; set; }

    public DailyMarketStatus Status { get; set; }
}
