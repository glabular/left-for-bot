namespace left_4_bot.Persistence;

public sealed record DailyBet(
    DateOnly GamingDayAnchor,
    long TelegramUserId,
    string TargetSteamId,
    int Amount);
