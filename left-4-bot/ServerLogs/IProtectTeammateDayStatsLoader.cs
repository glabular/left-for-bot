namespace left_4_bot.ServerLogs;

/// <summary>
/// Loads structured protect-teammate stats for a gaming-day anchor (for bet settlement).
/// </summary>
public interface IProtectTeammateDayStatsLoader
{
    Task<ProtectTeammateSectionData> LoadAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken);
}
