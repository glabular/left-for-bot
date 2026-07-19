using left_4_bot.ServerLogs;
using Microsoft.Extensions.Options;

namespace left_4_bot.Spy;

/// <summary>
/// Thread-safe on/off state for in-game chat relay. Automatically turns off when the gaming day changes.
/// </summary>
public sealed class SpyState
{
    private readonly Lock _gate = new();
    private readonly IOptions<GamingDayOptions> _gamingDayOptions;
    private bool _enabled;
    private DateOnly? _gamingDayAnchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpyState"/> class.
    /// </summary>
    /// <param name="gamingDayOptions">Gaming-day options used for the daily auto-off reset.</param>
    public SpyState(IOptions<GamingDayOptions> gamingDayOptions)
    {
        _gamingDayOptions = gamingDayOptions;
    }

    /// <summary>
    /// Gets a value indicating whether chat relay is enabled for the current gaming day.
    /// </summary>
    public bool Enabled
    {
        get
        {
            lock (_gate)
            {
                ResetIfGamingDayChanged_NoLock();
                return _enabled;
            }
        }
    }

    /// <summary>
    /// Toggles chat relay and returns the new enabled state.
    /// </summary>
    /// <returns><see langword="true"/> if relay is now enabled; otherwise, <see langword="false"/>.</returns>
    public bool Toggle()
    {
        lock (_gate)
        {
            ResetIfGamingDayChanged_NoLock();
            _enabled = !_enabled;
            if (_enabled)
            {
                _gamingDayAnchor = GamingDay.GetCurrentAnchor(_gamingDayOptions.Value);
            }

            return _enabled;
        }
    }

    /// <summary>
    /// Sets chat relay on or off.
    /// </summary>
    /// <param name="enabled"><see langword="true"/> to enable; <see langword="false"/> to disable.</param>
    /// <returns>The resulting enabled state.</returns>
    public bool Set(bool enabled)
    {
        lock (_gate)
        {
            ResetIfGamingDayChanged_NoLock();
            _enabled = enabled;
            if (_enabled)
            {
                _gamingDayAnchor = GamingDay.GetCurrentAnchor(_gamingDayOptions.Value);
            }

            return _enabled;
        }
    }

    /// <summary>
    /// Turns spy off when the gaming-day anchor changes. Must be called while holding the lock.
    /// </summary>
    private void ResetIfGamingDayChanged_NoLock()
    {
        var anchor = GamingDay.GetCurrentAnchor(_gamingDayOptions.Value);

        if (_gamingDayAnchor is null)
        {
            _gamingDayAnchor = anchor;
            return;
        }

        if (_gamingDayAnchor == anchor)
        {
            return;
        }

        _gamingDayAnchor = anchor;
        _enabled = false;
    }
}
