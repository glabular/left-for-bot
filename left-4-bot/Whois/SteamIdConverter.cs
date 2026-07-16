using System.Globalization;

namespace left_4_bot.Whois;

public static class SteamIdConverter
{
    private const ulong SteamId64Base = 76561197960265728UL;

    /// <summary>
    /// Converts classic Source ID <c>STEAM_X:Y:Z</c> to Steam64.
    /// </summary>
    public static bool TryToSteam64(string classicSteamId, out ulong steam64)
    {
        steam64 = 0;

        if (string.IsNullOrWhiteSpace(classicSteamId)
            || !classicSteamId.StartsWith("STEAM_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = classicSteamId.Split(':');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || y is not (0 or 1))
        {
            return false;
        }

        if (!ulong.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var accountId))
        {
            return false;
        }

        steam64 = accountId * 2UL + (ulong)y + SteamId64Base;
        return true;
    }
}
