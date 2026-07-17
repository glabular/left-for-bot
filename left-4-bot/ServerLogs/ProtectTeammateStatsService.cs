namespace left_4_bot.ServerLogs;

public sealed class ProtectTeammateStatsService
{
    /// <summary>
    /// Projects an already-parsed gaming-day scan into the protect-teammate section.
    /// The server logs are downloaded and scanned once by <see cref="PlayStatsService"/>,
    /// so this method does no FTP or file work.
    /// </summary>
    public ProtectTeammateSectionData CreateProtectTeammateSection(GamingDayLogScanResult scan)
    {
        if (scan.ProtectTeammatePlayers.Count == 0)
        {
            return ProtectTeammateSectionData.NoEventsWithPriorityPresent(scan.PriorityPlayerPresent);
        }

        return ProtectTeammateSectionData.HasData(
            scan.ProtectTeammatePlayers,
            scan.PriorityPlayerPresent);
    }
}
