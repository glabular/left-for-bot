using left_4_bot.ServerLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

/// <summary>
/// Settles unfinished past daily markets and ensures the current day has an Open row.
/// </summary>
public sealed class DailyMarketRolloverService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly BetSettlementService _settlement;
    private readonly IMarketChatNotifier _notifier;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly ILogger<DailyMarketRolloverService> _logger;

    private DateOnly? _lastLoggedCurrentAnchor;

    public DailyMarketRolloverService(
        IDbContextFactory<BotDbContext> dbFactory,
        BetSettlementService settlement,
        IMarketChatNotifier notifier,
        IOptions<GamingDayOptions> gamingDayOptions,
        ILogger<DailyMarketRolloverService> logger)
    {
        _dbFactory = dbFactory;
        _settlement = settlement;
        _notifier = notifier;
        _gamingDayOptions = gamingDayOptions.Value;
        _logger = logger;
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var current = GamingDay.GetCurrentAnchor(_gamingDayOptions);

        if (_lastLoggedCurrentAnchor is null)
        {
            _logger.LogInformation("Daily market rollover started (current gaming day {Anchor})", current);
            _lastLoggedCurrentAnchor = current;
        }
        else if (_lastLoggedCurrentAnchor != current)
        {
            _logger.LogInformation(
                "Gaming day changed from {Previous} to {Current}",
                _lastLoggedCurrentAnchor,
                current);
            _lastLoggedCurrentAnchor = current;
        }

        var unsettled = await FindUnsettledPastAnchorsAsync(current, cancellationToken);

        foreach (var anchor in unsettled)
        {
            var result = await _settlement.SettleAsync(anchor, cancellationToken);

            _logger.LogInformation(
                "Settle {Anchor}: Status={Status}, CancelReason={CancelReason}, Bank={Bank}",
                anchor,
                result.Status,
                result.CancelReason,
                result.Bank);

            await _notifier.TryAnnounceSettlementAsync(result, cancellationToken);
        }

        await EnsureCurrentMarketOpenAsync(current, cancellationToken);

        var hour = GamingDay.NowMoscow().Hour;
        var bettingWindowOpen = hour < ToteEconomy.BetWindowCloseHourMsk;

        if (bettingWindowOpen)
        {
            await _notifier.TryAnnounceOpenAsync(current, cancellationToken);
        }
        else
        {
            // Late start after BetWindowCloseHourMsk: do not spam "open" then "closed" in one tick.
            await SuppressUnannouncedWindowFlagsAsync(current, cancellationToken);
        }

        await MaybeAnnounceWindowMessagesAsync(current, cancellationToken);
    }

    /// <summary>
    /// When the bot first sees today's market after the betting window already closed,
    /// mark open/reminder/close as announced without sending — otherwise the group gets
    /// both "market open" and "bets closed" back-to-back.
    /// </summary>
    private async Task SuppressUnannouncedWindowFlagsAsync(
        DateOnly current,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == current, cancellationToken);

        if (market is null || market.Status != DailyMarketStatus.Open || market.OpenAnnounced)
        {
            return;
        }

        market.OpenAnnounced = true;
        market.ReminderAnnounced = true;
        market.CloseAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Skipped open/close announcements for {Anchor}: betting window already closed",
            current);
    }

    private async Task MaybeAnnounceWindowMessagesAsync(
        DateOnly current,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var market = await db.DailyMarkets
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == current, cancellationToken);

        if (market is null || market.Status != DailyMarketStatus.Open)
        {
            return;
        }

        var hour = GamingDay.NowMoscow().Hour;

        if (hour >= ToteEconomy.BetWindowCloseHourMsk && market.OpenAnnounced)
        {
            var bank = await db.DailyBets
                .AsNoTracking()
                .Where(b => b.GamingDayAnchor == current)
                .SumAsync(b => (int?)b.Amount, cancellationToken) ?? 0;

            await _notifier.TryAnnounceCloseAsync(current, bank, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<DateOnly>> FindUnsettledPastAnchorsAsync(
        DateOnly current,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var fromMarkets = await db.DailyMarkets
            .AsNoTracking()
            .Where(m =>
                m.GamingDayAnchor < current
                && (m.Status == DailyMarketStatus.Open || m.Status == DailyMarketStatus.Closed))
            .Select(m => m.GamingDayAnchor)
            .ToListAsync(cancellationToken);

        var finalizedAnchors = await db.DailyMarkets
            .AsNoTracking()
            .Where(m =>
                m.GamingDayAnchor < current
                && (m.Status == DailyMarketStatus.Settled || m.Status == DailyMarketStatus.Cancelled))
            .Select(m => m.GamingDayAnchor)
            .ToListAsync(cancellationToken);

        var finalizedSet = finalizedAnchors.ToHashSet();

        var fromBets = await db.DailyBets
            .AsNoTracking()
            .Where(b => b.GamingDayAnchor < current)
            .Select(b => b.GamingDayAnchor)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromBetsUnsettled = fromBets.Where(a => !finalizedSet.Contains(a));

        return fromMarkets
            .Concat(fromBetsUnsettled)
            .Distinct()
            .OrderBy(a => a)
            .ToList();
    }

    private async Task EnsureCurrentMarketOpenAsync(
        DateOnly current,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var exists = await db.DailyMarkets
            .AnyAsync(m => m.GamingDayAnchor == current, cancellationToken);

        if (exists)
        {
            return;
        }

        db.DailyMarkets.Add(new DailyMarket
        {
            GamingDayAnchor = current,
            Status = DailyMarketStatus.Open,
        });

        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Opened daily market for gaming day {Anchor}", current);
    }
}
