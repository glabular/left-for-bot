using System.Globalization;
using System.Text.RegularExpressions;

namespace left_4_bot.ServerLogs;

internal static partial class ServerLogLineParser
{
    [GeneratedRegex(
        @"^L (?<month>\d{2})/(?<day>\d{2})/(?<year>\d{4}) - (?<hour>\d{2}):(?<minute>\d{2}):(?<second>\d{2}):",
        RegexOptions.Compiled)]
    private static partial Regex TimestampRegex();

    // Chat lines from SuperLogs append extra <Class><Alive>… fields after team; join lines do not.
    // Match the common Name<uid><STEAM><team> prefix and ignore any trailing angle-bracket fields.
    [GeneratedRegex(
        @"^(?<name>[^<]+)<(?<userid>\d+)><(?<steam>[^>]+)><(?<team>[^>]*)>",
        RegexOptions.Compiled)]
    private static partial Regex PlayerTokenRegex();

    public static bool TryParseTimestamp(string line, out DateTime eventTime)
    {
        eventTime = default;

        var match = TimestampRegex().Match(line);
        if (!match.Success)
        {
            return false;
        }

        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture);
        var second = int.Parse(match.Groups["second"].Value, CultureInfo.InvariantCulture);

        eventTime = new DateTime(year, month, day, hour, minute, second);
        return true;
    }

    public static bool TryParsePlayerToken(string token, out ServerLogPlayerInfo player)
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

        player = new ServerLogPlayerInfo(
            match.Groups["name"].Value,
            steamId,
            match.Groups["team"].Value);

        return true;
    }
}
