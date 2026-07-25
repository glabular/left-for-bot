using left_4_bot.ServerLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace left_4_bot.Persistence;

public sealed class PlayersService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;
    private readonly GamingDayOptions _gamingDayOptions;

    public PlayersService(
        IDbContextFactory<BotDbContext> dbFactory,
        IOptions<GamingDayOptions> gamingDayOptions)
    {
        _dbFactory = dbFactory;
        _gamingDayOptions = gamingDayOptions.Value;
    }

    // TODO: Make async?
    public IReadOnlySet<string> GetPrioritySteamIds()
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Players
            .AsNoTracking()
            .Where(p => p.IsPriority)
            .Select(p => p.SteamId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Priority players ordered by SteamId for stable /bet list numbers.
    /// </summary>
    public IReadOnlyList<Player> GetPriorityPlayers()
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Players
            .AsNoTracking()
            .Where(p => p.IsPriority)
            .OrderBy(p => p.SteamId)
            .ToList();
    }

    /// <summary>
    /// Applies <paramref name="delta"/> to balance (negative holds Tabs, positive refunds).
    /// Fails when the result would be negative.
    /// </summary>
    public async Task<BalanceAdjustResult> TryAdjustBalanceAsync(
        long telegramUserId,
        int delta,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var player = await db.Players
            .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

        if (player is null)
        {
            return new BalanceAdjustResult(BalanceAdjustStatus.NeedLink, Balance: 0);
        }

        var next = player.Balance + delta;
        if (next < 0)
        {
            return new BalanceAdjustResult(BalanceAdjustStatus.InsufficientBalance, player.Balance);
        }

        player.Balance = next;
        await db.SaveChangesAsync(cancellationToken);

        return new BalanceAdjustResult(BalanceAdjustStatus.Adjusted, player.Balance);
    }

    public Player? FindByTelegramUserId(long telegramUserId)
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Players
            .AsNoTracking()
            .FirstOrDefault(p => p.TelegramUserId == telegramUserId);
    }

    public Player? FindBySteamId(string steamId)
    {
        using var db = _dbFactory.CreateDbContext();

        return db.Players
            .AsNoTracking()
            .FirstOrDefault(p => p.SteamId == steamId);
    }

    public async Task<LinkPlayerResult> LinkAsync(
        long telegramUserId,
        string? telegramUsername,
        string classicSteamId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var byTelegram = await db.Players
            .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

        if (byTelegram is not null)
        {
            if (string.Equals(byTelegram.SteamId, classicSteamId, StringComparison.Ordinal))
            {
                return new LinkPlayerResult(LinkPlayerStatus.AlreadyLinked, byTelegram);
            }

            return new LinkPlayerResult(LinkPlayerStatus.TelegramBoundToOtherSteam, byTelegram);
        }

        var bySteam = await db.Players
            .FirstOrDefaultAsync(p => p.SteamId == classicSteamId, cancellationToken);

        if (bySteam is not null)
        {
            if (bySteam.TelegramUserId is null)
            {
                bySteam.TelegramUserId = telegramUserId;
                bySteam.TelegramUsername = telegramUsername;
                bySteam.LinkedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return new LinkPlayerResult(LinkPlayerStatus.Linked, bySteam);
            }

            if (bySteam.TelegramUserId == telegramUserId)
            {
                return new LinkPlayerResult(LinkPlayerStatus.AlreadyLinked, bySteam);
            }

            return new LinkPlayerResult(LinkPlayerStatus.SteamBoundToOtherTelegram, bySteam);
        }

        var player = new Player
        {
            SteamId = classicSteamId,
            TelegramUserId = telegramUserId,
            TelegramUsername = telegramUsername,
            IsPriority = true,
            LinkedAt = DateTimeOffset.UtcNow,
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);

        return new LinkPlayerResult(LinkPlayerStatus.Linked, player);
    }

    /// <summary>
    /// Shows balance or grants the one-time starter tabs for a linked Telegram user.
    /// </summary>
    public async Task<BalanceClaimResult> GetOrClaimStarterBalanceAsync(
        long telegramUserId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var player = await db.Players
            .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

        if (player is null)
        {
            return new BalanceClaimResult(BalanceClaimStatus.NeedLink, Balance: 0, SteamId: null);
        }

        if (player.StarterGrantedAt is not null)
        {
            return new BalanceClaimResult(
                BalanceClaimStatus.AlreadyJoined,
                player.Balance,
                player.SteamId);
        }

        player.Balance = ToteEconomy.StarterTabs;
        player.StarterGrantedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new BalanceClaimResult(
            BalanceClaimStatus.Joined,
            player.Balance,
            player.SteamId);
    }

    /// <summary>
    /// Grants the given Tabs amount for a bot command use, if eligible and under the daily cap.
    /// </summary>
    /// <param name="tabsAmount">Tabs to add; must be positive.</param>
    public async Task<CommandRewardResult> TryGrantCommandRewardAsync(
        long telegramUserId,
        int tabsAmount,
        CancellationToken cancellationToken)
    {
        if (tabsAmount <= 0)
        {
            return new CommandRewardResult(
                CommandRewardStatus.NotEligible,
                Balance: 0,
                RewardsUsedToday: 0,
                TabsGrantedToday: 0);
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var player = await db.Players
            .FirstOrDefaultAsync(p => p.TelegramUserId == telegramUserId, cancellationToken);

        if (player is null || player.StarterGrantedAt is null)
        {
            return new CommandRewardResult(
                CommandRewardStatus.NotEligible,
                Balance: 0,
                RewardsUsedToday: 0,
                TabsGrantedToday: 0);
        }

        var today = GamingDay.GetCurrentAnchor(_gamingDayOptions);

        if (player.CommandRewardGamingDay != today)
        {
            player.CommandRewardGamingDay = today;
            player.CommandRewardCount = 0;
            player.CommandRewardTabsGrantedToday = 0;
        }

        if (player.CommandRewardCount >= ToteEconomy.CommandRewardsPerGamingDay)
        {
            return new CommandRewardResult(
                CommandRewardStatus.DailyLimitReached,
                player.Balance,
                player.CommandRewardCount,
                player.CommandRewardTabsGrantedToday);
        }

        player.CommandRewardCount += 1;
        player.Balance += tabsAmount;
        player.CommandRewardTabsGrantedToday += tabsAmount;

        await db.SaveChangesAsync(cancellationToken);

        if (player.CommandRewardCount >= ToteEconomy.CommandRewardsPerGamingDay)
        {
            return new CommandRewardResult(
                CommandRewardStatus.GrantedDailyComplete,
                player.Balance,
                player.CommandRewardCount,
                player.CommandRewardTabsGrantedToday);
        }

        return new CommandRewardResult(
            CommandRewardStatus.GrantedSilent,
            player.Balance,
            player.CommandRewardCount,
            player.CommandRewardTabsGrantedToday);
    }
}
