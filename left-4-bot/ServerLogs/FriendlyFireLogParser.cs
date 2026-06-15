using System.Globalization;
using System.Text.RegularExpressions;

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

public static partial class FriendlyFireLogParser
{
    [GeneratedRegex(
        @"^L (?<month>\d{2})/(?<day>\d{2})/(?<year>\d{4}) - .+?""(?<attacker>[^""]+)"" triggered ""friendly_fire"" against ""(?<victim>[^""]+)""",
        RegexOptions.Compiled)]
    private static partial Regex FriendlyFireLineRegex();

    [GeneratedRegex(
        @"^(?<name>[^<]+)<(?<userid>\d+)><(?<steam>[^>]+)><(?<team>[^>]+)>$",
        RegexOptions.Compiled)]
    private static partial Regex PlayerTokenRegex();

    public static Dictionary<string, FriendlyFireAttackerStats> ParseFiles(
        IEnumerable<string> filePaths,
        DateOnly moscowDate)
    {
        var attackers = new Dictionary<string, FriendlyFireAttackerStats>(StringComparer.Ordinal);

        foreach (var filePath in filePaths)
        {
            if (!File.Exists(filePath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(filePath))
            {
                TryParseLine(line, moscowDate, attackers);
            }
        }

        return attackers;
    }

    private static void TryParseLine(
        string line,
        DateOnly moscowDate,
        Dictionary<string, FriendlyFireAttackerStats> attackers)
    {
        var match = FriendlyFireLineRegex().Match(line);
        if (!match.Success)
        {
            return;
        }

        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var lineDate = new DateOnly(year, month, day);

        if (lineDate != moscowDate)
        {
            return;
        }

        if (!TryParsePlayer(match.Groups["attacker"].Value, out var attacker)
            || !TryParsePlayer(match.Groups["victim"].Value, out var victim))
        {
            return;
        }

        if (!string.Equals(attacker.Team, "Survivor", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(victim.Team, "Survivor", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!attackers.TryGetValue(attacker.SteamId, out var attackerStats))
        {
            attackerStats = new FriendlyFireAttackerStats
            {
                Name = attacker.Name,
                SteamId = attacker.SteamId,
            };
            attackers[attacker.SteamId] = attackerStats;
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

    private static bool TryParsePlayer(string token, out PlayerInfo player)
    {
        player = default;

        if (token.Contains("<BOT>", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var match = PlayerTokenRegex().Match(token);
        if (!match.Success)
        {
            return false;
        }

        var steamId = match.Groups["steam"].Value;
        if (!steamId.StartsWith("STEAM_", StringComparison.Ordinal))
        {
            return false;
        }

        player = new PlayerInfo(
            match.Groups["name"].Value,
            steamId,
            match.Groups["team"].Value);

        return true;
    }

    private readonly record struct PlayerInfo(string Name, string SteamId, string Team);
}
