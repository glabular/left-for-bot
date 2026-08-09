using System.Text.RegularExpressions;
using left_4_bot.Whois;

namespace left_4_bot.Rcon;

/// <summary>
/// Parses Steam ID bans from an RCON <c>listid</c> response.
/// </summary>
public static partial class BanIdListParser
{
    [GeneratedRegex(
        @"^\s*(?<slot>\d+)\s+(?<steam>STEAM_\d+:\d+:\d+)\s*(?::\s*(?<duration>.+?))?\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BanRowRegex();

    /// <summary>
    /// Extracts ban rows from raw <c>listid</c> text.
    /// </summary>
    /// <param name="listidText">Raw RCON response.</param>
    /// <returns>Parsed entries in list order; empty when none found.</returns>
    public static IReadOnlyList<ServerBanListEntry> Parse(string listidText)
    {
        if (string.IsNullOrWhiteSpace(listidText))
        {
            return [];
        }

        var entries = new List<ServerBanListEntry>();

        using var reader = new StringReader(listidText);
        while (reader.ReadLine() is { } line)
        {
            var match = BanRowRegex().Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["slot"].Value, out var slot))
            {
                continue;
            }

            var rawSteam = match.Groups["steam"].Value;
            if (!SteamIdConverter.TryToCanonicalClassic(rawSteam, out var classic))
            {
                classic = rawSteam;
            }

            var duration = match.Groups["duration"].Success
                ? match.Groups["duration"].Value.Trim()
                : null;

            if (string.IsNullOrWhiteSpace(duration))
            {
                duration = null;
            }
            else if (duration.Equals("permanent", StringComparison.OrdinalIgnoreCase))
            {
                duration = "навсегда";
            }

            entries.Add(new ServerBanListEntry(slot, classic, duration));
        }

        return entries;
    }
}
