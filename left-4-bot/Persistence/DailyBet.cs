namespace left_4_bot.Persistence;

/// <summary>
/// One active bet per Telegram user for a gaming day.
/// </summary>
public sealed class DailyBet
{
    public long Id { get; set; }

    public DateOnly GamingDayAnchor { get; set; }

    public long TelegramUserId { get; set; }

    public string TargetSteamId { get; set; } = string.Empty;

    public int Amount { get; set; }
}
