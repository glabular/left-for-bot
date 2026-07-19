using System.Text.RegularExpressions;

namespace left_4_bot.Whois;

/// <summary>
/// Parses player rows from an RCON <c>status</c> response.
/// </summary>
public static partial class RconStatusPlayerParser
{
    // Typical: # 2 "Name" STEAM_1:0:12345 00:12 ...
    // Or:      # 2 1 "Name" STEAM_1:0:12345 ...
    [GeneratedRegex(
        @"^#\s+\d+(?:\s+\d+)?\s+""(?<name>[^""]+)""\s+(?<steam>STEAM_\d+:\d+:\d+)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex StatusPlayerLineRegex();

    /// <summary>
    /// Extracts unique players from RCON status text.
    /// </summary>
    /// <param name="statusText">Raw <c>status</c> command output.</param>
    /// <returns>Parsed players, deduplicated by Steam ID.</returns>
    public static IReadOnlyList<WhoisPlayerRef> Parse(string statusText)
    {
        if (string.IsNullOrWhiteSpace(statusText))
        {
            return [];
        }

        var players = new List<WhoisPlayerRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        using var reader = new StringReader(statusText);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') == false)
            {
                continue;
            }

            // Header line: # userid name uniqueid ...
            if (trimmed.Contains("userid", StringComparison.OrdinalIgnoreCase)
                && trimmed.Contains("uniqueid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = StatusPlayerLineRegex().Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            var steamId = match.Groups["steam"].Value;
            if (!seen.Add(steamId))
            {
                continue;
            }

            players.Add(new WhoisPlayerRef(match.Groups["name"].Value, steamId));
        }

        return players;
    }
}
