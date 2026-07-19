namespace left_4_bot.Whois;

/// <summary>
/// Lightweight player identity used by whois features.
/// </summary>
/// <param name="Name">In-game player name.</param>
/// <param name="ClassicSteamId">Classic Source Steam ID (<c>STEAM_X:Y:Z</c>).</param>
public sealed record WhoisPlayerRef(string Name, string ClassicSteamId);
