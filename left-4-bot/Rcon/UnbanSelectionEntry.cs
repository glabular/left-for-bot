namespace left_4_bot.Rcon;

/// <summary>
/// One numbered row from the last <c>/unban</c> list (includes display name for chat messages).
/// </summary>
public sealed record UnbanSelectionEntry(
    int BanSlot,
    string ClassicSteamId,
    string DisplayName,
    string? DurationLabel);
