using System.Security.Cryptography;

namespace left_4_bot.L4D2;

public static class GoldenPoolPicker
{
    public static L4d2Campaign? Pick(GoldenPoolService pool)
    {
        var campaigns = pool.GetCampaigns();

        if (campaigns.Count == 0)
        {
            return null;
        }

        var index = RandomNumberGenerator.GetInt32(campaigns.Count);

        return campaigns[index];
    }
}