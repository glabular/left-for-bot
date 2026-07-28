using left_4_bot.Persistence;
using left_4_bot.ServerLogs;
using left_4_bot.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace left_4_bot.Tests.Persistence;

public sealed class DailyMarketRolloverServiceTests
{
    [Fact]
    public async Task Tick_SettlesPastOpenMarket()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var rollover = CreateRollover(db, loader);
        var past = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, past, SettlementTestData.SteamIdA);
        loader.SetStats(past, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(past));
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task Tick_RetriesAfterStatsUnavailable()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var rollover = CreateRollover(db, loader);
        var past = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, past, SettlementTestData.SteamIdA);
        loader.SetStats(past, ProtectTeammateSectionData.DownloadFailed());

        await rollover.TickAsync(CancellationToken.None);
        Assert.Equal(DailyMarketStatus.Closed, await db.GetMarketStatusAsync(past));
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User1));

        loader.SetStats(past, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));
        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(past));
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1));
    }

    [Fact]
    public async Task Tick_SkipsAlreadyFinalized()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var rollover = CreateRollover(db, loader);
        var past = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, past, SettlementTestData.SteamIdA);
        loader.SetStats(past, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        await rollover.TickAsync(CancellationToken.None);
        var balances = await SnapshotBalancesAsync(db);

        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(balances, await SnapshotBalancesAsync(db));
        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(past));
    }

    [Fact]
    public async Task Tick_EnsuresCurrentMarketOpen()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var rollover = CreateRollover(db, loader);
        var current = GamingDay.GetCurrentAnchor(SettlementTestData.GamingDayOptions);

        Assert.Null(await db.GetMarketStatusAsync(current));

        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(DailyMarketStatus.Open, await db.GetMarketStatusAsync(current));
    }

    [Fact]
    public async Task Tick_CatchUpMultiplePastDays()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var rollover = CreateRollover(db, loader);

        var day1 = new DateOnly(2020, 1, 1);
        var day2 = new DateOnly(2020, 1, 2);

        // Balances already reflect stakes held for both days (100+100 / 50+50 / 50+50).
        db.SeedPlayer(SettlementTestData.User1, balance: 800);
        db.SeedPlayer(SettlementTestData.User2, balance: 900);
        db.SeedPlayer(SettlementTestData.User3, balance: 900);

        db.SeedMarket(day1, DailyMarketStatus.Open);
        db.SeedBet(day1, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);
        db.SeedBet(day1, SettlementTestData.User2, SettlementTestData.SteamIdB, 50);
        db.SeedBet(day1, SettlementTestData.User3, SettlementTestData.SteamIdB, 50);

        db.SeedMarket(day2, DailyMarketStatus.Open);
        db.SeedBet(day2, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);
        db.SeedBet(day2, SettlementTestData.User2, SettlementTestData.SteamIdB, 50);
        db.SeedBet(day2, SettlementTestData.User3, SettlementTestData.SteamIdB, 50);

        loader.SetStats(day1, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));
        loader.SetStats(day2, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 8)));

        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(day1));
        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(day2));
        // Two banks of 200 credited to User1 from starting 800
        Assert.Equal(1200, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    private static DailyMarketRolloverService CreateRollover(
        SqliteTestDb db,
        FakeProtectTeammateStatsLoader loader)
    {
        var settlement = SettlementTestData.CreateService(db, loader);
        return new DailyMarketRolloverService(
            db.Factory,
            settlement,
            new RecordingMarketChatNotifier(db.Factory),
            Options.Create(SettlementTestData.GamingDayOptions),
            NullLogger<DailyMarketRolloverService>.Instance);
    }

    private static void SeedThreeBettors(SqliteTestDb db, DateOnly anchor, string winnerTarget)
    {
        db.SeedPlayer(SettlementTestData.User1, balance: 900);
        db.SeedPlayer(SettlementTestData.User2, balance: 950);
        db.SeedPlayer(SettlementTestData.User3, balance: 950);
        db.SeedMarket(anchor, DailyMarketStatus.Open);
        db.SeedBet(anchor, SettlementTestData.User1, winnerTarget, 100);
        db.SeedBet(anchor, SettlementTestData.User2, SettlementTestData.SteamIdB, 50);
        db.SeedBet(anchor, SettlementTestData.User3, SettlementTestData.SteamIdB, 50);
    }

    private static async Task<Dictionary<long, int>> SnapshotBalancesAsync(SqliteTestDb db) =>
        new()
        {
            [SettlementTestData.User1] = await db.GetBalanceAsync(SettlementTestData.User1),
            [SettlementTestData.User2] = await db.GetBalanceAsync(SettlementTestData.User2),
            [SettlementTestData.User3] = await db.GetBalanceAsync(SettlementTestData.User3),
        };
}
