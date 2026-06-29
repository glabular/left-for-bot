namespace left_4_bot.Rcon;

public sealed class RconOptions
{
    public const string SectionName = "Rcon";

    public string Host { get; set; } = "";

    public int Port { get; set; } = 27217;

    public string Password { get; set; } = "";
}
