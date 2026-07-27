namespace left_4_bot.Persistence;

/// <summary>
/// One winner's stake and credited payout (includes returned stake share).
/// </summary>
public sealed record PariMutuelPayout(long TelegramUserId, int Stake, int Payout);
