namespace left_4_bot.Persistence;

/// <summary>
/// A protect-teammate top player used for settlement messaging.
/// </summary>
public sealed record BetSettlementTopPlayer(string SteamId, string Name, int Count);
