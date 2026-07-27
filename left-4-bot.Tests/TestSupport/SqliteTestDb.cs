using left_4_bot.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace left_4_bot.Tests.TestSupport;

/// <summary>
/// Shared SQLite in-memory database for one test. Connection must stay open
/// for the lifetime of the fixture or the database disappears.
/// </summary>
public sealed class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BotDbContext> _options;

    public IDbContextFactory<BotDbContext> Factory { get; }

    public SqliteTestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(_connection)
            .Options;

        using (var db = new BotDbContext(_options))
        {
            db.Database.Migrate();
        }

        Factory = new FactoryImpl(_options);
    }

    public void SeedPlayer(long telegramUserId, int balance, string? steamId = null)
    {
        using var db = Factory.CreateDbContext();
        db.Players.Add(new Player
        {
            SteamId = steamId ?? $"STEAM_1:0:{telegramUserId}",
            TelegramUserId = telegramUserId,
            IsPriority = true,
            Balance = balance,
            LinkedAt = DateTimeOffset.UtcNow,
            StarterGrantedAt = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
    }

    public void SeedMarket(DateOnly anchor, DailyMarketStatus status)
    {
        using var db = Factory.CreateDbContext();
        db.DailyMarkets.Add(new DailyMarket
        {
            GamingDayAnchor = anchor,
            Status = status,
        });
        db.SaveChanges();
    }

    public void SeedBet(DateOnly anchor, long telegramUserId, string targetSteamId, int amount)
    {
        using var db = Factory.CreateDbContext();
        db.DailyBets.Add(new DailyBet
        {
            GamingDayAnchor = anchor,
            TelegramUserId = telegramUserId,
            TargetSteamId = targetSteamId,
            Amount = amount,
        });
        db.SaveChanges();
    }

    public async Task<int> GetBalanceAsync(long telegramUserId)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var player = await db.Players.SingleAsync(p => p.TelegramUserId == telegramUserId);
        return player.Balance;
    }

    public async Task<DailyMarketStatus?> GetMarketStatusAsync(DateOnly anchor)
    {
        await using var db = await Factory.CreateDbContextAsync();
        var market = await db.DailyMarkets.SingleOrDefaultAsync(m => m.GamingDayAnchor == anchor);
        return market?.Status;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class FactoryImpl : IDbContextFactory<BotDbContext>
    {
        private readonly DbContextOptions<BotDbContext> _options;

        public FactoryImpl(DbContextOptions<BotDbContext> options) => _options = options;

        public BotDbContext CreateDbContext() => new(_options);
    }
}
