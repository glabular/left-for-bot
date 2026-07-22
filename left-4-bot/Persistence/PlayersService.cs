using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Persistence;

public sealed class PlayersService
{
    private readonly IDbContextFactory<BotDbContext> _dbFactory;

    public PlayersService(IDbContextFactory<BotDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
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
        string? displayName,
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
                bySteam.TelegramDisplayName = displayName;
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
            TelegramDisplayName = displayName,
            IsPriority = true,
            LinkedAt = DateTimeOffset.UtcNow,
        };

        db.Players.Add(player);
        await db.SaveChangesAsync(cancellationToken);

        return new LinkPlayerResult(LinkPlayerStatus.Linked, player);
    }
}
