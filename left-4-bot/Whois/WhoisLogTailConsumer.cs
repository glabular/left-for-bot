using left_4_bot.ServerLogs;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace left_4_bot.Whois;

/// <summary>
/// Consumes shared log-tail slices and announces new player joins when whois is enabled.
/// </summary>
public sealed class WhoisLogTailConsumer : IServerLogTailConsumer
{
    private readonly WhoisState _state;
    private readonly WhoisService _whoisService;
    private readonly WhoisSessionTracker _sessionTracker;
    private readonly BotSettings _botSettings;
    private readonly ITelegramBotClient _bot;
    private readonly ILogger<WhoisLogTailConsumer> _logger;

    private bool _missingApiKeyNotified;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhoisLogTailConsumer"/> class.
    /// </summary>
    /// <param name="state">Shared whois enabled state.</param>
    /// <param name="whoisService">Service used to send whois reports.</param>
    /// <param name="sessionTracker">Tracker for online and announced players.</param>
    /// <param name="botSettings">Bot settings, including the group chat ID.</param>
    /// <param name="bot">Telegram bot client.</param>
    /// <param name="logger">Logger for this consumer.</param>
    public WhoisLogTailConsumer(
        WhoisState state,
        WhoisService whoisService,
        WhoisSessionTracker sessionTracker,
        BotSettings botSettings,
        ITelegramBotClient bot,
        ILogger<WhoisLogTailConsumer> logger)
    {
        _state = state;
        _whoisService = whoisService;
        _sessionTracker = sessionTracker;
        _botSettings = botSettings;
        _bot = bot;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool WantsPolling
    {
        get
        {
            var enabled = _state.Enabled;
            if (!enabled)
            {
                // Match previous watcher behavior: clear one-shot warnings while whois is off.
                _missingApiKeyNotified = false;
            }

            return enabled;
        }
    }

    /// <inheritdoc />
    public async Task OnNewSlicesAsync(
        IReadOnlyList<ServerLogTailSlice> slices,
        CancellationToken cancellationToken)
    {
        if (!_state.Enabled || _botSettings.GroupChatId == 0)
        {
            return;
        }

        var enteredThisTick = new List<WhoisPlayerRef>();

        foreach (var slice in slices)
        {
            var activities = WhoisLogActivityParser.Parse(slice.Text);
            if (activities.Count == 0)
            {
                continue;
            }

            _sessionTracker.UpdateOnlinePlayersFromLog(activities);

            foreach (var activity in activities)
            {
                if (activity.Kind == WhoisLogActivityKind.Entered)
                {
                    enteredThisTick.Add(activity.Player);
                }
            }
        }

        if (enteredThisTick.Count == 0)
        {
            return;
        }

        if (!_whoisService.HasApiKey)
        {
            if (!_missingApiKeyNotified)
            {
                _missingApiKeyNotified = true;
                _logger.LogWarning("Whois Steam Web API key is not configured");
                await _bot.SendMessage(
                    _botSettings.GroupChatId,
                    WhoisService.MissingApiKeyMessage,
                    cancellationToken: cancellationToken);
            }

            return;
        }

        _missingApiKeyNotified = false;

        var filtered = _whoisService.ExcludePriorityPlayers(enteredThisTick);
        var toAnnounce = _sessionTracker.FilterUnannouncedPlayers(filtered);

        if (toAnnounce.Count == 0)
        {
            return;
        }

        await _whoisService.SendWhoisReportAsync(
            _bot,
            _botSettings.GroupChatId,
            toAnnounce,
            cancellationToken);

        _sessionTracker.MarkAnnounced(toAnnounce.Select(p => p.ClassicSteamId));
    }
}
