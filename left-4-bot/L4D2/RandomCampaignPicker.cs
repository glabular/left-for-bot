using System.Security.Cryptography;

namespace left_4_bot.L4D2;

public static class RandomCampaignPicker
{
    public static string Pick()
    {
        var index = RandomNumberGenerator.GetInt32(L4d2Campaigns.All.Length);

        return L4d2Campaigns.All[index];
    }
}
