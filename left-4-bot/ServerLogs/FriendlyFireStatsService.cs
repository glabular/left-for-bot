namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireStatsService
{
    /// <summary>
    /// Projects an already-parsed gaming-day scan into the friendly-fire section.
    /// The server logs are downloaded and scanned once by <see cref="PlayStatsService"/>,
    /// so this method does no FTP or file work.
    /// </summary>
    public FriendlyFireSectionData CreateFriendlyFireSection(GamingDayLogScanResult scan)
    {
        if (scan.Attackers.Count == 0)
        {
            return FriendlyFireSectionData.NoEventsWithPriorityPresent(scan.PriorityPlayerPresent);
        }

        return FriendlyFireSectionData.HasHits(scan.Attackers, scan.PriorityPlayerPresent);
    }
}
