using left_4_bot.ServerLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

public sealed class BettingService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly PlayersService _playersService;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly SemaphoreSlim _placeGate = new(1, 1);

    public BettingService(
        IDbContextFactory<BotDbContext> dbFactory,
        PlayersService playersService,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _dbFactory = dbFactory;
        _playersService = playersService;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public IReadOnlyList<Player> GetPriorityTargets() =>
        _playersService.GetPriorityPlayers();

    public async Task<DailyBet?> TryGetCurrentBetAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.DailyBets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.GamingDayAnchor == today && b.TelegramUserId == telegramUserId,
                cancellationToken);
    }

    public async Task<DailyMarket?> GetMarketAsync(
        DateOnly gamingDayAnchor,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.DailyMarkets
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.GamingDayAnchor == gamingDayAnchor, cancellationToken);
    }

    public async Task<BetActionResult> PlaceOrReplaceAsync(
        long telegramUserId,
        int oneBasedIndex,
        int amount,
        CancellationToken cancellationToken)
    {
        var targets = GetPriorityTargets();

        var playerSnapshot = _playersService.FindByTelegramUserId(telegramUserId);

        if (playerSnapshot is null)
        {
            return Fail(BetActionStatus.NeedLink, targets, balance: 0);
        }

        if (playerSnapshot.StarterGrantedAt is null)
        {
            return Fail(BetActionStatus.NeedStarter, targets, playerSnapshot.Balance);
        }

        if (!BettingWindow.IsOpen(_gamingDayOptions))
        {
            return Fail(BetActionStatus.WindowClosed, targets, playerSnapshot.Balance);
        }

        if (targets.Count == 0)
        {
            return Fail(BetActionStatus.NoPriorityTargets, targets, playerSnapshot.Balance);
        }

        if (oneBasedIndex < 1 || oneBasedIndex > targets.Count)
        {
            return Fail(BetActionStatus.InvalidTargetIndex, targets, playerSnapshot.Balance);
        }

        if (amount < ToteEconomy.MinBetTabs)
        {
            return Fail(BetActionStatus.InvalidAmount, targets, playerSnapshot.Balance);
        }

        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);
        var target = targets[oneBasedIndex - 1];

        await _placeGate.WaitAsync(cancellationToken);

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            var player = await db.Players
                .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

            if (player is null)
            {
                return Fail(BetActionStatus.NeedLink, targets, balance: 0);
            }

            if (player.StarterGrantedAt is null)
            {
                return Fail(BetActionStatus.NeedStarter, targets, player.Balance);
            }

            var market = await db.DailyMarkets
                .FirstOrDefaultAsync(m => m.GamingDayAnchor == today, cancellationToken);

            if (market is not null && market.Status != DailyMarketStatus.Open)
            {
                return Fail(BetActionStatus.WindowClosed, targets, player.Balance);
            }

            if (market is not null && market.CloseAnnounced)
            {
                return Fail(BetActionStatus.WindowClosed, targets, player.Balance);
            }

            var previous = await db.DailyBets
                .FirstOrDefaultAsync(
                    b => b.GamingDayAnchor == today && b.TelegramUserId == telegramUserId,
                    cancellationToken);

            var isReplace = previous is not null;
            var held = previous?.Amount ?? 0;
            var delta = held - amount;
            var nextBalance = player.Balance + delta;

            if (nextBalance < 0)
            {
                return new BetActionResult(
                    BetActionStatus.InsufficientBalance,
                    previous,
                    player.Balance,
                    targets);
            }

            if (market is null)
            {
                db.DailyMarkets.Add(new DailyMarket
                {
                    GamingDayAnchor = today,
                    Status = DailyMarketStatus.Open,
                });
            }

            player.Balance = nextBalance;

            DailyBet placed;

            if (previous is null)
            {
                placed = new DailyBet
                {
                    GamingDayAnchor = today,
                    TelegramUserId = telegramUserId,
                    TargetSteamId = target.SteamId,
                    Amount = amount,
                };
                db.DailyBets.Add(placed);
            }
            else
            {
                previous.TargetSteamId = target.SteamId;
                previous.Amount = amount;
                placed = previous;
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new BetActionResult(
                isReplace ? BetActionStatus.Replaced : BetActionStatus.Placed,
                placed,
                player.Balance,
                targets);
        }
        finally
        {
            _placeGate.Release();
        }
    }

    public async Task<BetActionResult> CancelAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        var targets = GetPriorityTargets();
        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);

        var playerSnapshot = _playersService.FindByTelegramUserId(telegramUserId);

        if (playerSnapshot is null)
        {
            return Fail(BetActionStatus.NeedLink, targets, balance: 0);
        }

        if (!BettingWindow.IsOpen(_gamingDayOptions))
        {
            var existing = await TryGetCurrentBetAsync(telegramUserId, cancellationToken);
            return new BetActionResult(
                BetActionStatus.WindowClosed,
                existing,
                playerSnapshot.Balance,
                targets);
        }

        await _placeGate.WaitAsync(cancellationToken);

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            var player = await db.Players
                .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

            if (player is null)
            {
                return Fail(BetActionStatus.NeedLink, targets, balance: 0);
            }

            var market = await db.DailyMarkets
                .FirstOrDefaultAsync(m => m.GamingDayAnchor == today, cancellationToken);

            if (market is not null && market.Status != DailyMarketStatus.Open)
            {
                var lockedBet = await db.DailyBets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        b => b.GamingDayAnchor == today && b.TelegramUserId == telegramUserId,
                        cancellationToken);

                return new BetActionResult(
                    BetActionStatus.WindowClosed,
                    lockedBet,
                    player.Balance,
                    targets);
            }

            if (market is not null && market.CloseAnnounced)
            {
                var lockedBet = await db.DailyBets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        b => b.GamingDayAnchor == today && b.TelegramUserId == telegramUserId,
                        cancellationToken);

                return new BetActionResult(
                    BetActionStatus.WindowClosed,
                    lockedBet,
                    player.Balance,
                    targets);
            }

            var bet = await db.DailyBets
                .FirstOrDefaultAsync(
                    b => b.GamingDayAnchor == today && b.TelegramUserId == telegramUserId,
                    cancellationToken);

            if (bet is null)
            {
                return new BetActionResult(
                    BetActionStatus.NothingToCancel,
                    null,
                    player.Balance,
                    targets);
            }

            var removed = new DailyBet
            {
                Id = bet.Id,
                GamingDayAnchor = bet.GamingDayAnchor,
                TelegramUserId = bet.TelegramUserId,
                TargetSteamId = bet.TargetSteamId,
                Amount = bet.Amount,
            };

            player.Balance += bet.Amount;
            db.DailyBets.Remove(bet);

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new BetActionResult(
                BetActionStatus.Cancelled,
                removed,
                player.Balance,
                targets);
        }
        finally
        {
            _placeGate.Release();
        }
    }

    private static BetActionResult Fail(
        BetActionStatus status,
        IReadOnlyList<Player> targets,
        int balance,
        DailyBet? bet = null) =>
        new(status, bet, balance, targets);
}
