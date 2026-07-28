using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace left_4_bot.Persistence;

/// <summary>
/// Telegram group announcements for daily market lifecycle events.
/// </summary>
public sealed class MarketChatNotifier : IMarketChatNotifier
{
    private readonly ITelegramBotClient _bot;
    private readonly BotSettings _botSettings;
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly ILogger<MarketChatNotifier> _logger;

    public MarketChatNotifier(
        ITelegramBotClient bot,
        BotSettings botSettings,
        IDbContextFactory<BotDbContext> dbFactory,
        ILogger<MarketChatNotifier> logger)
    {
        _bot = bot;
        _botSettings = botSettings;
        _dbFactory = dbFactory;
        _logger = logger;
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

        var text = MarketAnnouncementFormatter.FormatOpen(
            ToteEconomy.BetWindowCloseHourMsk);

        if (!await TrySendAsync(text, cancellationToken))
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

        var text = MarketAnnouncementFormatter.FormatReminder(
            ToteEconomy.BetWindowCloseHourMsk,
            bank);

        if (!await TrySendAsync(text, cancellationToken))
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

        var text = MarketAnnouncementFormatter.FormatClose(bank);

        if (!await TrySendAsync(text, cancellationToken))
        {
            return;
        }

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

        var text = MarketAnnouncementFormatter.FormatGameStartedClose(bank);

        if (!await TrySendAsync(text, cancellationToken))
        {
            return;
        }

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
            // continue
        }
        else if (result.Status == BetSettlementStatus.Cancelled && result.Bank > 0)
        {
            // continue
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

        string text;
        if (result.Status == BetSettlementStatus.Settled)
        {
            var topLabels = result.TopPlayers
                .Select(p => string.IsNullOrWhiteSpace(p.Name) ? p.SteamId : p.Name)
                .ToList();

            var userIds = result.Credits.Select(c => c.TelegramUserId).Distinct().ToList();
            var players = await db.Players
                .AsNoTracking()
                .Where(p => p.TelegramUserId != null && userIds.Contains(p.TelegramUserId.Value))
                .ToListAsync(cancellationToken);

            var nameByUser = players.ToDictionary(
                p => p.TelegramUserId!.Value,
                p => string.IsNullOrWhiteSpace(p.TelegramUsername)
                    ? $"id {p.TelegramUserId}"
                    : p.TelegramUsername!);

            var winnerLines = result.Credits
                .Select(c =>
                {
                    var label = nameByUser.TryGetValue(c.TelegramUserId, out var name)
                        ? name
                        : $"id {c.TelegramUserId}";
                    return $"{label}: +{c.Credited} таблов";
                })
                .ToList();

            text = MarketAnnouncementFormatter.FormatSettled(topLabels, winnerLines, result.Bank);
        }
        else
        {
            text = MarketAnnouncementFormatter.FormatCancelled(result.CancelReason, result.Bank);
        }

        if (!await TrySendAsync(text, cancellationToken))
        {
            return;
        }

        market.SettlementAnnounced = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TrySendAsync(string text, CancellationToken cancellationToken)
    {
        if (_botSettings.GroupChatId == 0)
        {
            return false;
        }

        try
        {
            await _bot.SendMessage(
                _botSettings.GroupChatId,
                text,
                cancellationToken: cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send market announcement to group chat");
            return false;
        }
    }
}
