using left_4_bot.Persistence;
using left_4_bot.ServerLogs;
using left_4_bot.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace left_4_bot.Tests.Persistence;

public sealed class MarketAnnouncementRolloverTests
{
    [Fact]
    public async Task Tick_MarksOpenAnnouncedWithoutSending_WhenBettingWindowOpen()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var current = GamingDay.GetCurrentAnchor(SettlementTestData.GamingDayOptions);
        var hour = GamingDay.NowMoscow().Hour;

        await rollover.TickAsync(CancellationToken.None);
        await rollover.TickAsync(CancellationToken.None);

        Assert.Equal(DailyMarketStatus.Open, await db.GetMarketStatusAsync(current));
        Assert.Empty(notifier.Announcements);

        if (hour < ToteEconomy.BetWindowCloseHourMsk)
        {
            Assert.True(await IsFlagAsync(db, current, m => m.OpenAnnounced));
        }
        else
        {
            Assert.True(await IsFlagAsync(db, current, m => m.OpenAnnounced));
            Assert.True(await IsFlagAsync(db, current, m => m.CloseAnnounced));
        }
    }

    [Fact]
    public async Task Tick_AfterCloseHour_DoesNotAnnounceOpenAndCloseTogether()
    {
        if (GamingDay.NowMoscow().Hour < ToteEconomy.BetWindowCloseHourMsk)
        {
            return; // wall-clock gate; covered when run after BetWindowCloseHourMsk
        }

        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var current = GamingDay.GetCurrentAnchor(SettlementTestData.GamingDayOptions);

        await rollover.TickAsync(CancellationToken.None);

        Assert.Empty(notifier.Announcements);
        Assert.True(await IsFlagAsync(db, current, m => m.OpenAnnounced));
        Assert.True(await IsFlagAsync(db, current, m => m.CloseAnnounced));
    }

    [Fact]
    public async Task Close_AnnouncesOnlyWhenMinBettorsReached()
    {
        using var db = new SqliteTestDb();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var anchor = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, anchor, SettlementTestData.SteamIdA);

        await notifier.TryAnnounceCloseAsync(anchor, bank: 200, CancellationToken.None);

        Assert.Equal(
            [RecordedMarketAnnouncement.Close],
            notifier.Announcements);
        Assert.True(await IsFlagAsync(db, anchor, m => m.CloseAnnounced));
    }

    [Fact]
    public async Task Close_SkipsMessageWhenTooFewBettors_ButMarksCloseAnnounced()
    {
        using var db = new SqliteTestDb();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var anchor = SettlementTestData.PastAnchor;

        db.SeedPlayer(SettlementTestData.User1, balance: 900);
        db.SeedMarket(anchor, DailyMarketStatus.Open);
        db.SeedBet(anchor, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);

        await notifier.TryAnnounceCloseAsync(anchor, bank: 100, CancellationToken.None);

        Assert.Empty(notifier.Announcements);
        Assert.True(await IsFlagAsync(db, anchor, m => m.CloseAnnounced));
    }

    [Fact]
    public async Task Tick_AnnouncesSettledOnce()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var past = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, past, SettlementTestData.SteamIdA);
        loader.SetStats(past, SettlementTestData.HasData((SettlementTestData.SteamIdA, "A", 5)));

        await rollover.TickAsync(CancellationToken.None);
        Assert.Equal(1, notifier.Announcements.Count(a => a == RecordedMarketAnnouncement.Settled));

        await rollover.TickAsync(CancellationToken.None);
        Assert.Equal(1, notifier.Announcements.Count(a => a == RecordedMarketAnnouncement.Settled));
        Assert.True(await IsFlagAsync(db, past, m => m.SettlementAnnounced));
    }

    [Fact]
    public async Task Tick_AnnouncesCancelledWhenBankPositive()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var past = SettlementTestData.PastAnchor;

        db.SeedPlayer(SettlementTestData.User1, balance: 900);
        db.SeedPlayer(SettlementTestData.User2, balance: 950);
        db.SeedMarket(past, DailyMarketStatus.Open);
        db.SeedBet(past, SettlementTestData.User1, SettlementTestData.SteamIdA, 100);
        db.SeedBet(past, SettlementTestData.User2, SettlementTestData.SteamIdB, 50);

        await rollover.TickAsync(CancellationToken.None);

        Assert.Contains(RecordedMarketAnnouncement.Cancelled, notifier.Announcements);
        Assert.True(await IsFlagAsync(db, past, m => m.SettlementAnnounced));
    }

    [Fact]
    public async Task Tick_SkipsCancelledAnnounceWhenBankZero()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var past = SettlementTestData.PastAnchor;

        db.SeedMarket(past, DailyMarketStatus.Open);

        await rollover.TickAsync(CancellationToken.None);

        Assert.DoesNotContain(RecordedMarketAnnouncement.Cancelled, notifier.Announcements);
        Assert.Equal(DailyMarketStatus.Cancelled, await db.GetMarketStatusAsync(past));
    }

    [Fact]
    public async Task Tick_SkipsSettlementAnnounceWhenStatsUnavailable()
    {
        using var db = new SqliteTestDb();
        var loader = new FakeProtectTeammateStatsLoader();
        var notifier = new RecordingMarketChatNotifier(db.Factory);
        var rollover = CreateRollover(db, loader, notifier);
        var past = SettlementTestData.PastAnchor;

        SeedThreeBettors(db, past, SettlementTestData.SteamIdA);
        loader.SetStats(past, ProtectTeammateSectionData.DownloadFailed());

        await rollover.TickAsync(CancellationToken.None);

        Assert.DoesNotContain(RecordedMarketAnnouncement.Settled, notifier.Announcements);
        Assert.DoesNotContain(RecordedMarketAnnouncement.Cancelled, notifier.Announcements);
        Assert.Equal(DailyMarketStatus.Closed, await db.GetMarketStatusAsync(past));
        Assert.False(await IsFlagAsync(db, past, m => m.SettlementAnnounced));
    }

    private static DailyMarketRolloverService CreateRollover(
        SqliteTestDb db,
        FakeProtectTeammateStatsLoader loader,
        IMarketChatNotifier notifier)
    {
        var settlement = SettlementTestData.CreateService(db, loader);
        return new DailyMarketRolloverService(
            db.Factory,
            settlement,
            notifier,
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

    private static async Task<bool> IsFlagAsync(
        SqliteTestDb db,
        DateOnly anchor,
        Func<DailyMarket, bool> flag)
    {
        await using var ctx = await db.Factory.CreateDbContextAsync();
        var market = await ctx.DailyMarkets.SingleAsync(m => m.GamingDayAnchor == anchor);
        return flag(market);
    }
}
