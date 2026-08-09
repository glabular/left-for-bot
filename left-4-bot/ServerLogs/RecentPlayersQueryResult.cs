namespace left_4_bot.ServerLogs;

/// <summary>
/// Result of loading players from the last rolling wall-clock window.
/// </summary>
public sealed record RecentPlayersQueryResult(
    bool IsSuccess,
    string Message,
    IReadOnlyList<RecentPlayerEntry> Players)
{
    public static RecentPlayersQueryResult Failed(string message) =>
        new(false, message, []);

    public static RecentPlayersQueryResult Succeeded(
        IReadOnlyList<RecentPlayerEntry> players,
        string message) =>
        new(true, message, players);
}
