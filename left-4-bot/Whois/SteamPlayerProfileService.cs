using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.Whois;

public sealed class SteamPlayerProfileService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WhoisOptions _options;
    private readonly ILogger<SteamPlayerProfileService> _logger;

    public SteamPlayerProfileService(
        IHttpClientFactory httpClientFactory,
        IOptions<WhoisOptions> options,
        ILogger<SteamPlayerProfileService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_options.SteamWebApiKey);

    private HttpClient CreateClient() =>
        _httpClientFactory.CreateClient(nameof(SteamPlayerProfileService));

    public async Task<IReadOnlyList<SteamPlayerProfile>> GetProfilesAsync(
        IReadOnlyList<WhoisPlayerRef> players,
        CancellationToken cancellationToken)
    {
        if (players.Count == 0)
        {
            return [];
        }

        if (!HasApiKey)
        {
            return players
                .Select(p => new SteamPlayerProfile(p.Name, p.ClassicSteamId, null, null, null))
                .ToList();
        }

        var steam64ByClassic = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var player in players)
        {
            if (SteamIdConverter.TryToSteam64(player.ClassicSteamId, out var steam64))
            {
                steam64ByClassic[player.ClassicSteamId] = steam64;
            }
        }

        var vacBySteam64 = await GetVacCountsAsync(steam64ByClassic.Values.ToList(), cancellationToken);

        var results = new List<SteamPlayerProfile>(players.Count);
        foreach (var player in players)
        {
            if (!steam64ByClassic.TryGetValue(player.ClassicSteamId, out var steam64))
            {
                results.Add(new SteamPlayerProfile(player.Name, player.ClassicSteamId, null, null, null));
                continue;
            }

            vacBySteam64.TryGetValue(steam64, out var vac);
            var (l4d2, l4d1) = await GetPlaytimeHoursAsync(steam64, cancellationToken);
            results.Add(new SteamPlayerProfile(
                player.Name,
                player.ClassicSteamId,
                l4d2,
                l4d1,
                vac));
        }

        return results;
    }

    private async Task<Dictionary<ulong, int?>> GetVacCountsAsync(
        IReadOnlyList<ulong> steam64Ids,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<ulong, int?>();
        if (steam64Ids.Count == 0)
        {
            return result;
        }

        try
        {
            var steamIds = string.Join(',', steam64Ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            var url =
                $"https://api.steampowered.com/ISteamUser/GetPlayerBans/v1/?key={Uri.EscapeDataString(_options.SteamWebApiKey)}&steamids={steamIds}";

            using var response = await CreateClient().GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetPlayerBans failed with {StatusCode}", response.StatusCode);
                foreach (var id in steam64Ids)
                {
                    result[id] = null;
                }

                return result;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("players", out var playersElement))
            {
                foreach (var id in steam64Ids)
                {
                    result[id] = null;
                }

                return result;
            }

            foreach (var player in playersElement.EnumerateArray())
            {
                if (!player.TryGetProperty("SteamId", out var steamIdElement)
                    || !ulong.TryParse(
                        steamIdElement.GetString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var steam64))
                {
                    continue;
                }

                var vac = player.TryGetProperty("NumberOfVACBans", out var vacElement)
                    ? vacElement.GetInt32()
                    : (int?)null;
                result[steam64] = vac;
            }

            foreach (var id in steam64Ids)
            {
                result.TryAdd(id, null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetPlayerBans request failed");
            foreach (var id in steam64Ids)
            {
                result[id] = null;
            }
        }

        return result;
    }

    private async Task<(int? L4d2Hours, int? L4d1Hours)> GetPlaytimeHoursAsync(
        ulong steam64,
        CancellationToken cancellationToken)
    {
        try
        {
            var input = JsonSerializer.Serialize(new
            {
                steamid = steam64.ToString(CultureInfo.InvariantCulture),
                include_played_free_games = true,
                appids_filter = new[] { _options.L4d2AppId, _options.L4d1AppId },
            });

            var url = "https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/"
                + $"?key={Uri.EscapeDataString(_options.SteamWebApiKey)}"
                + $"&input_json={Uri.EscapeDataString(input)}";

            using var response = await CreateClient().GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetOwnedGames failed with {StatusCode}", response.StatusCode);
                return (null, null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("response", out var responseElement))
            {
                return (null, null);
            }

            if (!responseElement.TryGetProperty("games", out var gamesElement))
            {
                return (null, null);
            }

            var l4d2Minutes = 0;
            var l4d1Minutes = 0;
            var foundL4d2 = false;
            var foundL4d1 = false;

            foreach (var game in gamesElement.EnumerateArray())
            {
                if (!game.TryGetProperty("appid", out var appIdElement))
                {
                    continue;
                }

                var appId = appIdElement.GetInt32();
                var minutes = game.TryGetProperty("playtime_forever", out var playtimeElement)
                    ? playtimeElement.GetInt32()
                    : 0;

                if (appId == _options.L4d2AppId)
                {
                    l4d2Minutes = minutes;
                    foundL4d2 = true;
                }
                else if (appId == _options.L4d1AppId)
                {
                    l4d1Minutes = minutes;
                    foundL4d1 = true;
                }
            }

            return (
                (foundL4d2 ? l4d2Minutes : 0) / 60,
                (foundL4d1 ? l4d1Minutes : 0) / 60);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "GetOwnedGames request failed for {Steam64}", steam64);
            return (null, null);
        }
    }
}
