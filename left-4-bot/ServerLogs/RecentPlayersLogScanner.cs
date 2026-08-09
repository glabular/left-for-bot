using System.Text.RegularExpressions;
using left_4_bot.Whois;

namespace left_4_bot.ServerLogs;

/// <summary>
/// Collects unique players who connected or entered within a Moscow wall-clock window.
/// </summary>
public static partial class RecentPlayersLogScanner
{
    [GeneratedRegex(
        @"^L .+?""(?<player>[^""]+)"" (connected|entered the game)",
        RegexOptions.Compiled)]
    private static partial Regex PlayerActivityRegex();

    /// <summary>
    /// Scans local log files for players active in <paramref name="bounds"/>.
    /// </summary>
    /// <param name="filePaths">Local log file paths.</param>
    /// <param name="bounds">Half-open Moscow time window.</param>
    /// <returns>
    /// Players ordered by most recent activity first; nickname is the latest seen.
    /// </returns>
    public static IReadOnlyList<RecentPlayerEntry> ScanFiles(
        IEnumerable<string> filePaths,
        GamingDayBounds bounds)
    {
        var bySteam = new Dictionary<string, (string Name, DateTime LastSeen)>(
            StringComparer.Ordinal);

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(filePath))
            {
                TryScanLine(line, bounds, bySteam);
            }
        }

        return bySteam
            .OrderByDescending(kv => kv.Value.LastSeen)
            .Select(kv => new RecentPlayerEntry(kv.Value.Name, kv.Key))
            .ToList();
    }

    /// <summary>
    /// Scans an in-memory log text (for tests and small slices).
    /// </summary>
    public static IReadOnlyList<RecentPlayerEntry> ScanText(
        string logText,
        GamingDayBounds bounds)
    {
        var bySteam = new Dictionary<string, (string Name, DateTime LastSeen)>(
            StringComparer.Ordinal);

        if (string.IsNullOrEmpty(logText))
        {
            return [];
        }

        using var reader = new StringReader(logText);
        while (reader.ReadLine() is { } line)
        {
            TryScanLine(line, bounds, bySteam);
        }

        return bySteam
            .OrderByDescending(kv => kv.Value.LastSeen)
            .Select(kv => new RecentPlayerEntry(kv.Value.Name, kv.Key))
            .ToList();
    }

    private static void TryScanLine(
        string line,
        GamingDayBounds bounds,
        Dictionary<string, (string Name, DateTime LastSeen)> bySteam)
    {
        if (!ServerLogLineParser.TryParseTimestamp(line, out var eventTime)
            || !GamingDay.Contains(bounds, eventTime))
        {
            return;
        }

        var match = PlayerActivityRegex().Match(line);
        if (!match.Success)
        {
            return;
        }

        if (!ServerLogLineParser.TryParsePlayerToken(match.Groups["player"].Value, out var player))
        {
            return;
        }

        if (!SteamIdConverter.TryToCanonicalClassic(player.SteamId, out var classic))
        {
            classic = player.SteamId;
        }

        if (bySteam.TryGetValue(classic, out var existing) && existing.LastSeen > eventTime)
        {
            return;
        }

        bySteam[classic] = (player.Name, eventTime);
    }
}
