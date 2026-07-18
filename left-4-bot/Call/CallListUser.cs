namespace left_4_bot.Call;

/// <summary>
/// A call-list member identified by Telegram user id.
/// </summary>
public sealed class CallListUser
{
    /// <summary>
    /// Telegram numeric user id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Display name used when mentioning this user.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
