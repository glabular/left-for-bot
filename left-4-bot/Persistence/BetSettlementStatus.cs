namespace left_4_bot.Persistence;

public enum BetSettlementStatus
{
    /// <summary>Anchor is still the current gaming day (or in the future).</summary>
    NotReady,

    /// <summary>Market already Settled or Cancelled.</summary>
    AlreadyFinalized,

    /// <summary>Protect stats could not be downloaded; market stays Closed for retry.</summary>
    StatsUnavailable,

    /// <summary>Expected players missing during credit; nothing committed.</summary>
    PersistenceConflict,

    /// <summary>No market row and no bets for this day.</summary>
    NothingToSettle,

    /// <summary>Winners credited; market Settled.</summary>
    Settled,

    /// <summary>Full refund; market Cancelled.</summary>
    Cancelled,
}
