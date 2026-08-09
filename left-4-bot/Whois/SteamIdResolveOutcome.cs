namespace left_4_bot.Whois;

/// <summary>
/// Outcome of resolving user input to a classic Source Steam ID.
/// </summary>
public sealed record SteamIdResolveOutcome(bool IsSuccess, string? ClassicSteamId, string? ErrorMessage)
{
    public static SteamIdResolveOutcome Ok(string classicSteamId) =>
        new(true, classicSteamId, null);

    public static SteamIdResolveOutcome Fail(string errorMessage) =>
        new(false, null, errorMessage);
}
