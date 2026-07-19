using System.Text.RegularExpressions;
using left_4_bot.ServerLogs;

namespace left_4_bot.Spy;

/// <summary>
/// Parses team-chat (<c>say_team</c>) lines from server log slices.
/// All-chat (<c>say</c>) is ignored.
/// </summary>
internal static partial class SpyChatParser
{
    [GeneratedRegex(
        @"^L .+?""(?<player>[^""]+)"" say_team ""(?<message>.*)""",
        RegexOptions.Compiled)]
    private static partial Regex SayTeamRegex();

    /// <summary>
    /// Parses team-chat messages from a log text slice.
    /// </summary>
    /// <param name="logSlice">UTF-8 log text to scan.</param>
    /// <returns>Parsed team-chat messages in file order.</returns>
    public static IReadOnlyList<SpyChatMessage> Parse(string logSlice)
    {
        if (string.IsNullOrEmpty(logSlice))
        {
            return [];
        }

        var messages = new List<SpyChatMessage>();
        using var reader = new StringReader(logSlice);

        while (reader.ReadLine() is { } line)
        {
            var match = SayTeamRegex().Match(line);

            if (!match.Success)
            {
                continue;
            }

            if (!ServerLogLineParser.TryParsePlayerToken(
                    match.Groups["player"].Value,
                    out var player))
            {
                continue;
            }

            var text = match.Groups["message"].Value;

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            messages.Add(new SpyChatMessage(player.Name, player.SteamId, text));
        }

        return messages;
    }
}
