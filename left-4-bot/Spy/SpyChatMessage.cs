namespace left_4_bot.Spy;

/// <summary>
/// A team-chat line parsed from a server log slice.
/// </summary>
/// <param name="PlayerName">In-game display name.</param>
/// <param name="ClassicSteamId">Classic Source Steam ID (<c>STEAM_X:Y:Z</c>).</param>
/// <param name="Text">Chat message text.</param>
public sealed record SpyChatMessage(string PlayerName, string ClassicSteamId, string Text);
