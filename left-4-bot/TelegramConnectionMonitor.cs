using Microsoft.Extensions.Logging;

namespace left_4_bot;

/// <summary>
/// Unified logging for Telegram connectivity blips.
/// Set "left_4_bot": "Debug" in appsettings.json to see per-blip exception detail.
/// </summary>
public sealed class TelegramConnectionMonitor
{
    private readonly ILogger<TelegramConnectionMonitor> _logger;
    private bool _wasDisconnected;

    public bool IsDisconnected => _wasDisconnected;

    public TelegramConnectionMonitor(ILogger<TelegramConnectionMonitor> logger)
    {
        _logger = logger;
    }

    public void ReportTransientFailure(Exception exception, string source)
    {
        var reason = TelegramNetworkErrorClassifier.GetShortReason(exception)!;

        if (!_wasDisconnected)
        {
            _wasDisconnected = true;
            _logger.LogWarning(
                "Telegram connection problem ({Reason}); retrying.",
                reason);
        }

        _logger.LogDebug(
            exception,
            "Telegram network blip ({Reason}) [{Source}].",
            reason,
            source);
    }

    public bool ReportRecovered()
    {
        if (!_wasDisconnected)
        {
            return false;
        }

        _logger.LogInformation("Telegram connection restored.");
        _wasDisconnected = false;
        return true;
    }

    public void ReportFatalFailure(Exception exception, string source)
    {
        _logger.LogError(exception, "Telegram error [{Source}].", source);
    }
}
