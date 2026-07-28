using left_4_bot.Persistence;
using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Tests.TestSupport;

/// <summary>
/// Records market announcements and marks DB flags like production (no Telegram).
/// </summary>
public sealed class RecordingMarketChatNotifier : IMarketChatNotifier
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;

    public List<string> Messages { get; } = [];

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

        Messages.Add(MarketAnnouncementFormatter.FormatOpen(
            ToteEconomy.BetWindowCloseHourMsk));
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

        Messages.Add(MarketAnnouncementFormatter.FormatReminder(
            ToteEconomy.BetWindowCloseHourMsk,
            bank));
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

        Messages.Add(MarketAnnouncementFormatter.FormatClose(bank));
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

        Messages.Add(MarketAnnouncementFormatter.FormatGameStartedClose(bank));
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

        if (result.Status == BetSettlementStatus.Settled)
        {
            var topLabels = result.TopPlayers
                .Select(p => string.IsNullOrWhiteSpace(p.Name) ? p.SteamId : p.Name)
                .ToList();
            var winnerLines = result.Credits
                .Select(c => $"id {c.TelegramUserId}: +{c.Credited} таблов")
                .ToList();
            Messages.Add(MarketAnnouncementFormatter.FormatSettled(topLabels, winnerLines, result.Bank));
        }
        else
        {
            Messages.Add(MarketAnnouncementFormatter.FormatCancelled(result.CancelReason, result.Bank));
        }

        market.SettlementAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
