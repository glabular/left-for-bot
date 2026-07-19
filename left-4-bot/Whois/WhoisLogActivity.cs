namespace left_4_bot.Whois;

/// <summary>
/// A player activity event extracted from server logs.
/// </summary>
/// <param name="Kind">Activity kind.</param>
/// <param name="Player">Player associated with the activity.</param>
internal sealed record WhoisLogActivity(WhoisLogActivityKind Kind, WhoisPlayerRef Player);
