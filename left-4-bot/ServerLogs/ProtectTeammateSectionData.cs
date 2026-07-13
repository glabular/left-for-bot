namespace left_4_bot.ServerLogs;

public enum ProtectTeammateSectionState
{
    DownloadFailed,
    NoLogs,
    CleanDay,
    HasData,
}

public sealed class ProtectTeammateSectionData
{
    public ProtectTeammateSectionState State { get; init; }

    public IReadOnlyDictionary<string, ProtectTeammatePlayerStats> Players { get; init; } =
        new Dictionary<string, ProtectTeammatePlayerStats>(StringComparer.Ordinal);

    public bool PriorityPlayerPresent { get; init; }

    public static ProtectTeammateSectionData DownloadFailed() =>
        new() { State = ProtectTeammateSectionState.DownloadFailed };

    public static ProtectTeammateSectionData NoLogs() =>
        new() { State = ProtectTeammateSectionState.NoLogs };

    public static ProtectTeammateSectionData CleanDay(bool priorityPlayerPresent) =>
        new()
        {
            State = ProtectTeammateSectionState.CleanDay,
            PriorityPlayerPresent = priorityPlayerPresent,
        };

    public static ProtectTeammateSectionData HasData(
        IReadOnlyDictionary<string, ProtectTeammatePlayerStats> players,
        bool priorityPlayerPresent) =>
        new()
        {
            State = ProtectTeammateSectionState.HasData,
            Players = players,
            PriorityPlayerPresent = priorityPlayerPresent,
        };
}
