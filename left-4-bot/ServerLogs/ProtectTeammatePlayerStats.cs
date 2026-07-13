namespace left_4_bot.ServerLogs;

public sealed class ProtectTeammatePlayerStats
{
    public required string SteamId { get; init; }

    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }
}
