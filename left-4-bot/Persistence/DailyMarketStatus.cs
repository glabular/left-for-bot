namespace left_4_bot.Persistence;

/// <summary>
/// Lifecycle of one gaming-day betting market. Settled/Cancelled block re-settlement (step 4+).
/// </summary>
public enum DailyMarketStatus
{
    Open = 0,
    Closed = 1,
    Settled = 2,
    Cancelled = 3,
}
