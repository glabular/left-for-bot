namespace left_4_bot.ServerLogs;

/// <summary>
/// A player seen in server logs within a time window.
/// </summary>
public sealed record RecentPlayerEntry(string Name, string ClassicSteamId);
