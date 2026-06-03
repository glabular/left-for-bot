using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace left_4_bot.L4D2;

public sealed class GoldenPoolService
{
    private readonly string _appsettingsPath;
    private List<string> _campaignNames;

    public GoldenPoolService(IOptions<GoldenPoolOptions> options, IHostEnvironment environment)
    {
        _appsettingsPath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _campaignNames = new List<string>(options.Value.Campaigns);
    }

    public IReadOnlyList<L4d2Campaign> GetCampaigns()
    {
        var result = new List<L4d2Campaign>();

        foreach (var name in _campaignNames)
        {
            if (CampaignCatalog.TryGetByEnglishName(name, out var campaign))
            {
                result.Add(campaign);
            }
        }

        return result;
    }

    public bool Contains(string englishName) =>
        _campaignNames.Any(n => n.Equals(englishName, StringComparison.OrdinalIgnoreCase));

    public AddResult TryAdd(string englishName)
    {
        if (!CampaignCatalog.TryGetByEnglishName(englishName, out var campaign))
        {
            return AddResult.UnknownCampaign;
        }

        if (Contains(campaign.EnglishName))
        {
            return AddResult.AlreadyExists;
        }

        _campaignNames.Add(campaign.EnglishName);
        Save();
        return AddResult.Added;
    }

    public RemoveResult TryRemove(string englishName)
    {
        if (!CampaignCatalog.TryGetByEnglishName(englishName, out var campaign))
        {
            return RemoveResult.UnknownCampaign;
        }

        var index = _campaignNames.FindIndex(
            n => n.Equals(campaign.EnglishName, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return RemoveResult.NotInPool;
        }

        _campaignNames.RemoveAt(index);
        Save();

        return RemoveResult.Removed;
    }

    private void Save()
    {
        var json = File.ReadAllText(_appsettingsPath);
        var root = JsonNode.Parse(json)?.AsObject()
            ?? throw new InvalidOperationException("Failed to parse appsettings.json.");

        var campaignsArray = new JsonArray();

        foreach (var name in _campaignNames)
        {
            campaignsArray.Add(name);
        }

        root["GoldenPool"] = new JsonObject
        {
            ["Campaigns"] = campaignsArray
        };

        var output = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_appsettingsPath, output);
    }

    public enum AddResult
    {
        Added,
        AlreadyExists,
        UnknownCampaign
    }

    public enum RemoveResult
    {
        Removed,
        NotInPool,
        UnknownCampaign
    }
}