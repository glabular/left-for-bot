namespace left_4_bot.Whois;

/// <summary>
/// Thread-safe on/off state for automatic whois checks.
/// </summary>
public sealed class WhoisState
{
    private readonly Lock _gate = new();
    private bool _enabled;

    /// <summary>
    /// Gets a value indicating whether automatic whois checks are enabled.
    /// </summary>
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                return _enabled;
            }
        }
    }

    /// <summary>
    /// Toggles automatic whois checks and returns the new enabled state.
    /// </summary>
    /// <returns><see langword="true"/> if checks are now enabled; otherwise, <see langword="false"/>.</returns>
    public bool ToggleAutoCheckEnabled()
    {
        lock (_gate)
        {
            _enabled = !_enabled;

            return _enabled;
        }
    }
}
