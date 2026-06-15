namespace left_4_bot.ServerLogs;

public static class FriendlyFireLogParser
{
    public static Dictionary<string, FriendlyFireAttackerStats> ParseFiles(
        IEnumerable<string> filePaths,
        GamingDayBounds bounds) =>
        GamingDayLogParser.ScanFiles(
            filePaths,
            bounds,
            new HashSet<string>(StringComparer.Ordinal)).Attackers;
}
