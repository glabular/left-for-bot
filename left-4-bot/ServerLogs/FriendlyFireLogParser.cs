namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireVictimStats
{
    public string Name { get; set; } = "";

    public string SteamId { get; set; } = "";

    public int HitCount { get; set; }
}

public sealed class FriendlyFireAttackerStats
{
    public string Name { get; set; } = "";

    public string SteamId { get; set; } = "";

    public Dictionary<string, FriendlyFireVictimStats> Victims { get; } = new(StringComparer.Ordinal);
}

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
