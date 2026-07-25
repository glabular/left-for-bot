namespace left_4_bot.Persistence;

public enum BalanceAdjustStatus
{
    NeedLink,
    InsufficientBalance,
    Adjusted,
}

public readonly record struct BalanceAdjustResult(
    BalanceAdjustStatus Status,
    int Balance);
