using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace left_4_bot.L4D2;

public sealed class GoldenPoolService
{
    private static readonly JsonDocumentOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string _projectAppSettingsPath;
    private readonly string _runtimeAppSettingsPath;
    private List<string> _campaignNames;
    private readonly object _sync = new();

    public GoldenPoolService(IOptions<GoldenPoolOptions> options)
    {
        _projectAppSettingsPath = AppSettingsPathResolver.ResolveProjectAppSettingsPath();
        _runtimeAppSettingsPath = AppSettingsPathResolver.ResolveRuntimeAppSettingsPath();
        _campaignNames = new List<string>(options.Value.Campaigns);
    }

    public IReadOnlyList<L4d2Campaign> GetCampaigns()
    {
        lock (_sync)
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
    }

    public bool Contains(string englishName)
    {
        lock (_sync)
        {
            return _campaignNames.Any(n => n.Equals(englishName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public GoldenPoolToggleResult TryToggle(string englishName)
    {
        lock (_sync)
        {
            if (!CampaignCatalog.TryGetByEnglishName(englishName, out var campaign))
            {
                return GoldenPoolToggleResult.UnknownCampaign;
            }

            var index = _campaignNames.FindIndex(
                n => n.Equals(campaign.EnglishName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                var removedName = _campaignNames[index];
                _campaignNames.RemoveAt(index);

                if (!TrySave())
                {
                    _campaignNames.Insert(index, removedName);
                    return GoldenPoolToggleResult.SaveFailed;
                }

                return GoldenPoolToggleResult.Removed;
            }

            _campaignNames.Add(campaign.EnglishName);

            if (!TrySave())
            {
                _campaignNames.RemoveAt(_campaignNames.Count - 1);
                return GoldenPoolToggleResult.SaveFailed;
            }

            return GoldenPoolToggleResult.Added;
        }
    }

    private bool TrySave()
    {
        try
        {
            var readPath = File.Exists(_projectAppSettingsPath)
                ? _projectAppSettingsPath
                : _runtimeAppSettingsPath;

            JsonObject root;

            if (File.Exists(readPath))
            {
                var json = File.ReadAllText(readPath);
                root = JsonNode.Parse(json, documentOptions: JsonReadOptions)?.AsObject()
                    ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var campaignsArray = new JsonArray();

            foreach (var name in _campaignNames)
            {
                campaignsArray.Add(name);
            }

            root["GoldenPool"] = new JsonObject
            {
                ["Campaigns"] = campaignsArray
            };

            var output = root.ToJsonString(JsonWriteOptions);

            File.WriteAllText(_projectAppSettingsPath, output);

            if (!string.Equals(_projectAppSettingsPath, _runtimeAppSettingsPath, StringComparison.OrdinalIgnoreCase)
                && File.Exists(_runtimeAppSettingsPath))
            {
                File.WriteAllText(_runtimeAppSettingsPath, output);
            }

            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return false;
        }
    }
}