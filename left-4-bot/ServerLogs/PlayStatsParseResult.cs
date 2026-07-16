namespace left_4_bot.ServerLogs;

public sealed class PlayStatsParseResult
{
    public required Dictionary<string, PlayerDayStats> Players { get; init; }

    public required IReadOnlyList<string> FailedPaths { get; init; }
}
