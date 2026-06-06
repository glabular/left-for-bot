using System.Security.Cryptography;

namespace left_4_bot.L4D2;

public static class RandomCampaignPicker
{
    public static L4d2Campaign Pick()
    {
        return PickFrom(L4d2Campaigns.All)!.Value;
    }

    public static L4d2Campaign? PickFrom(IReadOnlyList<L4d2Campaign> campaigns)
    {
        if (campaigns.Count == 0)
        {
            return null;
        }

        return campaigns[RandomNumberGenerator.GetInt32(campaigns.Count)];
    }
}
