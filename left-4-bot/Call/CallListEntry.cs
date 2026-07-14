namespace left_4_bot.Call;

/// <summary>
/// One person on the call list — either by Telegram @username or by numeric user id.
/// </summary>
public readonly record struct CallListEntry(string? Username, long? UserId, string DisplayLabel)
{
    public static CallListEntry FromUsername(string username) =>
        new(username, null, username);

    public static CallListEntry FromUser(long userId, string displayName) =>
        new(null, userId, displayName);
}
