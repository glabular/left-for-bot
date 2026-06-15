namespace left_4_bot.ServerLogs;

public sealed class PriorityPlayersOptions
{
    public const string SectionName = "PriorityPlayers";

    public List<string> SteamIds { get; set; } = [];
}
