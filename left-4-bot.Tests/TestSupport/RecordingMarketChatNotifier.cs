using left_4_bot.Persistence;
using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Tests.TestSupport;

/// <summary>
/// Records market announcement kinds and marks DB flags like production (no Telegram).
/// </summary>
public sealed class RecordingMarketChatNotifier : IMarketChatNotifier
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;

    public List<RecordedMarketAnnouncement> Announcements { get; } = [];

    public RecordingMarketChatNotifier(IDbContextFactory<BotDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task TryAnnounceOpenAsync(DateOnly gamingDayAnchor, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null || market.Status != DailyMarketStatus.Open || market.OpenAnnounced)
        {
            return;
        }

        market.OpenAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TryAnnounceReminderAsync(
        DateOnly gamingDayAnchor,
        int bank,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null
            || market.Status != DailyMarketStatus.Open
            || market.ReminderAnnounced
            || market.CloseAnnounced)
        {
            return;
        }

        market.ReminderAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TryAnnounceCloseAsync(
        DateOnly gamingDayAnchor,
        int bank,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null || market.Status != DailyMarketStatus.Open || market.CloseAnnounced)
        {
            return;
        }

        var bettorCount = await db.DailyBets
            .AsNoTracking()
            .Where(b => b.GamingDayAnchor == gamingDayAnchor)
            .Select(b => b.TelegramUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (bettorCount < ToteEconomy.MinBettorsPerMarket)
        {
            market.CloseAnnounced = true;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        Announcements.Add(RecordedMarketAnnouncement.Close);
        market.CloseAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TryAnnounceGameStartedCloseAsync(
        DateOnly gamingDayAnchor,
        int bank,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null)
        {
            market = new DailyMarket
            {
                GamingDayAnchor = gamingDayAnchor,
                Status = DailyMarketStatus.Open,
            };
            db.DailyMarkets.Add(market);
        }

        if (market.Status != DailyMarketStatus.Open || market.CloseAnnounced)
        {
            return;
        }

        var bettorCount = await db.DailyBets
            .AsNoTracking()
            .Where(b => b.GamingDayAnchor == gamingDayAnchor)
            .Select(b => b.TelegramUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (bettorCount < ToteEconomy.MinBettorsPerMarket)
        {
            market.CloseAnnounced = true;
            market.ReminderAnnounced = true;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        Announcements.Add(RecordedMarketAnnouncement.GameStartedClose);
        market.CloseAnnounced = true;
        market.ReminderAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task TryAnnounceSettlementAsync(
        BetSettlementResult result,
        CancellationToken cancellationToken)
    {
        if (result.Status == BetSettlementStatus.Settled)
        {
            // ok
        }
        else if (result.Status == BetSettlementStatus.Cancelled && result.Bank > 0)
        {
            // ok
        }
        else
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == result.GamingDayAnchor, cancellationToken);

        if (market is null || market.SettlementAnnounced)
        {
            return;
        }

        Announcements.Add(result.Status == BetSettlementStatus.Settled
            ? RecordedMarketAnnouncement.Settled
            : RecordedMarketAnnouncement.Cancelled);

        market.SettlementAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
