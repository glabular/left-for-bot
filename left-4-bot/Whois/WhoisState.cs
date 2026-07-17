namespace left_4_bot.Whois;

public sealed class WhoisState
{
    private readonly object _gate = new();
    private bool _enabled;

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

    public bool ToggleAutoCheckEnabled()
    {
        lock (_gate)
        {
            _enabled = !_enabled;
            return _enabled;
        }
    }
}
