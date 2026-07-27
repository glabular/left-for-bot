namespace left_4_bot.Persistence;

/// <summary>
/// One credited or refunded balance change from settlement.
/// </summary>
public sealed record BetPayoutResult(
    long TelegramUserId,
    string TargetSteamId,
    int Stake,
    int Credited);
