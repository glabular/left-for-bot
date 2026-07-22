namespace left_4_bot.Persistence;

public readonly record struct BalanceClaimResult(
    BalanceClaimStatus Status,
    int Balance,
    string? SteamId);
