namespace left_4_bot.Whois;

/// <summary>
/// Configuration options for the whois feature.
/// </summary>
public sealed class WhoisOptions
{
    /// <summary>
    /// Configuration section name for whois options.
    /// </summary>
    public const string SectionName = "Whois";

    /// <summary>
    /// Gets or sets the Steam Web API key.
    /// </summary>
    public string SteamWebApiKey { get; set; } = string.Empty;
}
