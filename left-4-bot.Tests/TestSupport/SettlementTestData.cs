using left_4_bot.Persistence;
using left_4_bot.ServerLogs;
using Microsoft.Extensions.Options;

namespace left_4_bot.Tests.TestSupport;

public static class SettlementTestData
{
    public static readonly DateOnly PastAnchor = new(2020, 1, 1);

    public const string SteamIdA = "STEAM_1:0:1";
    public const string SteamIdAUniverse0 = "STEAM_0:0:1";
    public const string SteamIdB = "STEAM_1:0:2";
    public const string SteamIdC = "STEAM_1:0:3";

    public const long User1 = 101;
    public const long User2 = 102;
    public const long User3 = 103;

    public static GamingDayOptions GamingDayOptions { get; } = new() { GamingDayStartHour = 6 };

    public static BetSettlementService CreateService(
        SqliteTestDb db,
        FakeProtectTeammateStatsLoader loader) =>
        new(db.Factory, loader, Options.Create(GamingDayOptions));

    public static ProtectTeammateSectionData HasData(
        params (string SteamId, string Name, int Count)[] players)
    {
        var map = players.ToDictionary(
            p => p.SteamId,
            p => new ProtectTeammatePlayerStats
            {
                SteamId = p.SteamId,
                Name = p.Name,
                Count = p.Count,
            },
            StringComparer.Ordinal);

        return ProtectTeammateSectionData.HasData(map, priorityPlayerPresent: true);
    }
}
