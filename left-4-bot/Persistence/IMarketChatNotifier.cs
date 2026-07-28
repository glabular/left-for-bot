namespace left_4_bot.Persistence;

/// <summary>
/// Sends proactive daily-market announcements to the group chat (idempotent).
/// </summary>
public interface IMarketChatNotifier
{
    Task TryAnnounceOpenAsync(DateOnly gamingDayAnchor, CancellationToken cancellationToken);

    Task TryAnnounceReminderAsync(DateOnly gamingDayAnchor, int bank, CancellationToken cancellationToken);

    Task TryAnnounceCloseAsync(DateOnly gamingDayAnchor, int bank, CancellationToken cancellationToken);

    Task TryAnnounceGameStartedCloseAsync(
        DateOnly gamingDayAnchor,
        int bank,
        CancellationToken cancellationToken);

    Task TryAnnounceSettlementAsync(BetSettlementResult result, CancellationToken cancellationToken);
}
