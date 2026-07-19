namespace left_4_bot.L4D2;

/// <summary>
/// Result of toggling a campaign in the golden pool.
/// </summary>
public enum GoldenPoolToggleResult
{
    /// <summary>
    /// The campaign was added.
    /// </summary>
    Added,

    /// <summary>
    /// The campaign was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The name does not match any official campaign.
    /// </summary>
    UnknownCampaign,

    /// <summary>
    /// The in-memory change could not be persisted.
    /// </summary>
    SaveFailed,
}
