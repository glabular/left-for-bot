using System.Text.RegularExpressions;
using left_4_bot.ServerLogs;

namespace left_4_bot.Whois;

internal enum WhoisLogActivityKind
{
    Entered,
    Disconnected,
}

internal sealed record WhoisLogActivity(WhoisLogActivityKind Kind, WhoisPlayerRef Player);

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
