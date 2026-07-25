namespace left_4_bot.Persistence;

public enum BetActionStatus
{
    NeedLink,
    NeedStarter,
    WindowClosed,
    NoPriorityTargets,
    InvalidTargetIndex,
    InvalidAmount,
    InsufficientBalance,
    Placed,
    Replaced,
    Cancelled,
    NothingToCancel,
}

public readonly record struct BetActionResult(
    BetActionStatus Status,
    DailyBet? Bet,
    int Balance,
    IReadOnlyList<Player> PriorityTargets);
