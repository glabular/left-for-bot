using System.Globalization;
using System.Text.RegularExpressions;
using left_4_bot.Whois;

namespace left_4_bot.Persistence;

public abstract record SteamLinkParseResult;

public sealed record SteamLinkParsedSteam64(ulong Steam64) : SteamLinkParseResult;

public sealed record SteamLinkParsedVanity(string Vanity) : SteamLinkParseResult;

public sealed record SteamLinkParseFailed(string ErrorMessage) : SteamLinkParseResult;

public static partial class SteamProfileLinkParser
{
    [GeneratedRegex(
        @"^\s*(?:https?://)?(?:www\.)?steamcommunity\.com/profiles/(?<id>\d+)/?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProfilesUrlRegex();

    [GeneratedRegex(
        @"^\s*(?:https?://)?(?:www\.)?steamcommunity\.com/id/(?<vanity>[A-Za-z0-9_-]+)/?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VanityUrlRegex();

    [GeneratedRegex(@"^\s*(?<id>7656119\d+)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BareSteam64Regex();

    [GeneratedRegex(
        @"^\s*(?<id>STEAM_\d+:[01]:\d+)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClassicSteamIdRegex();

    public static SteamLinkParseResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new SteamLinkParseFailed("Укажи ссылку на профиль Steam или Steam ID.");
        }

        var text = input.Trim();
        var profiles = ProfilesUrlRegex().Match(text);

        if (profiles.Success
            && ulong.TryParse(
                profiles.Groups["id"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var fromProfiles))
        {
            return new SteamLinkParsedSteam64(fromProfiles);
        }

        var vanity = VanityUrlRegex().Match(text);

        if (vanity.Success)
        {
            return new SteamLinkParsedVanity(vanity.Groups["vanity"].Value);
        }

        var bare = BareSteam64Regex().Match(text);

        if (bare.Success
            && ulong.TryParse(
                bare.Groups["id"].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var fromBare))
        {
            return new SteamLinkParsedSteam64(fromBare);
        }

        var classic = ClassicSteamIdRegex().Match(text);

        if (classic.Success
            && SteamIdConverter.TryToSteam64(classic.Groups["id"].Value, out var fromClassic))
        {
            return new SteamLinkParsedSteam64(fromClassic);
        }

        return new SteamLinkParseFailed(
            "Не понял Steam-профиль. Нужен адрес вида:\n"
            + "https://steamcommunity.com/profiles/7656119…\n"
            + "или https://steamcommunity.com/id/имя\n"
            + "или Steam ID: 7656119… / STEAM_1:1:…");
    }
}
