namespace left_4_bot.Whois;

/// <summary>
/// Steam profile data used in a whois report.
/// </summary>
/// <param name="Name">In-game player name.</param>
/// <param name="ClassicSteamId">Classic Source Steam ID (<c>STEAM_X:Y:Z</c>).</param>
/// <param name="L4d2Hours">Left 4 Dead 2 playtime in hours, if known.</param>
/// <param name="L4d1Hours">Left 4 Dead playtime in hours, if known.</param>
/// <param name="VacBanCount">Number of VAC bans, if known.</param>
public sealed record SteamPlayerProfile(
    string Name,
    string ClassicSteamId,
    int? L4d2Hours,
    int? L4d1Hours,
    int? VacBanCount)
{
    /// <summary>
    /// Gets combined L4D1 and L4D2 hours, or <see langword="null"/> if either value is missing.
    /// </summary>
    public int? TotalHours
    {
        get
        {
            if (L4d2Hours is null || L4d1Hours is null)
            {
                return null;
            }

            return L4d2Hours.Value + L4d1Hours.Value;
        }
    }
}
