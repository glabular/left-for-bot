using left_4_bot.ServerLogs;
using left_4_bot.Whois;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

/// <summary>
/// Settles one gaming-day betting market from protect-teammate stats.
/// </summary>
public sealed class BetSettlementService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly IProtectTeammateDayStatsLoader _protectStatsLoader;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly SemaphoreSlim _settleGate = new(1, 1);

    public BetSettlementService(
        IDbContextFactory<BotDbContext> dbFactory,
        IProtectTeammateDayStatsLoader protectStatsLoader,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _dbFactory = dbFactory;
        _protectStatsLoader = protectStatsLoader;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public async Task<BetSettlementResult> SettleAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        var currentAnchor = GamingDay.GetCurrentAnchor(_gamingDayOptions);
        if (gamingDayAnchor >= currentAnchor)
        {
            return BetSettlementResult.NotReady(gamingDayAnchor);
        }

        await _settleGate.WaitAsync(cancellationToken);

        try
        {
            var closeResult = await EnsureMarketClosedAsync(gamingDayAnchor, cancellationToken);

            if (closeResult is not null)
            {
                return closeResult;
            }

            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var bets = await db.DailyBets
                .AsNoTracking()
                .Where(b => b.GamingDayAnchor == gamingDayAnchor)
                .ToListAsync(cancellationToken);

            var bank = bets.Sum(b => b.Amount);

            if (bets.Count == 0)
            {
                // Closed empty market (or market created without bets) — finalize as cancelled.
                return await FinalizeRefundAsync(
                    gamingDayAnchor,
                    bank: 0,
                    [],
                    BetSettlementCancelReason.TooFewBettors,
                    cancellationToken);
            }

            if (bets.Select(b => b.TelegramUserId).Distinct().Count() < ToteEconomy.MinBettorsPerMarket)
            {
                return await FinalizeRefundAsync(
                    gamingDayAnchor,
                    bank,
                    [],
                    BetSettlementCancelReason.TooFewBettors,
                    cancellationToken);
            }

            var stats = await _protectStatsLoader.LoadAsync(gamingDayAnchor, cancellationToken);

            if (stats.State == ProtectTeammateSectionState.DownloadFailed)
            {
                return BetSettlementResult.StatsUnavailable(gamingDayAnchor, bank);
            }

            var merged = MergeByCanonicalSteamId(stats);
            var topPlayers = ResolveTopPlayers(merged);

            if (topPlayers.Count == 0)
            {
                return await FinalizeRefundAsync(
                    gamingDayAnchor,
                    bank,
                    [],
                    BetSettlementCancelReason.NoProtectWinner,
                    cancellationToken);
            }

            var topCanonical = topPlayers
                .Select(p => p.SteamId)
                .ToHashSet(StringComparer.Ordinal);

            var winningBets = bets
                .Where(b =>
                    SteamIdConverter.TryToCanonicalClassic(b.TargetSteamId, out var canonical)
                    && topCanonical.Contains(canonical))
                .ToList();

            if (winningBets.Count == 0)
            {
                return await FinalizeRefundAsync(
                    gamingDayAnchor,
                    bank,
                    topPlayers,
                    BetSettlementCancelReason.NoWinningBets,
                    cancellationToken);
            }

            var payouts = PariMutuelPayoutCalculator.Calculate(
                bank,
                winningBets.Select(b => (b.TelegramUserId, b.Amount)).ToList());

            return await FinalizePayoutsAsync(
                gamingDayAnchor,
                bank,
                winningBets,
                payouts,
                topPlayers,
                cancellationToken);
        }
        finally
        {
            _settleGate.Release();
        }
    }

    private async Task<BetSettlementResult?> EnsureMarketClosedAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        var hasBets = await db.DailyBets
            .AnyAsync(b => b.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null)
        {
            if (!hasBets)
            {
                return BetSettlementResult.NothingToSettle(gamingDayAnchor);
            }

            db.DailyMarkets.Add(new DailyMarket
            {
                GamingDayAnchor = gamingDayAnchor,
                Status = DailyMarketStatus.Closed,
            });
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return null;
        }

        if (market.Status is DailyMarketStatus.Settled or DailyMarketStatus.Cancelled)
        {
            return BetSettlementResult.AlreadyFinalized(gamingDayAnchor, market.Status);
        }

        if (market.Status == DailyMarketStatus.Open)
        {
            market.Status = DailyMarketStatus.Closed;
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return null;
    }

    private async Task<BetSettlementResult> FinalizeRefundAsync(
        DateOnly gamingDayAnchor,
        int bank,
        IReadOnlyList<BetSettlementTopPlayer> topPlayers,
        BetSettlementCancelReason reason,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null)
        {
            return BetSettlementResult.NothingToSettle(gamingDayAnchor);
        }

        if (market.Status is DailyMarketStatus.Settled or DailyMarketStatus.Cancelled)
        {
            return BetSettlementResult.AlreadyFinalized(gamingDayAnchor, market.Status);
        }

        var liveBets = await db.DailyBets
            .Where(b => b.GamingDayAnchor == gamingDayAnchor)
            .ToListAsync(cancellationToken);

        var credits = new List<BetPayoutResult>(liveBets.Count);

        foreach (var bet in liveBets)
        {
            var player = await db.Players
                .FirstOrDefaultAsync(p => p.TelegramUserId == bet.TelegramUserId, cancellationToken);

            if (player is null)
            {
                return BetSettlementResult.PersistenceConflict(gamingDayAnchor, bank);
            }

            player.Balance += bet.Amount;
            credits.Add(new BetPayoutResult(bet.TelegramUserId, bet.TargetSteamId, bet.Amount, bet.Amount));
        }

        market.Status = DailyMarketStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new BetSettlementResult(
            gamingDayAnchor,
            BetSettlementStatus.Cancelled,
            reason,
            DailyMarketStatus.Cancelled,
            bank,
            topPlayers,
            credits);
    }

    private async Task<BetSettlementResult> FinalizePayoutsAsync(
        DateOnly gamingDayAnchor,
        int bank,
        IReadOnlyList<DailyBet> winningBets,
        IReadOnlyList<PariMutuelPayout> payouts,
        IReadOnlyList<BetSettlementTopPlayer> topPlayers,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var market = await db.DailyMarkets
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);

        if (market is null)
        {
            return BetSettlementResult.NothingToSettle(gamingDayAnchor);
        }

        if (market.Status is DailyMarketStatus.Settled or DailyMarketStatus.Cancelled)
        {
            return BetSettlementResult.AlreadyFinalized(gamingDayAnchor, market.Status);
        }

        var payoutByUser = payouts.ToDictionary(p => p.TelegramUserId);
        var credits = new List<BetPayoutResult>(winningBets.Count);

        foreach (var bet in winningBets)
        {
            if (!payoutByUser.TryGetValue(bet.TelegramUserId, out var payout))
            {
                return BetSettlementResult.PersistenceConflict(gamingDayAnchor, bank);
            }

            var player = await db.Players
                .FirstOrDefaultAsync(p => p.TelegramUserId == bet.TelegramUserId, cancellationToken);

            if (player is null)
            {
                return BetSettlementResult.PersistenceConflict(gamingDayAnchor, bank);
            }

            player.Balance += payout.Payout;
            credits.Add(new BetPayoutResult(
                bet.TelegramUserId,
                bet.TargetSteamId,
                bet.Amount,
                payout.Payout));
        }

        market.Status = DailyMarketStatus.Settled;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new BetSettlementResult(
            gamingDayAnchor,
            BetSettlementStatus.Settled,
            BetSettlementCancelReason.None,
            DailyMarketStatus.Settled,
            bank,
            topPlayers,
            credits);
    }

    private static Dictionary<string, ProtectTeammatePlayerStats> MergeByCanonicalSteamId(
        ProtectTeammateSectionData stats)
    {
        var merged = new Dictionary<string, ProtectTeammatePlayerStats>(StringComparer.Ordinal);

        if (stats.State != ProtectTeammateSectionState.HasData)
        {
            return merged;
        }

        foreach (var player in stats.Players.Values)
        {
            if (player.Count <= 0)
            {
                continue;
            }

            if (!SteamIdConverter.TryToCanonicalClassic(player.SteamId, out var canonical))
            {
                continue;
            }

            if (!merged.TryGetValue(canonical, out var existing))
            {
                merged[canonical] = new ProtectTeammatePlayerStats
                {
                    SteamId = canonical,
                    Name = player.Name,
                    Count = player.Count,
                };
            }
            else
            {
                existing.Count += player.Count;
                if (!string.IsNullOrWhiteSpace(player.Name))
                {
                    existing.Name = player.Name;
                }
            }
        }

        return merged;
    }

    private static IReadOnlyList<BetSettlementTopPlayer> ResolveTopPlayers(
        IReadOnlyDictionary<string, ProtectTeammatePlayerStats> merged)
    {
        if (merged.Count == 0)
        {
            return [];
        }

        var max = merged.Values.Max(p => p.Count);

        if (max <= 0)
        {
            return [];
        }

        return merged.Values
            .Where(p => p.Count == max)
            .OrderBy(p => p.SteamId, StringComparer.Ordinal)
            .Select(p => new BetSettlementTopPlayer(p.SteamId, p.Name, p.Count))
            .ToList();
    }
}
