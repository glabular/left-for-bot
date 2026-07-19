using System.Text.RegularExpressions;
using left_4_bot.ServerLogs;

namespace left_4_bot.Whois;

/// <summary>
/// Kind of player activity parsed from a server log line.
/// </summary>
internal enum WhoisLogActivityKind
{
    /// <summary>
    /// Player entered the game.
    /// </summary>
    Entered,

    /// <summary>
    /// Player disconnected from the server.
    /// </summary>
    Disconnected,
}

/// <summary>
/// A player activity event extracted from server logs.
/// </summary>
/// <param name="Kind">Activity kind.</param>
/// <param name="Player">Player associated with the activity.</param>
internal sealed record WhoisLogActivity(WhoisLogActivityKind Kind, WhoisPlayerRef Player);

/// <summary>
/// Parses enter and disconnect lines from server log slices.
/// </summary>
internal static partial class WhoisLogActivityParser
{
    [GeneratedRegex(
        @"^L .+?""(?<player>[^""]+)"" entered the game",
        RegexOptions.Compiled)]
    private static partial Regex EnteredTheGameRegex();

    [GeneratedRegex(
        @"^L .+?""(?<player>[^""]+)"" disconnected",
        RegexOptions.Compiled)]
    private static partial Regex DisconnectedRegex();

    /// <summary>
    /// Parses enter and disconnect activities from a log text slice.
    /// </summary>
    /// <param name="logSlice">UTF-8 log text to scan.</param>
    /// <returns>Parsed activities in file order.</returns>
    public static IReadOnlyList<WhoisLogActivity> Parse(string logSlice)
    {
        if (string.IsNullOrEmpty(logSlice))
        {
            return [];
        }

        var activities = new List<WhoisLogActivity>();
        using var reader = new StringReader(logSlice);

        while (reader.ReadLine() is { } line)
        {
            var entered = EnteredTheGameRegex().Match(line);

            if (entered.Success
                && ServerLogLineParser.TryParsePlayerToken(entered.Groups["player"].Value, out var enteredPlayer))
            {
                activities.Add(new WhoisLogActivity(
                    WhoisLogActivityKind.Entered,
                    new WhoisPlayerRef(enteredPlayer.Name, enteredPlayer.SteamId)));
                continue;
            }

            var disconnected = DisconnectedRegex().Match(line);

            if (disconnected.Success
                && ServerLogLineParser.TryParsePlayerToken(disconnected.Groups["player"].Value, out var leftPlayer))
            {
                activities.Add(new WhoisLogActivity(
                    WhoisLogActivityKind.Disconnected,
                    new WhoisPlayerRef(leftPlayer.Name, leftPlayer.SteamId)));
            }
        }

        return activities;
    }
}
