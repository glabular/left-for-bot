namespace left_4_bot.ServerLogs;

public sealed class FriendlyFireAttackerStats
{
    public string Name { get; set; } = "";

    public string SteamId { get; set; } = "";

    public Dictionary<string, FriendlyFireVictimStats> Victims { get; } = new(StringComparer.Ordinal);
}
