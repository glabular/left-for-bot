namespace left_4_bot.Persistence;

public sealed record BetSettlementResult(
    DateOnly GamingDayAnchor,
    BetSettlementStatus Status,
    BetSettlementCancelReason CancelReason,
    DailyMarketStatus? MarketStatus,
    int Bank,
    IReadOnlyList<BetSettlementTopPlayer> TopPlayers,
    IReadOnlyList<BetPayoutResult> Credits)
{
    public static BetSettlementResult NotReady(DateOnly anchor) =>
        new(anchor, BetSettlementStatus.NotReady, BetSettlementCancelReason.None, null, 0, [], []);

    public static BetSettlementResult AlreadyFinalized(DateOnly anchor, DailyMarketStatus status) =>
        new(anchor, BetSettlementStatus.AlreadyFinalized, BetSettlementCancelReason.None, status, 0, [], []);

    public static BetSettlementResult StatsUnavailable(DateOnly anchor, int bank) =>
        new(
            anchor,
            BetSettlementStatus.StatsUnavailable,
            BetSettlementCancelReason.None,
            DailyMarketStatus.Closed,
            bank,
            [],
            []);

    public static BetSettlementResult NothingToSettle(DateOnly anchor) =>
        new(anchor, BetSettlementStatus.NothingToSettle, BetSettlementCancelReason.None, null, 0, [], []);

    public static BetSettlementResult PersistenceConflict(DateOnly anchor, int bank) =>
        new(
            anchor,
            BetSettlementStatus.PersistenceConflict,
            BetSettlementCancelReason.None,
            DailyMarketStatus.Closed,
            bank,
            [],
            []);
}
