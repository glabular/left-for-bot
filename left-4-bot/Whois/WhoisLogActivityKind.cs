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
