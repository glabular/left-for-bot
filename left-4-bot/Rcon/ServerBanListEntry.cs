namespace left_4_bot.Rcon;

/// <summary>
/// One row from an RCON <c>listid</c> response.
/// </summary>
public sealed record ServerBanListEntry(
    int BanSlot,
    string ClassicSteamId,
    string? DurationLabel);
