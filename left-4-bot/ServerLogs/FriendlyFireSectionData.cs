namespace left_4_bot.ServerLogs;

public enum FriendlyFireSectionState
{
    DownloadFailed,
    NoLogs,
    CleanDay,
    HasHits,
}

public sealed class FriendlyFireSectionData
{
    public FriendlyFireSectionState State { get; init; }

    public IReadOnlyDictionary<string, FriendlyFireAttackerStats> Attackers { get; init; } =
        new Dictionary<string, FriendlyFireAttackerStats>(StringComparer.Ordinal);

    public bool PriorityPlayerPresent { get; init; }

    public static FriendlyFireSectionData DownloadFailed() =>
        new() { State = FriendlyFireSectionState.DownloadFailed };

    public static FriendlyFireSectionData NoLogs() =>
        new() { State = FriendlyFireSectionState.NoLogs };

    public static FriendlyFireSectionData NoEventsWithPriorityPresent(bool priorityPlayerPresent) =>
        new()
        {
            State = FriendlyFireSectionState.CleanDay,
            PriorityPlayerPresent = priorityPlayerPresent,
        };

    public static FriendlyFireSectionData HasHits(
        IReadOnlyDictionary<string, FriendlyFireAttackerStats> attackers,
        bool priorityPlayerPresent) =>
        new()
        {
            State = FriendlyFireSectionState.HasHits,
            Attackers = attackers,
            PriorityPlayerPresent = priorityPlayerPresent,
        };
}
