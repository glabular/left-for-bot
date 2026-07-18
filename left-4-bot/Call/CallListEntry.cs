namespace left_4_bot.Call;

/// <summary>
/// One person on the call list, identified by Telegram username or user id.
/// </summary>
/// <param name="Username">Telegram @username without the leading <c>@</c>, or <see langword="null"/> when identified by id.</param>
/// <param name="UserId">Telegram numeric user id, or <see langword="null"/> when identified by username.</param>
/// <param name="DisplayLabel">Text shown when mentioning this entry.</param>
public readonly record struct CallListEntry(string? Username, long? UserId, string DisplayLabel)
{
    /// <summary>
    /// Creates an entry identified by Telegram username.
    /// </summary>
    /// <param name="username">Username without the leading <c>@</c>.</param>
    /// <returns>A username-based call-list entry.</returns>
    public static CallListEntry FromUsername(string username) =>
        new(username, null, username);

    /// <summary>
    /// Creates an entry identified by Telegram user id.
    /// </summary>
    /// <param name="userId">Telegram numeric user id.</param>
    /// <param name="displayName">Display name stored for mentions.</param>
    /// <returns>A user-id-based call-list entry.</returns>
    public static CallListEntry FromUser(long userId, string displayName) =>
        new(null, userId, displayName);
}
