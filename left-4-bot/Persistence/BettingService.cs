using left_4_bot.ServerLogs;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

public sealed class BettingService
{
    private readonly PlayersService _playersService;
    private readonly InMemoryDailyBetStore _betStore;
    private readonly GamingDayOptions _gamingDayOptions;
    private readonly SemaphoreSlim _placeGate = new(1, 1);

    public BettingService(
        PlayersService playersService,
        InMemoryDailyBetStore betStore,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _playersService = playersService;
        _betStore = betStore;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    public IReadOnlyList<Player> GetPriorityTargets() =>
        _playersService.GetPriorityPlayers();

    public DailyBet? TryGetCurrentBet(long telegramUserId)
    {
        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);
        return _betStore.TryGet(today, telegramUserId);
    }

    public async Task<BetActionResult> PlaceOrReplaceAsync(
        long telegramUserId,
        int oneBasedIndex,
        int amount,
        CancellationToken cancellationToken)
    {
        var targets = GetPriorityTargets();

        var player = _playersService.FindByTelegramUserId(telegramUserId);

        if (player is null)
        {
            return Fail(BetActionStatus.NeedLink, targets, balance: 0);
        }

        if (player.StarterGrantedAt is null)
        {
            return Fail(BetActionStatus.NeedStarter, targets, player.Balance);
        }

        if (!BettingWindow.IsOpen(_gamingDayOptions))
        {
            return Fail(BetActionStatus.WindowClosed, targets, player.Balance);
        }

        if (targets.Count == 0)
        {
            return Fail(BetActionStatus.NoPriorityTargets, targets, player.Balance);
        }

        if (oneBasedIndex < 1 || oneBasedIndex > targets.Count)
        {
            return Fail(BetActionStatus.InvalidTargetIndex, targets, player.Balance);
        }

        if (amount < ToteEconomy.MinBetTabs)
        {
            return Fail(BetActionStatus.InvalidAmount, targets, player.Balance);
        }

        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);
        var target = targets[oneBasedIndex - 1];

        await _placeGate.WaitAsync(cancellationToken);

        try
        {
            var previous = _betStore.TryGet(today, telegramUserId);
            var held = previous?.Amount ?? 0;
            var delta = held - amount;

            var adjust = await _playersService.TryAdjustBalanceAsync(
                telegramUserId,
                delta,
                cancellationToken);

            if (adjust.Status == BalanceAdjustStatus.NeedLink)
            {
                return Fail(BetActionStatus.NeedLink, targets, balance: 0);
            }

            if (adjust.Status == BalanceAdjustStatus.InsufficientBalance)
            {
                return new BetActionResult(
                    BetActionStatus.InsufficientBalance,
                    previous,
                    adjust.Balance,
                    targets);
            }

            var placed = new DailyBet(today, telegramUserId, target.SteamId, amount);
            _betStore.Set(placed);

            var status = previous is null ? BetActionStatus.Placed : BetActionStatus.Replaced;

            return new BetActionResult(status, placed, adjust.Balance, targets);
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

        var player = _playersService.FindByTelegramUserId(telegramUserId);

        if (player is null)
        {
            return Fail(BetActionStatus.NeedLink, targets, balance: 0);
        }

        if (!BettingWindow.IsOpen(_gamingDayOptions))
        {
            return new BetActionResult(
                BetActionStatus.WindowClosed,
                _betStore.TryGet(today, telegramUserId),
                player.Balance,
                targets);
        }

        await _placeGate.WaitAsync(cancellationToken);

        try
        {
            if (!_betStore.TryRemove(today, telegramUserId, out var removed) || removed is null)
            {
                return new BetActionResult(
                    BetActionStatus.NothingToCancel,
                    null,
                    player.Balance,
                    targets);
            }

            var adjust = await _playersService.TryAdjustBalanceAsync(
                telegramUserId,
                removed.Amount,
                cancellationToken);

            if (adjust.Status != BalanceAdjustStatus.Adjusted)
            {
                _betStore.Set(removed);

                return Fail(BetActionStatus.NeedLink, targets, adjust.Balance, removed);
            }

            return new BetActionResult(
                BetActionStatus.Cancelled,
                removed,
                adjust.Balance,
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
