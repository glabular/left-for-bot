namespace left_4_bot.ServerLogs;

public sealed class GamingDayLogScanResult
{
    public bool PriorityPlayerPresent { get; set; }

    public Dictionary<string, FriendlyFireAttackerStats> Attackers { get; } =
        new(StringComparer.Ordinal);
}
