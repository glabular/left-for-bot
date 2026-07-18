namespace left_4_bot.Call;

/// <summary>
/// Result of toggling an item on the call list.
/// </summary>
public enum CallToggleResult
{
    /// <summary>
    /// The item was added.
    /// </summary>
    Added,

    /// <summary>
    /// The item was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// The input was invalid and nothing changed.
    /// </summary>
    Invalid,

    /// <summary>
    /// The in-memory change could not be persisted.
    /// </summary>
    SaveFailed,
}
