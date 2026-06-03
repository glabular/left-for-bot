namespace left_4_bot.L4D2;

public static class CampaignCatalog
{
    public static bool TryGetByEnglishName(string englishName, out L4d2Campaign campaign)
    {
        foreach (var candidate in L4d2Campaigns.All)
        {
            if (candidate.EnglishName.Equals(englishName, StringComparison.OrdinalIgnoreCase))
            {
                campaign = candidate;

                return true;
            }
        }

        campaign = default;

        return false;
    }
}