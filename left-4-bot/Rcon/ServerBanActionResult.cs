namespace left_4_bot.Rcon;

/// <summary>
/// Outcome of a ban or unban RCON sequence.
/// </summary>
public sealed record ServerBanActionResult(bool IsSuccess, string Message);
