namespace left_4_bot.Whois;

public sealed class WhoisOptions
{
    public const string SectionName = "Whois";

    public string SteamWebApiKey { get; set; } = string.Empty;

    public int PollIntervalSeconds { get; set; } = 15;

    public int L4d2AppId { get; set; } = 550;

    public int L4d1AppId { get; set; } = 500;
}
