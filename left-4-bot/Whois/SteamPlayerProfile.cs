namespace left_4_bot.Whois;

public sealed record SteamPlayerProfile(
    string Name,
    string ClassicSteamId,
    int? L4d2Hours,
    int? L4d1Hours,
    int? VacBanCount)
{
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
