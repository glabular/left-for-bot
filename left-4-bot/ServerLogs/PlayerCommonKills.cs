namespace left_4_bot.ServerLogs;

public sealed class PlayerCommonKills
{
    public PlayerCommonKills(string steamId, string name)
    {
        SteamId = steamId;
        Name = name;
    }

    public string SteamId { get; }

    public string Name { get; set; }

    public int CommonKills { get; set; }
}
