namespace left_4_bot.Call;

/// <summary>
/// Configuration for the call list, bound from the <c>CallList</c> appsettings section.
/// </summary>
public sealed class CallListOptions
{
    /// <summary>
    /// Name of the configuration section in appsettings.
    /// </summary>
    public const string SectionName = "CallList";

    /// <summary>
    /// Telegram usernames on the call list (without leading <c>@</c>).
    /// </summary>
    public List<string> Usernames { get; set; } = [];

    /// <summary>
    /// Telegram users on the call list, keyed by numeric id.
    /// </summary>
    public List<CallListUser> Users { get; set; } = [];

    /// <summary>
    /// Ping phrases randomly chosen when calling the list.
    /// </summary>
    public List<string> Phrases { get; set; } = [];
}
