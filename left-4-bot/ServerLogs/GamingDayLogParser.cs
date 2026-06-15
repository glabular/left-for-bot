using System.Text.RegularExpressions;

namespace left_4_bot.ServerLogs;

public static partial class GamingDayLogParser
{
    [GeneratedRegex(
        @"^L .+?""(?<player>[^""]+)"" (connected|entered the game)",
        RegexOptions.Compiled)]
    private static partial Regex PlayerActivityRegex();

    [GeneratedRegex(
        @"^L .+?""(?<attacker>[^""]+)"" triggered ""friendly_fire"" against ""(?<victim>[^""]+)""",
        RegexOptions.Compiled)]
    private static partial Regex FriendlyFireLineRegex();

    public static GamingDayLogScanResult ScanFiles(
        IEnumerable<string> filePaths,
        GamingDayBounds bounds,
        IReadOnlySet<string> prioritySteamIds)
    {
        var result = new GamingDayLogScanResult();

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(filePath))
            {
                ScanLine(line, bounds, prioritySteamIds, result);
            }
        }

        return result;
    }

    private static void ScanLine(
        string line,
        GamingDayBounds bounds,
        IReadOnlySet<string> prioritySteamIds,
        GamingDayLogScanResult result)
    {
        if (!ServerLogLineParser.TryParseTimestamp(line, out var eventTime)
            || !GamingDay.Contains(bounds, eventTime))
        {
            return;
        }

        TryScanPlayerActivity(line, prioritySteamIds, result);
        TryScanFriendlyFire(line, prioritySteamIds, result);
    }

    private static void TryScanPlayerActivity(
        string line,
        IReadOnlySet<string> prioritySteamIds,
        GamingDayLogScanResult result)
    {
        var match = PlayerActivityRegex().Match(line);
        if (!match.Success)
        {
            return;
        }

        if (!ServerLogLineParser.TryParsePlayerToken(match.Groups["player"].Value, out var player))
        {
            return;
        }

        if (prioritySteamIds.Contains(player.SteamId))
        {
            result.PriorityPlayerPresent = true;
        }
    }

    private static void TryScanFriendlyFire(
        string line,
        IReadOnlySet<string> prioritySteamIds,
        GamingDayLogScanResult result)
    {
        var match = FriendlyFireLineRegex().Match(line);
        if (!match.Success)
        {
            return;
        }

        if (!ServerLogLineParser.TryParsePlayerToken(match.Groups["attacker"].Value, out var attacker)
            || !ServerLogLineParser.TryParsePlayerToken(match.Groups["victim"].Value, out var victim))
        {
            return;
        }

        if (!string.Equals(attacker.Team, "Survivor", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(victim.Team, "Survivor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (prioritySteamIds.Contains(attacker.SteamId)
            || prioritySteamIds.Contains(victim.SteamId))
        {
            result.PriorityPlayerPresent = true;
        }

        if (!result.Attackers.TryGetValue(attacker.SteamId, out var attackerStats))
        {
            attackerStats = new FriendlyFireAttackerStats
            {
                Name = attacker.Name,
                SteamId = attacker.SteamId,
            };
            result.Attackers[attacker.SteamId] = attackerStats;
        }
        else
        {
            attackerStats.Name = attacker.Name;
        }

        if (!attackerStats.Victims.TryGetValue(victim.SteamId, out var victimStats))
        {
            victimStats = new FriendlyFireVictimStats
            {
                Name = victim.Name,
                SteamId = victim.SteamId,
            };
            attackerStats.Victims[victim.SteamId] = victimStats;
        }
        else
        {
            victimStats.Name = victim.Name;
        }

        victimStats.HitCount++;
    }
}
