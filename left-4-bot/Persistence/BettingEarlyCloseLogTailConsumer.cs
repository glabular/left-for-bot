using left_4_bot.ServerLogs;
using left_4_bot.Whois;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

/// <summary>
/// Closes today's betting window early when a priority player enters the game in live logs.
/// </summary>
public sealed class BettingEarlyCloseLogTailConsumer : IServerLogTailConsumer
{
    private readonly PlayersService _playersService;
    private readonly IMarketChatNotifier _notifier;
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly ILogger<BettingEarlyCloseLogTailConsumer> _logger;

    public BettingEarlyCloseLogTailConsumer(
        PlayersService playersService,
        IMarketChatNotifier notifier,
        IDbContextFactory<BotDbContext> dbFactory,
        IOptions<GamingDayOptions> gamingDayOptions,
        ILogger<BettingEarlyCloseLogTailConsumer> logger)
    {
        _playersService = playersService;
        _notifier = notifier;
        _dbFactory = dbFactory;
        _gamingDayOptions = gamingDayOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool WantsPolling =>
        GamingDay.NowMoscow().Hour < ToteEconomy.BetWindowCloseHourMsk;

    /// <inheritdoc />
    public async Task OnNewSlicesAsync(
        IReadOnlyList<ServerLogTailSlice> slices,
        CancellationToken cancellationToken)
    {
        if (!WantsPolling)
        {
            return;
        }

        var priorityCanonical = BuildPriorityCanonicalSet();

        if (priorityCanonical.Count == 0)
        {
            return;
        }

        var sawPriorityEnter = false;

        foreach (var slice in slices)
        {
            var activities = WhoisLogActivityParser.Parse(slice.Text);

            foreach (var activity in activities)
            {
                if (activity.Kind != WhoisLogActivityKind.Entered)
                {
                    continue;
                }

                if (!SteamIdConverter.TryToCanonicalClassic(
                        activity.Player.ClassicSteamId,
                        out var enteredCanonical))
                {
                    continue;
                }

                if (priorityCanonical.Contains(enteredCanonical))
                {
                    sawPriorityEnter = true;
                    break;
                }
            }

            if (sawPriorityEnter)
            {
                break;
            }
        }

        if (!sawPriorityEnter)
        {
            return;
        }

        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var bank = await db.DailyBets
            .AsNoTracking()
            .Where(b => b.GamingDayAnchor == today)
            .SumAsync(b => (int?)b.Amount, cancellationToken) ?? 0;

        _logger.LogInformation(
            "Priority player entered the game; closing betting window for {Anchor} (bank {Bank})",
            today,
            bank);

        await _notifier.TryAnnounceGameStartedCloseAsync(today, bank, cancellationToken);
    }

    private HashSet<string> BuildPriorityCanonicalSet()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        foreach (var steamId in _playersService.GetPrioritySteamIds())
        {
            if (SteamIdConverter.TryToCanonicalClassic(steamId, out var canonical))
            {
                set.Add(canonical);
            }
        }

        return set;
    }
}
