namespace left_4_bot.ServerLogs;

public sealed class PlayerDayStats
{
    public PlayerDayStats(string steamId, string name)
    {
        SteamId = steamId;
        Name = name;
    }

    public string SteamId { get; }

    public string Name { get; set; }

    public int Common { get; set; }

    public int SiKilled { get; set; }

    public int Headshots { get; set; }

    public int WitchDamage { get; set; }

    public int TankDamage { get; set; }

    public int RockSkeets { get; set; }

    public int Clears { get; set; }

    public int MeleesOnTank { get; set; }
}
