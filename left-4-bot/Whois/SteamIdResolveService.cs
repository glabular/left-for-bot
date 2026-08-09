using left_4_bot.Persistence;

namespace left_4_bot.Whois;

/// <summary>
/// Resolves Steam profile URLs, vanity names, Steam64, and classic IDs to <c>STEAM_1:Y:Z</c>.
/// </summary>
public sealed class SteamIdResolveService
{
    private readonly SteamPlayerProfileService _steamProfiles;

    public SteamIdResolveService(SteamPlayerProfileService steamProfiles)
    {
        _steamProfiles = steamProfiles;
    }

    /// <summary>
    /// Parses <paramref name="input"/> and returns a canonical classic Steam ID when possible.
    /// </summary>
    public async Task<SteamIdResolveOutcome> TryResolveClassicAsync(
        string input,
        CancellationToken cancellationToken)
    {
        var parsed = SteamProfileLinkParser.Parse(input);
        ulong? steam64 = null;

        switch (parsed)
        {
            case SteamLinkParseFailed failed:
                return SteamIdResolveOutcome.Fail(failed.ErrorMessage);

            case SteamLinkParsedSteam64 direct:
                steam64 = direct.Steam64;
                break;

            case SteamLinkParsedVanity vanity:
                if (!_steamProfiles.HasApiKey)
                {
                    return SteamIdResolveOutcome.Fail(WhoisService.MissingApiKeyMessage);
                }

                steam64 = await _steamProfiles.ResolveVanityAsync(vanity.Vanity, cancellationToken);

                if (steam64 is null)
                {
                    return SteamIdResolveOutcome.Fail(
                        "Не удалось найти Steam-профиль по этой ссылке /id/… "
                        + "Проверь адрес или используй ссылку вида /profiles/7656119… "
                        + "или Steam ID.");
                }

                break;

            default:
                return SteamIdResolveOutcome.Fail("Не удалось разобрать Steam ID.");
        }

        if (steam64 is null
            || !SteamIdConverter.TryFromSteam64(steam64.Value, out var classicSteamId))
        {
            return SteamIdResolveOutcome.Fail("Не удалось разобрать Steam ID.");
        }

        return SteamIdResolveOutcome.Ok(classicSteamId);
    }
}
