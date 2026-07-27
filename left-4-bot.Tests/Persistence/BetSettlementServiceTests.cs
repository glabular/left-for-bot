using left_4_bot.Persistence;
using left_4_bot.ServerLogs;
using left_4_bot.Tests.TestSupport;

namespace left_4_bot.Tests.Persistence;

public sealed class BetSettlementServiceTests
{
    [Fact]
    public async Task NotReady_WhenAnchorIsCurrentDay()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);

        db.SeedPlayer(SettlementTestData.User1, balance: 500);
        db.SeedMarket(SettlementTestData.PastAnchor, DailyMarketStatus.Open);

        var current = GamingDay.GetCurrentAnchor(SettlementTestData.GamingDayOptions);
        var result = await service.SettleAsync(current, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.NotReady, result.Status);
        Assert.Equal(500, await db.GetBalanceAsync(SettlementTestData.User1));
    }

    [Fact]
    public async Task NothingToSettle_NoMarketNoBets()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);

        var result = await service.SettleAsync(SettlementTestData.PastAnchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.NothingToSettle, result.Status);
    }

    [Fact]
    public async Task AlreadyFinalized_DoesNotChangeBalances()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        var first = await service.SettleAsync(anchor, CancellationToken.None);
        Assert.Equal(BetSettlementStatus.Settled, first.Status);

        var balancesAfterFirst = await SnapshotBalancesAsync(db);

        var second = await service.SettleAsync(anchor, CancellationToken.None);
        Assert.Equal(BetSettlementStatus.AlreadyFinalized, second.Status);
        Assert.Equal(balancesAfterFirst, await SnapshotBalancesAsync(db));
    }

    [Fact]
    public async Task TooFewBettors_RefundsAll()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        db.SeedPlayer(SettlementTestData.User1, balance: 900);
        db.SeedPlayer(SettlementTestData.User2, balance: 950);
        db.SeedMarket(anchor, DailyMarketStatus.Open);
        db.SeedBet(anchor, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);
        db.SeedBet(anchor, SettlementTestData.User2, SettlementTestData.SteamIdB, 50);

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Cancelled, result.Status);
        Assert.Equal(BetSettlementCancelReason.TooFewBettors, result.CancelReason);
        Assert.Equal(DailyMarketStatus.Cancelled, await db.GetMarketStatusAsync(anchor));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User2));
    }

    [Fact]
    public async Task NoProtectWinner_RefundsAll()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, ProtectTeammateSectionData.NoEventsWithPriorityPresent(true));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Cancelled, result.Status);
        Assert.Equal(BetSettlementCancelReason.NoProtectWinner, result.CancelReason);
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task NoWinningBets_RefundsAll()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdC, "C", 9)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Cancelled, result.Status);
        Assert.Equal(BetSettlementCancelReason.NoWinningBets, result.CancelReason);
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(1000, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task SingleWinner_GetsFullBank()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        // User1 holds 100 on A; User2 50 on B; User3 50 on B. Bank = 200.
        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 7)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, result.Status);
        Assert.Equal(DailyMarketStatus.Settled, await db.GetMarketStatusAsync(anchor));
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1)); // 900 + 200
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User3));
        Assert.Equal(200, result.Credits.Sum(c => c.Credited));
    }

    [Fact]
    public async Task ProportionalPayout_MultipleWinners()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        // User1: 10 on A, User2: 30 on A, User3: 50 on B. Bank=90. Winners share 90 as 10:30 → 22.5→23? Wait.
        // Exact: winning stake 40, bank 90 → 10 gets 22, 30 gets 67? 90*10/40=22 rem 20; 90*30/40=67 rem 20; leftover=1 → +1 to lowest userId among equal rem → user1.
        // Actually rem both 20, ThenBy userId → user1 gets +1 → 23 and 67. Sum=90.
        db.SeedPlayer(SettlementTestData.User1, balance: 990);
        db.SeedPlayer(SettlementTestData.User2, balance: 970);
        db.SeedPlayer(SettlementTestData.User3, balance: 950);
        db.SeedMarket(anchor, DailyMarketStatus.Open);
        db.SeedBet(anchor, SettlementTestData.User1, SettlementTestData.SteamIdA, 10);
        db.SeedBet(anchor, SettlementTestData.User2, SettlementTestData.SteamIdA, 30);
        db.SeedBet(anchor, SettlementTestData.User3, SettlementTestData.SteamIdB, 50);

        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, result.Status);
        Assert.Equal(90, result.Credits.Sum(c => c.Credited));
        Assert.Equal(1013, await db.GetBalanceAsync(SettlementTestData.User1)); // 990 + 23
        Assert.Equal(1037, await db.GetBalanceAsync(SettlementTestData.User2)); // 970 + 67
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task TieAtTop_AllMatchingBetsWin()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        db.SeedPlayer(SettlementTestData.User1, balance: 900);
        db.SeedPlayer(SettlementTestData.User2, balance: 900);
        db.SeedPlayer(SettlementTestData.User3, balance: 900);
        db.SeedMarket(anchor, DailyMarketStatus.Open);
        db.SeedBet(anchor, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);
        db.SeedBet(anchor, SettlementTestData.User2, SettlementTestData.SteamIdB, 100);
        db.SeedBet(anchor, SettlementTestData.User3, SettlementTestData.SteamIdC, 100);

        loader.SetStats(
            anchor,
            SettlementTestData.HasData(
                (SettlementTestData.SteamIdA, "A", 5),
                (SettlementTestData.SteamIdB, "B", 5),
                (SettlementTestData.SteamIdC, "C", 1)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, result.Status);
        Assert.Equal(2, result.TopPlayers.Count);
        Assert.Equal(300, result.Credits.Sum(c => c.Credited));
        Assert.Equal(1050, await db.GetBalanceAsync(SettlementTestData.User1)); // half of 300
        Assert.Equal(1050, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task Steam0InStats_MatchesSteam1Bet()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(
            anchor,
            SettlementTestData.HasData((SettlementTestData.SteamIdAUniverse0, "A0", 8)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, result.Status);
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1));
    }

    [Fact]
    public async Task MergedUniverse_BeforeRanking()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        // STEAM_0:0:1 count=3 + STEAM_1:0:1 count=2 → merged 5 > SteamIdB's 4
        loader.SetStats(
            anchor,
            SettlementTestData.HasData(
                (SettlementTestData.SteamIdAUniverse0, "A0", 3),
                (SettlementTestData.SteamIdA, "A1", 2),
                (SettlementTestData.SteamIdB, "B", 4)));

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, result.Status);
        Assert.Single(result.TopPlayers);
        Assert.Equal(SettlementTestData.SteamIdA, result.TopPlayers[0].SteamId);
        Assert.Equal(5, result.TopPlayers[0].Count);
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1));
    }

    [Fact]
    public async Task DownloadFailed_LeavesMarketClosed()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, ProtectTeammateSectionData.DownloadFailed());

        var result = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.StatsUnavailable, result.Status);
        Assert.Equal(DailyMarketStatus.Closed, await db.GetMarketStatusAsync(anchor));
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User1));
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User2));
        Assert.Equal(950, await db.GetBalanceAsync(SettlementTestData.User3));
    }

    [Fact]
    public async Task RetryAfterDownloadFailed_ThenSettles()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, ProtectTeammateSectionData.DownloadFailed());

        var first = await service.SettleAsync(anchor, CancellationToken.None);
        Assert.Equal(BetSettlementStatus.StatsUnavailable, first.Status);
        Assert.Equal(900, await db.GetBalanceAsync(SettlementTestData.User1));

        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));
        var second = await service.SettleAsync(anchor, CancellationToken.None);

        Assert.Equal(BetSettlementStatus.Settled, second.Status);
        Assert.Equal(1100, await db.GetBalanceAsync(SettlementTestData.User1));
    }

    [Fact]
    public async Task DoubleSettle_AfterSuccess_IsIdempotent()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var service = SettlementTestData.CreateService(db, loader);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettorsOpenMarket(db, anchor, winnerTarget: SettlementTestData.SteamIdA);
        loader.SetStats(anchor, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        var first = await service.SettleAsync(anchor, CancellationToken.None);
        Assert.Equal(BetSettlementStatus.Settled, first.Status);

        var balances = await SnapshotBalancesAsync(db);

        var second = await service.SettleAsync(anchor, CancellationToken.None);
        Assert.Equal(BetSettlementStatus.AlreadyFinalized, second.Status);
        Assert.Equal(balances, await SnapshotBalancesAsync(db));
    }

    /// <summary>
    /// User1 bets 100 on <paramref name="winnerTarget"/>; User2/User3 bet 50 on SteamIdB.
    /// Starting balances: 900 / 950 / 950 (as if stakes already held).
    /// </summary>
    private static void SeedThreeBettorsOpenMarket(
        SqliteTestDb db,
        DateOnly anchor,
        string winnerTarget)
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
